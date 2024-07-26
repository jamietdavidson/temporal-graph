using Graph.Api;
using Graph.Core;

namespace Graph.Mongo;

public class StateMapExecutor
{
    private Core.Node _node;

    private ThreadValidator _threadValidator;

    public Cursor? Cursor { get; set; }

    public Guid TemplateGuid { get; private set; }

    public Guid? Guid { get; private set; }

    public List<Guid> PartyGuids { get; private set; }

    public Guid? ActivePartyGuid { get; private set; }

    public Core.Node Template
    {
        get
        {
            _CheckCursorState();

            if (!Cursor!.NodeGuids.ContainsKey(TemplateGuid))
                return Cursor.NowAsync(TemplateGuid).Result!;

            return Cursor.NodeGuids[TemplateGuid];
        }
    }

    public Core.Node Node
    {
        get { return _node; }
        set { _node = value; }
    }

    public async Task RefreshNodeAsync()
    {
        _CheckCursorState();
        var stateMapNode = await Cursor!.NowAsync((Guid)Guid!);
        if (stateMapNode == null) throw new Exception("StateMap node not found");
        _node = stateMapNode;
    }
    public StateMapExecutor(
        Guid templateGuid,
        Core.Node? node = null,
        List<Guid>? partyGuids = null,
        Cursor? cursor = null,
        Guid? activePartyGuid = null
    )
    {
        TemplateGuid = templateGuid;

        if (node != null)
        {
            Guid = node.Guid;
            _node = node;
        }

        PartyGuids = partyGuids ?? new List<Guid>();
        Cursor = cursor;
        ActivePartyGuid = activePartyGuid;
    }

    public static async Task<StateMapExecutor> LoadAsync(Guid stateMapId, Guid partyId, Cursor? cursor = null)
    {
        cursor = cursor ?? new Cursor(new Session(new MongoDataStore()));
        await cursor.NowAsync(new List<Guid> { stateMapId, partyId });

        if (!cursor.NodeGuids.ContainsKey(stateMapId))
            throw new GraphQLException($"State map id '{stateMapId}' does not exist.");

        if (!cursor.NodeGuids.ContainsKey(partyId))
            throw new GraphQLException($"Party id '{partyId}' does not exist.");

        var stateMapNode = cursor.NodeGuids[stateMapId];

        var parties = (EdgeCollection<Core.Node, Core.Node>?)await stateMapNode.GetValueAsync("parties");
        if (!parties?.Contains(partyId) ?? false)
            throw new GraphQLException($"Party id {partyId} is not authorized to access state map id {stateMapId}");

        var template = (Core.Node?)await stateMapNode.GetValueAsync("template");
        if (template == null) throw new Exception("StateMap has no template");

        var allTemplateNodeGuids = ((List<string>)(await template.GetValueAsync("_allGuids"))!)
            .Select(g => new Guid(g)).ToList();

        await cursor.NowAsync(allTemplateNodeGuids.Concat(GetPromisedObjectGuidsAsync(stateMapNode)).ToList());

        return new StateMapExecutor(
            ((Core.Node?)await stateMapNode.GetValueAsync("template"))!.Guid,
            stateMapNode,
            parties!.GetGuidsForRange(0, parties.Count),
            cursor,
            partyId
        );
    }

    public async Task FromTemplateAsync(Guid templateGuid, Cursor? cursor = null)
    {
        if (Guid != null)
            throw new Exception("StateMap already created");

        Cursor = cursor ?? new Cursor(new Session(new MongoDataStore()));
        await Cursor.NowAsync(templateGuid);
        var template = Cursor.NodeGuids[templateGuid];

        var stateMapRep = new NodeRep("StateMap");

        stateMapRep.Edges.Tags.Add("template", "Template");
        stateMapRep.Edges.Values.Add("template", templateGuid);

        var templateParties = (EdgeCollection<Core.Node, Core.Node>?)await template.GetValueAsync("parties");
        if (templateParties == null || templateParties.Count == 0) throw new Exception("Template has no parties");

        // StateMap node has a psuedo-dictionary of edges that point to object instances
        var objectPromises = (EdgeCollection<Core.Node, Core.Node>?)await template.GetValueAsync("object_promises");
        if (objectPromises == null || objectPromises.Count == 0) throw new Exception("Template has no object promises");
        var objectPromiseLookup = new Dictionary<Guid, (string, bool)>();
        var evergreenCreateActions = (EdgeCollection<Core.Node, Core.Node>?)await template.GetValueAsync("evergreen_create_actions");
        foreach (var objectPromise in await objectPromises.GetAllNodesAsync())
        {
            // If the object promise has a threaded context,
            // then the object instance will be an edge on the thread node.
            // We can skip it here because the edge keys will be set when each individual thread is spawned.
            if ((Core.Node?)await objectPromise.GetValueAsync("context") != null)
                continue;

            var objectType = (Core.Node?)await objectPromise.GetValueAsync("object_type");
            var tag = (string)(await objectType!.GetValueAsync("name"))!;

            // If the object promise is fulfilled by an evergreen action,
            // then a new instance will be created every time the action is performed.
            // Therefore, the promise is flagged as a list because an edge collection must be used to reference the instances.
            var isList = false;
            if ((evergreenCreateActions?.Count ?? 0) > 0)
            {
                // If an object promised is referenced by a single action, then that action is a CREATE action.
                var referencedByActions = (EdgeCollection<Core.Node, Core.Node>?)await objectPromise.GetValueAsync("referenced_by_actions");
                if ((referencedByActions?.Count ?? 0) == 1 && evergreenCreateActions!.Contains(referencedByActions!.GetGuidsForRange(0, 1).First()))
                    isList = true;
            }

            objectPromiseLookup[objectPromise.Guid] = (tag, isList);
        }

        await Cursor.TogetherAsync(operation: async () =>
        {
            var tasks = new List<Task>();
            var parties = new List<Core.Node>();
            foreach (var templatePartyGuid in templateParties.GetGuidsForRange(0, templateParties.Count))
            {
                var party = _PartyFromTemplate(templatePartyGuid);
                party.Cursor = Cursor;
                tasks.Add(party.SaveAsync());
                parties.Add(party);
                var key = "_party_" + templatePartyGuid.ToString();
                stateMapRep.Edges.Tags.Add(key, "Party");
                stateMapRep.Edges.Values.Add(key, party.Guid);
            }
            stateMapRep.EdgeCollections.Tags.Add("parties", "Party");
            stateMapRep.EdgeCollections.Values.Add("parties", parties.Select(p => p.Guid).ToList());

            // Initial available actions are the entry actions
            var entryActions = (EdgeCollection<Core.Node, Core.Node>?)await template.GetValueAsync("entry_actions");
            if (entryActions == null) throw new Exception("Template has no entry actions");
            stateMapRep.EdgeCollections.Tags.Add("available_actions", "Action");
            stateMapRep.EdgeCollections.Values.Add(
                "available_actions",
                entryActions.GetGuidsForRange(0, entryActions.Count)
            );

            stateMapRep.EdgeCollections.Tags.Add("performed_actions", "Action");
            stateMapRep.EdgeCollections.Values.Add("performed_actions", new List<Guid>());
            stateMapRep.EdgeCollections.Tags.Add("satisfied_checkpoints", "Checkpoint");
            stateMapRep.EdgeCollections.Values.Add("satisfied_checkpoints", new List<Guid>());
            stateMapRep.EdgeCollections.Tags.Add("available_thread_groups", "ThreadGroup");
            stateMapRep.EdgeCollections.Values.Add("available_thread_groups", new List<Guid>());

            // Create psuedo-dictionary of {object promise: object instance} edges
            foreach (var (objectPromiseGuid, (tag, isList)) in objectPromiseLookup)
            {
                var key = "_object_" + objectPromiseGuid.ToString();
                if (isList)
                {
                    stateMapRep.EdgeCollections.Tags.Add(key, tag);
                    stateMapRep.EdgeCollections.Values.Add(key, new List<Guid>());
                }
                else
                {
                    stateMapRep.Edges.Tags.Add(key, tag);
                    stateMapRep.Edges.Values.Add(key, null);
                }
            }

            var stateMap = Core.Node.FromRep(stateMapRep);
            stateMap.Cursor = Cursor;
            tasks.Add(stateMap.SaveAsync());

            await Task.WhenAll(tasks);
            Guid = stateMap.Guid;
        });
    }

    // Sets default values on a Create operation as required,
    // and returns a list of SetEdge operations according to the default edges that must be set.
    public async Task<List<SetEdge>> SetDefaultValuesAsync(Create createOperation, List<SetEdge>? providedEdgesToSet)
    {
        var action = await _GetActionNodeAsync(createOperation.ActionId);
        var operation = (Core.Node)(await action.GetValueAsync("operation"))!;
        var defaultValues = await Utils.GetEdgeCollectionNodesAsync(operation, "default_values");

        if (defaultValues.Count == 0)
            return await _GetDefaultEdgeOperationsAsync(createOperation, operation, providedEdgesToSet);

        var includedKeys = new Dictionary<string, IEnumerable<string>?>
        {
            { "boolean", createOperation.BooleanFields?.Select(fd => fd.Key) },
            { "string", createOperation.StringFields?.Select(fd => fd.Key) },
            { "numeric", createOperation.NumericFields?.Select(fd => fd.Key) },
            { "boolean_list", createOperation.BooleanListFields?.Select(fd => fd.Key) },
            { "string_list", createOperation.StringListFields?.Select(fd => fd.Key) },
            { "numeric_list", createOperation.NumericListFields?.Select(fd => fd.Key) }
        };

        foreach (var defaultValue in defaultValues)
        {
            var key = ((string?)await defaultValue.GetValueAsync("key"))!;
            var valueType = ((string?)await defaultValue.GetValueAsync("value_type"))!;

            if (includedKeys[valueType]?.Contains(key) ?? false) continue;

            if (valueType == "boolean")
            {
                if (createOperation.BooleanFields == null)
                    createOperation.BooleanFields = new List<BooleanFieldData>();

                createOperation.BooleanFields!.Add(new BooleanFieldData
                {
                    Key = key,
                    Value = (bool)(await defaultValue.GetValueAsync(valueType + "_default_value"))!
                });
            }
            else if (valueType == "string")
            {
                if (createOperation.StringFields == null)
                    createOperation.StringFields = new List<StringFieldData>();

                createOperation.StringFields!.Add(new StringFieldData
                {
                    Key = key,
                    Value = (string)(await defaultValue.GetValueAsync(valueType + "_default_value"))!
                });
            }
            else if (valueType == "numeric")
            {
                if (createOperation.NumericFields == null)
                    createOperation.NumericFields = new List<NumericFieldData>();

                createOperation.NumericFields!.Add(new NumericFieldData
                {
                    Key = key,
                    Value = (await defaultValue.GetValueAsync(valueType + "_default_value"))!.ToString()
                });
            }
            else if (valueType == "boolean_list")
            {
                if (createOperation.BooleanListFields == null)
                    createOperation.BooleanListFields = new List<BooleanListFieldData>();

                createOperation.BooleanListFields!.Add(new BooleanListFieldData
                {
                    Key = key,
                    Value = (List<bool?>)(await defaultValue.GetValueAsync(valueType + "_default_value"))!
                });
            }
            else if (valueType == "string_list")
            {
                if (createOperation.StringListFields == null)
                    createOperation.StringListFields = new List<StringListFieldData>();

                createOperation.StringListFields!.Add(new StringListFieldData
                {
                    Key = key,
                    Value = (List<string?>)(await defaultValue.GetValueAsync(valueType + "_default_value"))!
                });
            }
            else if (valueType == "numeric_list")
            {
                if (createOperation.NumericListFields == null)
                    createOperation.NumericListFields = new List<NumericListFieldData>();

                createOperation.NumericListFields!.Add(new NumericListFieldData
                {
                    Key = key,
                    Value = ((List<decimal?>?)await defaultValue.GetValueAsync(valueType + "_default_value"))!
                        .Select(v => v.ToString()).ToList()
                });
            }
        }

        return await _GetDefaultEdgeOperationsAsync(createOperation, operation, providedEdgesToSet);
    }

    private async Task<List<SetEdge>> _GetDefaultEdgeOperationsAsync(
        Create createOperation,
        Core.Node operation,
        List<SetEdge>? providedEdgesToSet
    )
    {
        var edgesToSet = new List<SetEdge>();
        var providedKeys = providedEdgesToSet?.Where(se => se.ActionId == createOperation.ActionId)
            .Select(se => se.Key).ToList() ?? new List<string>();

        var defaultEdges = await Utils.GetEdgeCollectionNodesAsync(operation, "default_edges");
        foreach (var defaultEdge in defaultEdges)
        {
            var key = (string)(await defaultEdge.GetValueAsync("key"))!;
            if (providedKeys.Contains(key)) continue;

            var defaultObjectPromiseId = new Guid(((string?)await defaultEdge.GetValueAsync("string_default_value"))!);
            var toNode = await GetPromisedObjectAsync(defaultObjectPromiseId);
            if (toNode == null) throw new Exception("Default object promise not found");

            edgesToSet.Add(new SetEdge
            {
                ActionId = createOperation.ActionId,
                Key = key,
                FromNodeId = createOperation.GetInstance(Cursor!).Guid,
                ToNodeId = toNode.Guid
            });
        }

        return edgesToSet;
    }

    private async Task<Core.Node> _GetActionNodeAsync(Guid actionId)
    {
        _CheckCursorState();

        if (Cursor!.NodeGuids.ContainsKey(actionId))
            return Cursor.NodeGuids[actionId];

        var action = await Cursor.NowAsync(actionId);
        if (action == null) throw new Exception("Action not found");
        return action;
    }

    public async Task ValidateActionsAsync(
        Guid partyGuid,
        Dictionary<Guid, List<IOperation>> actionOperations
    )
    {
        _CheckCursorState();
        _threadValidator = new ThreadValidator(stateMap: this);

        var tasks = new List<Task>();
        foreach (var (actionGuid, operations) in actionOperations)
        {
            IOperationWithFieldData? createOrUpdate = null;
            Guid? affectedNodeId = null;
            var relationshipOperations = new List<IGraphRelationshipOperation>();
            SpawnThread? threadSpawnOperation = null;
            foreach (var operation in operations)
            {
                // Double-checking here to be absolutely certain
                if (operation.ActionId != actionGuid)
                    throw new Exception("Action id mismatch");

                if (operation is Create || operation is Update)
                {
                    // A single action id must have exactly one Create or Update operation
                    if (createOrUpdate != null)
                        throw new Exception("Multiple Create/Update operations specified for action id " + actionGuid);

                    createOrUpdate = (IOperationWithFieldData)operation;

                    if (operation is Create)
                        affectedNodeId = ((Create)operation).GetInstance(Cursor!).Guid;
                    else
                        affectedNodeId = ((Update)operation).NodeId;
                }
                else if (operation is IGraphRelationshipOperation)
                    relationshipOperations.Add((IGraphRelationshipOperation)operation);
                else if (operation is SpawnThread)
                {
                    if (threadSpawnOperation != null)
                        throw new Exception("Multiple SpawnThread operations specified for action id " + actionGuid);

                    threadSpawnOperation = (SpawnThread)operation;
                }
                else
                    throw new Exception("Invalid operation type");
            }

            // If an action id is specified by an operation,
            // there must be at least one Create or Update operation
            // (cannot perform an IGraphRelationshipOperation without a Create or Update operation)
            if (createOrUpdate == null || affectedNodeId == null)
                throw new Exception("No Create/Update operation specified for referenced action id " + actionGuid);

            tasks.Add(_ValidateActionAsync(
                partyGuid,
                actionGuid,
                (Guid)affectedNodeId,
                createOrUpdate,
                relationshipOperations,
                threadSpawnOperation
            ));
        }

        await Task.WhenAll(tasks);
    }

    private Core.Node _PartyFromTemplate(Guid templatePartyGuid)
    {
        var partyRep = new NodeRep("Party");
        partyRep.Edges.Tags.Add("template_party", "TemplateParty");
        partyRep.Edges.Values.Add("template_party", templatePartyGuid);

        var party = Core.Node.FromRep(partyRep);
        PartyGuids.Add(party.Guid);
        return party;
    }

    private async Task _ValidateActionAsync(
        Guid partyGuid,
        Guid actionGuid,
        Guid affectedNodeId,
        IOperationWithFieldData operation,
        List<IGraphRelationshipOperation>? relationshipOperations = null,
        SpawnThread? threadSpawnOperation = null
    )
    {
        _CheckCursorState();

        if (Guid == null) throw new Exception("StateMap not instantiated");

        var party = Cursor!.NodeGuids[partyGuid];
        var action = Cursor.NodeGuids[actionGuid];

        // Is the party authorized to perform the action?
        var templateParty = (Core.Node?)await party.GetValueAsync("template_party");
        var actionParty = (Core.Node?)await action.GetValueAsync("party");
        if (templateParty == null || actionParty?.Guid != templateParty?.Guid)
            throw new Exception("Party is not authorized to perform that action id " + actionGuid);

        var objectPromise = ((Core.Node?)await action.GetValueAsync("object_promise"))!;
        var objectType = ((Core.Node?)await objectPromise.GetValueAsync("object_type"))!;

        var isEvergreenAction = ((EdgeCollection<Core.Node, Core.Node>?)await Template.GetValueAsync(
            "evergreen_actions"
        ))?.Contains(actionGuid) ?? false;
        var isEvergreenCreateAction = isEvergreenAction && (
            ((EdgeCollection<Core.Node, Core.Node>?)await Template.GetValueAsync(
                "evergreen_create_actions"
            ))?.Contains(actionGuid) ?? false
        );

        Core.Node? affectedNode;
        if (threadSpawnOperation == null)
        {
            var isThreadedAction = await action.GetValueAsync("context") != null;

            // Is the action available?
            if (isThreadedAction)
                await _threadValidator.ValidateThreadedActionAsync(action, (IOperationWithThreadId)operation);
            else
            {
                if (!((EdgeCollection<Core.Node, Core.Node>)(await Node.GetValueAsync(
                    "available_actions"
                ))!).Contains(actionGuid))
                    throw new Exception("Action is not available");
            }

            // The action type will be inferred from whether the promised object has been instantiated.
            if (isEvergreenCreateAction)
                affectedNode = null;
            else
            {
                Core.Node affectedNodeContext;
                if (isThreadedAction)
                    // Use the thread node as the context from which to resolve the promised object.
                    affectedNodeContext = Cursor!.NodeGuids[(Guid)((IOperationWithThreadId)operation).ThreadId!];
                else
                    affectedNodeContext = Node;

                affectedNode = await GetPromisedObjectAsync(objectPromise.Guid, affectedNodeContext);
            }
        }
        else // Spawning a new thread
        {
            if (!(operation is Create))
                throw new Exception("Invalid action type (UPDATE) for ThreadSpawn operation");

            await _threadValidator.ValidateThreadSpawnActionAsync(
                action,
                (Create)operation,
                threadSpawnOperation
            );

            affectedNode = null;
        }

        if (operation is Create)
        {
            // If the affected node already exists, it can be inferred that the action type is UPDATE.
            if (affectedNode != null)
                throw new Exception("Invalid operation (CREATE) for action (UPDATE)");

            var expectedTag = ((string?)await objectType!.GetValueAsync("name"))!;
            var providedTag = ((Create)operation).Tag;
            if (providedTag != expectedTag)
                throw new Exception($"Invalid tag: expected {expectedTag}, got \"{providedTag}\"");
        }
        else if (operation is Update)
        {
            // If the affected node does not exist, it can be inferred that the operation type is CREATE.
            if (affectedNode == null)
                throw new Exception("Invalid operation (UPDATE) for action (CREATE)");

            if (affectedNode.Guid != ((Update)operation).NodeId)
                throw new Exception("Invalid node id");
        }
        else
            throw new Exception("Invalid operation type");

        await _ValidateIncludedFieldsAsync(
            action,
            objectType,
            affectedNodeId,
            operation,
            relationshipOperations
        );
    }

    private async Task _ValidateIncludedFieldsAsync(
        Core.Node action,
        Core.Node objectType,
        Guid affectedNodeId,
        IOperationWithFieldData operation,
        List<IGraphRelationshipOperation>? relationshipOperations
    )
    {
        // Do the provided fields/edges/edge collections match the action's operation specifications?
        var actionOperation = (Core.Node)(await action.GetValueAsync("operation"))!;
        var fieldInclusionType = (string)(await actionOperation.GetValueAsync("inclusion_type"))!;

        var objectTypeAttributes = ((EdgeCollection<Core.Node, Core.Node>?)await objectType.GetValueAsync("attributes"))!;
        var attributes = await objectTypeAttributes.GetAllNodesAsync();
        var attributesDict = new Dictionary<string, (string, string?)>();
        foreach (var attribute in attributes)
        {
            var key = await attribute.GetValueAsync("name");
            var type = await attribute.GetValueAsync("type");
            if (key != null && type != null)
                attributesDict.Add(
                    (string)key,
                    ((string)type, (string?)await attribute.GetValueAsync("object_type"))
                );
        }

        var allowedKeys = new List<string>();
        if (fieldInclusionType == "include")
        {
            var includedKeys = (List<string>?)await actionOperation.GetValueAsync("include");
            if (includedKeys != null)
                allowedKeys.AddRange(includedKeys);
        }
        else if (fieldInclusionType == "exclude")
        {
            var excludedKeys = (List<string>?)await actionOperation.GetValueAsync("exclude");
            if (excludedKeys != null)
            {
                foreach (var key in attributesDict.Keys)
                {
                    if (!excludedKeys.Contains(key))
                        allowedKeys.Add(key);
                }
            }
            else
                allowedKeys.AddRange(attributesDict.Keys);
        }
        else
            throw new Exception("Invalid inclusion type");

        var fieldListsToCheck = new Dictionary<string, List<IFieldData>?>
        {
            { "STRING", operation.StringFields?.Select(fd => (IFieldData)fd).ToList() },
            { "NUMERIC", operation.NumericFields?.Select(fd => (IFieldData)fd).ToList() },
            { "BOOLEAN", operation.BooleanFields?.Select(fd => (IFieldData)fd).ToList() },
            { "STRING_LIST", operation.StringListFields?.Select(fd => (IFieldData)fd).ToList() },
            { "NUMERIC_LIST", operation.NumericListFields?.Select(fd => (IFieldData)fd).ToList() },
            { "BOOLEAN_LIST", operation.BooleanListFields?.Select(fd => (IFieldData)fd).ToList() }
        };
        var defaultValuesDict = await _GetDefaultValuesDictAsync(actionOperation);

        foreach (var (actualType, fieldList) in fieldListsToCheck)
        {
            foreach (var fieldData in fieldList ?? new List<IFieldData>())
            {
                if (!allowedKeys.Contains(fieldData.Key) && !_IsDefaultValue(fieldData, actualType, defaultValuesDict))
                    throw new Exception($"Attribute inclusion not permitted in operation of action id {action.Guid}: \"{fieldData.Key}\"");

                var expectedType = attributesDict[fieldData.Key].Item1;
                if (actualType != expectedType)
                    throw new Exception($"Attribute type mismatch: {fieldData.Key} (expected {expectedType}, got {actualType})");
            }
        }

        if (relationshipOperations == null) return;

        var defaultEdgesDict = await _GetDefaultEdgesDictAsync(actionOperation);
        var objectTypeNodes = await Utils.GetEdgeCollectionNodesAsync(Template, "object_types");
        foreach (var relationshipOperation in relationshipOperations)
        {
            if (
                !allowedKeys.Contains(relationshipOperation.Key)
                && !await _IsDefaultEdgeAsync(relationshipOperation, defaultEdgesDict)
            )
                throw new Exception($"Attribute inclusion not permitted in operation of action id {action.Guid}: \"{relationshipOperation.Key}\"");

            string actualType;
            Guid fromNodeId;
            if (relationshipOperation is SetEdge)
            {
                actualType = "EDGE";
                var setEdge = (SetEdge)relationshipOperation;
                fromNodeId = (Guid)setEdge.FromNodeId!;
            }
            else if (relationshipOperation is UnsetEdge)
            {
                actualType = "EDGE";
                var unsetEdge = (UnsetEdge)relationshipOperation;
                fromNodeId = unsetEdge.FromNodeId;
            }
            else if (relationshipOperation is AddToEdgeCollection)
            {
                actualType = "EDGE_COLLECTION";
                var addToEdgeCollection = (AddToEdgeCollection)relationshipOperation;
                fromNodeId = (Guid)addToEdgeCollection.FromNodeId!;
            }
            else if (relationshipOperation is RemoveFromEdgeCollection)
            {
                actualType = "EDGE_COLLECTION";
                var removeFromEdgeCollection = (RemoveFromEdgeCollection)relationshipOperation;
                fromNodeId = removeFromEdgeCollection.FromNodeId;
            }
            else
                throw new Exception("Invalid relationship operation type");

            var expectedType = attributesDict[relationshipOperation.Key].Item1;
            if (expectedType != actualType)
                throw new Exception($"Attribute type mismatch: {relationshipOperation.Key} (expected {expectedType}, got {actualType})");

            if (fromNodeId != affectedNodeId)
                throw new Exception($"FromNodeId mismatch: {relationshipOperation.Key} (expected {affectedNodeId}, got {fromNodeId})");
        }
    }

    private async Task<Dictionary<string, object?>> _GetDefaultValuesDictAsync(Core.Node actionOperation)
    {
        var defaultValuesDict = new Dictionary<string, object?>();
        var defaultValues = await Utils.GetEdgeCollectionNodesAsync(actionOperation, "default_values");

        // Get value types in parallel first in order to get the correct default value
        var valueTypeTasks = new List<(Core.Node, Task<object?>)>();
        foreach (var defaultValue in defaultValues)
            valueTypeTasks.Add((defaultValue, defaultValue.GetValueAsync("value_type")));
        await Task.WhenAll(valueTypeTasks.Select(x => x.Item2));

        // Get all keys and default values in parallel
        var kvpTasks = new List<(Task<object?>, Task<object?>)>();
        foreach (var (defaultValue, valueTypeTask) in valueTypeTasks)
        {
            kvpTasks.Add((
                defaultValue.GetValueAsync("key"),
                defaultValue.GetValueAsync(valueTypeTask.Result + "_default_value")
            ));
        }
        await Task.WhenAll(kvpTasks.Select(x => x.Item1).Concat(kvpTasks.Select(x => x.Item2)));

        // Return a dict in the form of {key: default_value}
        return kvpTasks.ToDictionary(
            x => (string)x.Item1.Result!,
            x => x.Item2.Result
        );
    }

    private async Task<Dictionary<string, Guid>> _GetDefaultEdgesDictAsync(Core.Node actionOperation)
    {
        var defaultEdgesDict = new Dictionary<string, object?>();
        var defaultEdges = await Utils.GetEdgeCollectionNodesAsync(actionOperation, "default_edges");

        // Get all keys and default edges in parallel
        var kvpTasks = new List<(Task<object?>, Task<object?>)>();
        foreach (var defaultEdge in defaultEdges)
        {
            kvpTasks.Add((
                defaultEdge.GetValueAsync("key"),
                defaultEdge.GetValueAsync("string_default_value")
            ));
        }
        await Task.WhenAll(kvpTasks.Select(x => x.Item1).Concat(kvpTasks.Select(x => x.Item2)));

        // Return a dict in the form of {key: default_value}
        return kvpTasks.ToDictionary(
            x => (string)x.Item1.Result!,
            x => new Guid((string)x.Item2.Result!)
        );
    }

    private bool _IsDefaultValue(IFieldData fieldData, string type, Dictionary<string, object?> defaultValues)
    {
        if (!defaultValues.ContainsKey(fieldData.Key))
            return false;

        object? value;
        switch (type)
        {
            case "STRING":
                value = ((StringFieldData)fieldData).Value;
                break;
            case "NUMERIC":
                value = decimal.Parse(((NumericFieldData)fieldData).Value!);
                break;
            case "BOOLEAN":
                value = ((BooleanFieldData)fieldData).Value;
                break;
            case "STRING_LIST":
                value = ((StringListFieldData)fieldData).Value;
                break;
            case "NUMERIC_LIST":
                value = Api.Utils.ToDecimalList(((NumericListFieldData)fieldData).Value, "");
                break;
            case "BOOLEAN_LIST":
                value = ((BooleanListFieldData)fieldData).Value;
                break;
            default:
                throw new Exception("Invalid field type");
        }

        return value?.GetType().Name == defaultValues[fieldData.Key]?.GetType().Name
            && new Comparison(value, "EQUALS", defaultValues[fieldData.Key]).Result;
    }

    private async Task<bool> _IsDefaultEdgeAsync(IGraphRelationshipOperation operation, Dictionary<string, Guid> defaultEdges)
    {
        if (!(operation is SetEdge) || !defaultEdges.ContainsKey(operation.Key))
            return false;

        var setEdge = (SetEdge)operation;
        return setEdge.ToNodeId != null
            && setEdge.ToNodeId == (await GetPromisedObjectAsync(defaultEdges[operation.Key]))?.Guid;
    }

    public async Task UpdateStateAsync(Dictionary<Guid, List<IOperation>> actionOperations)
    {
        _CheckCursorState();

        // Group action operations by thread context
        var contextToActionOperations = new Dictionary<Guid, Dictionary<Guid, List<IOperation>>>();
        foreach (var (actionGuid, operations) in actionOperations)
        {
            var threadId = ((IOperationWithThreadId)operations.Find(o => o is IOperationWithThreadId)!).ThreadId ?? Node.Guid;

            if (!contextToActionOperations.ContainsKey(threadId))
                contextToActionOperations.Add(threadId, new Dictionary<Guid, List<IOperation>>());

            contextToActionOperations[threadId].Add(actionGuid, operations);
        }

        var evergreenActions = (EdgeCollection<Core.Node, Core.Node>)(await Template.GetValueAsync("evergreen_actions"))!;

        // For newly spawned thread contexts
        var newContextsToActionOperations = new Dictionary<Guid, Dictionary<Guid, List<IOperation>>>();

        Func<Guid, Dictionary<Guid, List<IOperation>>, bool, Task> updateContextState = async (
            contextId, operationsByActionId, isNewThreadContext
        ) =>
        {
            var context = Cursor!.NodeGuids[contextId];
            var threadContext = new ThreadContext(stateMap: this, context);

            var actionsToDependentCheckpointTasks = new Dictionary<Guid, Task<object?>>();
            var performedActions = (EdgeCollection<Core.Node, Core.Node>)(await context.GetValueAsync("performed_actions"))!;
            foreach (var (performedActionId, operations) in operationsByActionId)
            {

                var performedAction = Cursor.NodeGuids[performedActionId];

                // If it's an existing context, new threads may need to be spawned.
                if (!isNewThreadContext)
                {
                    var threadSpawnOperation = (SpawnThread?)operations.Find(o => o is SpawnThread);
                    if (threadSpawnOperation != null)
                    {
                        // The action creates a new thread context,
                        // the state of which will be updated after the existing contexts have been dealt with.
                        var newThread = await _SpawnThreadAsync(context, performedAction, threadSpawnOperation);
                        newContextsToActionOperations.Add(newThread.Guid, operationsByActionId);
                        continue;
                    }
                }

                // Add the performed action to the list of performed actions
                performedActions.Append(performedAction);
                if (evergreenActions.Contains(performedActionId))
                    // Evergreen actions have no dependents
                    continue;

                actionsToDependentCheckpointTasks.Add(
                    performedActionId,
                    performedAction.GetValueAsync("dependent_checkpoints")
                );
            }
            await performedActions.DeltaToDataAsync();

            // Construct a list of any checkpoint nodes that depend on the performed actions.
            await Task.WhenAll(actionsToDependentCheckpointTasks.Values);
            var newlyPerformedActionsToDependentCheckpoints = actionsToDependentCheckpointTasks.ToDictionary(
                x => x.Key,
                x => (EdgeCollection<Core.Node, Core.Node>?)x.Value.Result
            );
            var nodeListTasks = newlyPerformedActionsToDependentCheckpoints.Values.Select(
                collection => collection == null
                ? Task.Run(() => new List<Core.Node>())
                : collection.GetAllNodesAsync()
            ).ToList();
            var dependentCheckpointLists = await Task.WhenAll(nodeListTasks);
            var dependentCheckpoints = new Dictionary<Guid, Core.Node>();
            foreach (List<Core.Node> nodeList in dependentCheckpointLists)
            {
                foreach (var dependentCheckpoint in nodeList)
                    dependentCheckpoints[dependentCheckpoint.Guid] = dependentCheckpoint;
            }

            await _AttachPromisedObjects(actionOperations, context);

            // Evaluate the checkpoints that depend on performed actions.
            var newlySatisfiedCheckpoints = await _EvaluateCheckpointsRecursiveAsync(
                threadContext,
                checkpointsToEvaluate: dependentCheckpoints.Values.ToList()
            );

            await _UpdateUnavailableActionsAsync(
                threadContext,
                newlyPerformedActionsToDependentCheckpoints
            );
            await _UpdateAvailableActionsAndThreadGroupsAsync(
                threadContext,
                newlySatisfiedCheckpoints
            );
        };

        var isNewThreadContext = false;
        foreach (var (contextId, operationsByActionId) in contextToActionOperations)
            await updateContextState(contextId, operationsByActionId, isNewThreadContext);

        isNewThreadContext = true;
        foreach (var (contextId, operationsByActionId) in newContextsToActionOperations)
            await updateContextState(contextId, operationsByActionId, isNewThreadContext);
    }

    private async Task<List<Core.Node>> _EvaluateCheckpointsRecursiveAsync(
        ThreadContext threadContext,
        List<Core.Node>? checkpointsToEvaluate
    )
    {
        var newlySatisfiedCheckpoints = new List<Core.Node>();
        if (checkpointsToEvaluate == null || checkpointsToEvaluate.Count == 0)
            return newlySatisfiedCheckpoints; // nothing to evaluate

        var idsInScope = await _GetIdsInScope(
            threadContext,
            new List<string> {
                "performed_actions",
                "satisfied_checkpoints"
            }
        );

        // Evaluate each checkpoint
        var checkpointStates = new Dictionary<Core.Node, Task<bool>>();
        foreach (var checkpoint in checkpointsToEvaluate)
        {
            checkpointStates.Add(
                checkpoint,
                CheckpointEvaluator.CheckpointIsSatisfiedAsync(
                    stateMap: this,
                    checkpoint,
                    threadContext,
                    idsInScope["performed_actions"],
                    idsInScope["satisfied_checkpoints"]
                )
            );
        }

        // Update the list of satisfied checkpoints,
        // and recursively evaluate checkpoints that depend on any newly satisfied checkpoints
        await Task.WhenAll(checkpointStates.Values);
        var recursiveEvaluationTasks = new List<Task<List<Core.Node>>>();
        foreach (var (checkpoint, isSatisfied) in checkpointStates)
        {
            if (isSatisfied.Result)
            {
                await _AddSatisfiedCheckpoint(checkpoint, threadContext);
                newlySatisfiedCheckpoints.Add(checkpoint);

                recursiveEvaluationTasks.Add(_EvaluateCheckpointsRecursiveAsync(
                    threadContext,
                    await Utils.GetEdgeCollectionNodesAsync(checkpoint, "dependent_checkpoints")
                ));
            }
        }
        foreach (var satisfiedCheckpointList in await Task.WhenAll(recursiveEvaluationTasks))
            newlySatisfiedCheckpoints.AddRange(satisfiedCheckpointList);

        return newlySatisfiedCheckpoints;
    }

    private async Task _AttachPromisedObjects(
        Dictionary<Guid, List<IOperation>> actionOperations,
        Core.Node context
    )
    {
        // Get the created node instances and the corresponding object promises
        var actionsToCreatedNodes = new Dictionary<Guid, Core.Node>();
        var actionsToObjectPromiseTasks = new Dictionary<Guid, Task<object?>>();
        foreach (var (performedActionGuid, operations) in actionOperations)
        {
            foreach (var operation in operations)
            {
                if (operation is Create)
                {
                    actionsToCreatedNodes.Add(
                        performedActionGuid,
                        ((Create)operation).GetInstance(Cursor!)
                    );
                    actionsToObjectPromiseTasks.Add(
                        performedActionGuid,
                        Cursor!.NodeGuids[performedActionGuid].GetValueAsync("object_promise")
                    );
                }
            }
        }
        await Task.WhenAll(actionsToObjectPromiseTasks.Values);

        // Attach the created node instances to the _object_ promise keys on the state map node        
        var evergreenCreateActions = (EdgeCollection<Core.Node, Core.Node>?)await Template.GetValueAsync(
            "evergreen_create_actions"
        );
        var attachEdgesTasks = new List<Task>();
        var edgeCollectionTasks = new List<(Guid, Task<object?>)>();
        foreach (var (actionGuid, objectPromiseTask) in actionsToObjectPromiseTasks)
        {
            var objectPromiseId = ((Core.Node?)objectPromiseTask.Result)!.Guid.ToString();
            if (evergreenCreateActions?.Contains(actionGuid) ?? false)
            {
                // Objects spawned by evergreen create actions must be referenced by an edge collection.
                edgeCollectionTasks.Add((
                    actionGuid,
                    context.GetValueAsync("_object_" + objectPromiseId)
                ));
            }
            else
            {
                attachEdgesTasks.Add(
                    context.SetValueAsync(
                        "_object_" + objectPromiseId,
                        actionsToCreatedNodes[actionGuid]
                    )
                );
            }
        }

        await Task.WhenAll(edgeCollectionTasks.Select(x => x.Item2));
        foreach (var (actionGuid, edgeCollectionTask) in edgeCollectionTasks)
            ((EdgeCollection<Core.Node, Core.Node>?)edgeCollectionTask.Result)!
                .Append(actionsToCreatedNodes[actionGuid]);

        await Task.WhenAll(attachEdgesTasks);
    }

    // Sets a specific context's newly-performed actions as unavailable,
    // unless a given action is evergreen.
    // Note that for a given UpdateGraph mutation,
    // this method is called once for each unique context in which an action was performed.
    private async Task _UpdateUnavailableActionsAsync(
        ThreadContext threadContext,
        Dictionary<Guid, EdgeCollection<Core.Node, Core.Node>?> newlyPerformedActionsToDependentCheckpoints
    )
    {
        var availableActions = (EdgeCollection<Core.Node, Core.Node>)(await _GetNode(threadContext.Thread).GetValueAsync(
            "available_actions"
        ))!;

        // If anything depends on a performed action, remove the action from available actions.
        // If nothing depends on a performed action, it is a perpetually-available evergreen action.
        foreach (var (actionGuid, dependentCheckpoints) in newlyPerformedActionsToDependentCheckpoints)
        {
            if ((dependentCheckpoints?.Count ?? 0) > 0)
                availableActions.Remove(actionGuid);
        }
    }

    // Given a specific context and the newly-satisfied checkpoints of that context,
    // sets dependent actions and checkpoints as available
    // in that context and all existing nested contexts (threads).
    private async Task _UpdateAvailableActionsAndThreadGroupsAsync(
        ThreadContext threadContext,
        List<Core.Node> newlySatisfiedCheckpoints
    )
    {
        // Look up the actions that depend on the newly satisfied checkpoints...
        var dependentActionTasks = new List<Task<List<Core.Node>>>();
        var dependentTemplateThreadGroupTasks = new List<Task<List<Core.Node>>>();
        foreach (var checkpoint in newlySatisfiedCheckpoints)
        {
            dependentActionTasks.Add(Utils.GetEdgeCollectionNodesAsync(checkpoint, "dependent_actions"));
            dependentTemplateThreadGroupTasks.Add(Utils.GetEdgeCollectionNodesAsync(checkpoint, "dependent_thread_groups"));
        }

        // Determine the context of each action
        await Task.WhenAll(dependentActionTasks);
        var actionsToTtgTasks = new Dictionary<Core.Node, Task<object?>>();
        foreach (Task<List<Core.Node>> actionsTask in dependentActionTasks)
        {
            foreach (var action in actionsTask.Result)
                actionsToTtgTasks.Add(action, action.GetValueAsync("context"));
        }

        // Determine the context of each template thread group
        await Task.WhenAll(dependentTemplateThreadGroupTasks);
        var templateThreadGroupsToTtgTasks = new Dictionary<Core.Node, Task<object?>>();
        foreach (Task<List<Core.Node>> templateThreagGroupsTask in dependentTemplateThreadGroupTasks)
        {
            foreach (var templateThreadGroup in templateThreagGroupsTask.Result)
                templateThreadGroupsToTtgTasks.Add(templateThreadGroup, templateThreadGroup.GetValueAsync("context"));
        }

        // Group results by entity type ("Action" or "ThreadGroup")
        await Task.WhenAll(actionsToTtgTasks.Values.Concat(templateThreadGroupsToTtgTasks.Values));
        var entitiesToTemplateThreadGroups = new Dictionary<string, Dictionary<Core.Node, Core.Node?>>
        {
            { "Action", new Dictionary<Core.Node, Core.Node?>() },
            { "ThreadGroup", new Dictionary<Core.Node, Core.Node?>() }
        };
        foreach (var (entity, templateThreadGroupTask) in actionsToTtgTasks)
            entitiesToTemplateThreadGroups["Action"].Add(entity, (Core.Node?)templateThreadGroupTask.Result);
        foreach (var (entity, templateThreadGroupTask) in templateThreadGroupsToTtgTasks)
            entitiesToTemplateThreadGroups["ThreadGroup"].Add(entity, (Core.Node?)templateThreadGroupTask.Result);

        // Keys are context ids
        var pendingUpdates = new Dictionary<Guid, PendingAvailabilityUpdate>();

        var entitiesByTemplateThreadGroupId = new Dictionary<Guid, Dictionary<string, List<Core.Node>>>();
        foreach (var entityType in entitiesToTemplateThreadGroups.Keys)
        {
            foreach (var (entity, templateThreadGroup) in entitiesToTemplateThreadGroups[entityType])
            {
                if (templateThreadGroup?.Guid == threadContext.TemplateThreadGroup?.Guid)
                {
                    // The context id is already known (it's the current context)
                    if (!pendingUpdates.ContainsKey(threadContext.Thread.Guid))
                        pendingUpdates.Add(threadContext.Thread.Guid, new PendingAvailabilityUpdate(threadContext.Thread.Guid));

                    pendingUpdates[threadContext.Thread.Guid].Add(entityType, entity);
                }
                else if (templateThreadGroup == null)
                    // This should never happen, but if it does, this will make debugging easier
                    throw new Exception($"Invalid template: {entityType} depends on checkpoint from nested scope: {entity.Guid}");
                else
                {
                    // The entity is nested and its context (thread) must be resolved
                    if (!entitiesByTemplateThreadGroupId.ContainsKey(templateThreadGroup.Guid))
                        entitiesByTemplateThreadGroupId.Add(
                            templateThreadGroup.Guid,
                            new Dictionary<string, List<Core.Node>>
                            {
                                { "Action", new List<Core.Node>() },
                                { "ThreadGroup", new List<Core.Node>() }
                            }
                        );

                    entitiesByTemplateThreadGroupId[templateThreadGroup.Guid][entityType].Add(entity);
                }
            }
        }

        // The thread path does not include any further-nested threads,
        // so any necessary availability updates must branch out from there to all nested threads that have already been spawned.

        // A dependent checkpoint may specify a different context,
        // in which case its satisfied state would have to be applied to all threads in the specified thread group.
        // Some of those thread groups may not have any threads yet;
        // for such thread groups the satisfied state will be checked upon the spawning of a thread in that group.

        // Determine the nested contexts that need to be updated
        var nestedContexts = new Dictionary<Guid, NestedContext>(); // Keys are the nested context templateThreadGroupIds
        foreach (var templateThreadGroupId in entitiesByTemplateThreadGroupId.Keys)
        {
            var parentContext = threadContext.GetThread(templateThreadGroupId);

            // Any existing nested threads must be found and updated...

            // Determine the path from the current context to the nested context
            // (here "path" means a linked list of template thread group ids)
            var templateThreadGroupPath = await ThreadUtils.ThreadPathAsLinkedListAsync(
                Cursor!.NodeGuids[templateThreadGroupId], "context"
            );
            LinkedList<Guid> templatePathFromParent;
            if (threadContext.TemplateThreadGroup == null)
                // The current context is non-threaded so the full path is used
                templatePathFromParent = templateThreadGroupPath;
            else
            {
                // Truncate the path to not include the current context or its parent scopes
                templatePathFromParent = new LinkedList<Guid>();
                for (var ttg = templateThreadGroupPath.First; ttg != null; ttg = ttg.Next)
                {
                    if (ttg.Value == threadContext.TemplateThreadGroup.Guid)
                        break;

                    templatePathFromParent.AddFirst(ttg.Value);
                }
            }

            nestedContexts.Add(
                templateThreadGroupId,
                new NestedContext(
                    stateMap: this,
                    parentContext,
                    templatePathFromParent
                )
            );
        }

        // Resolve the nested context paths to threads
        var tasks = new List<Task>();
        foreach (var nestedContext in nestedContexts.Values)
            tasks.Add(nestedContext.FindRelevantContextsAsync());

        await Task.WhenAll(tasks);
        foreach (var (templateThreadGroupId, nestedContext) in nestedContexts)
        {
            foreach (var threadId in nestedContext.ThreadIds)
            {
                if (!pendingUpdates.ContainsKey(threadId))
                    pendingUpdates.Add(threadId, new PendingAvailabilityUpdate(threadId));

                pendingUpdates[threadId].AddActions(
                    entitiesByTemplateThreadGroupId[templateThreadGroupId]["Action"]
                );
                pendingUpdates[threadId].AddTemplateThreadGroups(
                    entitiesByTemplateThreadGroupId[templateThreadGroupId]["ThreadGroup"]
                );
            }
        }

        // Get the edge collections for each affected thread
        var availableActionTasksByThreadId = new Dictionary<Guid, Task<object?>>();
        var availableThreadGroupTasksByThreadId = new Dictionary<Guid, Task<object?>>();
        foreach (var threadId in pendingUpdates.Keys)
        {
            availableActionTasksByThreadId.Add(threadId, Cursor!.NodeGuids[threadId].GetValueAsync("available_actions"));
            availableThreadGroupTasksByThreadId.Add(threadId, Cursor!.NodeGuids[threadId].GetValueAsync("available_thread_groups"));
        }

        // Update the availability of actions and thread groups
        await Task.WhenAll(availableActionTasksByThreadId.Values.Concat(availableThreadGroupTasksByThreadId.Values));
        tasks.Clear();
        foreach (var (threadId, pendingAvailabilityUpdate) in pendingUpdates)
        {
            var availableActions = availableActionTasksByThreadId[threadId].Result;
            if (availableActions != null)
                ((EdgeCollection<Core.Node, Core.Node>)availableActions).Append(pendingAvailabilityUpdate.Actions);

            var availableThreadGroups = availableThreadGroupTasksByThreadId[threadId].Result;
            if (availableThreadGroups != null)
                ((EdgeCollection<Core.Node, Core.Node>)availableThreadGroups).Append(pendingAvailabilityUpdate.TemplateThreadGroups);

            tasks.Add(Cursor!.NodeGuids[threadId].SaveAsync());
        }
        await Task.WhenAll(tasks);
    }

    private async Task _AddSatisfiedCheckpoint(Core.Node checkpoint, ThreadContext threadContext)
    {
        var templateThreadGroup = (Core.Node?)await checkpoint.GetValueAsync("context");
        var contextToUpdate = _GetNode(threadContext.GetThread(templateThreadGroup?.Guid));
        var satisfiedCheckpoints = (EdgeCollection<Core.Node, Core.Node>)(await contextToUpdate.GetValueAsync(
            "satisfied_checkpoints"
        ))!;
        satisfiedCheckpoints.Append(checkpoint);
        await contextToUpdate.SaveAsync();
    }

    private void _CheckCursorState()
    {
        if (Cursor == null)
            throw new Exception("StateMap cursor not set");
    }

    public async Task<ActionType> GetActionOperationTypeAsync(Core.Node action)
    {
        _CheckCursorState();

        var objectPromise = ((Core.Node?)await action.GetValueAsync("object_promise"))!;
        var obj = await GetPromisedObjectAsync(objectPromise.Guid);

        return obj == null ? ActionType.Create : ActionType.Update;
    }

    public async Task<Core.Node?> GetPromisedObjectAsync(
        Guid objectPromiseGuid,
        Core.Node? fromNode = null,
        ModeEnum mode = ModeEnum.Data
    )
    {
        _CheckCursorState();
        return (Core.Node?)await _GetNode(fromNode ?? Node).GetValueAsync("_object_" + objectPromiseGuid.ToString(), mode);
    }

    public static List<Guid> GetPromisedObjectGuidsAsync(Core.Node stateMapNode)
    {
        return stateMapNode.AsNodeRep().Edges.Values
            .Where(kvp => kvp.Key.StartsWith("_object_") && kvp.Value != null)
            .Select(kvp => (Guid)kvp.Value!).ToList();
    }

    public async Task<EdgeCollection<Core.Node, Core.Node>?> GetPromisedObjectCollectionAsync(
        Guid objectPromiseGuid,
        Core.Node? fromNode = null
    )
    {
        _CheckCursorState();
        return (EdgeCollection<Core.Node, Core.Node>?)await (fromNode ?? Node).GetValueAsync(
            "_object_" + objectPromiseGuid.ToString()
        );
    }

    public async Task<EdgeCollection<Core.Node, Core.Node>> GetThreadGroupAsync(
        Core.Node fromNode,
        Guid templateThreadGroupGuid
    )
    {
        var threadGroup = (EdgeCollection<Core.Node, Core.Node>?)await fromNode.GetValueAsync("_thread_group_" + templateThreadGroupGuid.ToString());

        if (threadGroup == null)
            throw new Exception($"ThreadGroup {templateThreadGroupGuid} not found on node {fromNode.Guid}");

        return threadGroup;
    }

    private async Task<Core.Node> _SpawnThreadAsync(
        Core.Node? parentThread,
        Core.Node action,
        SpawnThread spawnThreadOperation
    )
    {
        var templateThreadGroup = (Core.Node)(await action.GetValueAsync("context"))!;

        var threadRep = new NodeRep("Thread");
        threadRep.Fields.StringFields = new Dictionary<string, string?>
        {
            { "thread_variable_type", spawnThreadOperation.VariableType },
            { "string_thread_variable", spawnThreadOperation.StringVariableValue },
            { "reference_thread_variable", spawnThreadOperation.ReferenceVariableValue?.ToString() }
        };
        threadRep.Fields.NumericFields = new Dictionary<string, decimal?>
        {
            {
                "numeric_thread_variable",
                spawnThreadOperation.NumericVariableValue != null
                    ? decimal.Parse(spawnThreadOperation.NumericVariableValue)
                    : null
            }
        };
        threadRep.Fields.BooleanFields = new Dictionary<string, bool?>
        { { "boolean_thread_variable", spawnThreadOperation.BooleanVariableValue } };

        threadRep.Edges.Tags = new Dictionary<string, string>
        {
            { "context", "ThreadGroup" },
            { "parent_thread", "Thread" }
        };
        threadRep.Edges.Values = new Dictionary<string, Guid?>
        {
            { "context", templateThreadGroup.Guid },
            { "parent_thread", parentThread?.Guid }
        };

        threadRep.EdgeCollections.Tags = new Dictionary<string, string>
        {
            { "performed_actions", "Action" },
            { "satisfied_checkpoints", "Checkpoint" },
            { "available_actions", "Action" },
            { "available_thread_groups", "ThreadGroup" }
        };
        threadRep.EdgeCollections.Values = new Dictionary<string, List<Guid>>
        {
            { "performed_actions", new List<Guid>() },
            { "satisfied_checkpoints", new List<Guid>() },
            { "available_actions", new List<Guid>() },
            { "available_thread_groups", new List<Guid>() }
        };

        // Populate available actions based on the template
        var threadedActions = await Utils.GetEdgeCollectionNodesAsync(templateThreadGroup, "threaded_actions");
        var actionAvailabilityTasks = new Dictionary<Guid, Task<bool>>();
        foreach (var threadedAction in threadedActions)
        {
            actionAvailabilityTasks.Add(
                threadedAction.Guid,
                ThreadUtils.ThreadedActionIsAvailableAsync(
                    stateMap: this,
                    threadedAction,
                    templateThreadGroup,
                    thread: null,
                    parentContext: parentThread ?? Node
                )
            );
        }
        await Task.WhenAll(actionAvailabilityTasks.Values);
        threadRep.EdgeCollections.Values.Add(
            "available_actions",
            actionAvailabilityTasks.Where(x => x.Value.Result).Select(x => x.Key).ToList()
        );

        var nestedThreadGroups = await Utils.GetEdgeCollectionNodesAsync(templateThreadGroup, "nested_thread_groups");
        var threadGroupAvailabilityTasks = new Dictionary<Guid, Task<bool>>();
        foreach (var nestedThreadGroup in nestedThreadGroups)
        {
            // Add an edge collection to the threadRep for the nested threads
            threadRep.EdgeCollections.Tags.Add(
                "_thread_group_" + nestedThreadGroup.Guid.ToString(),
                "Thread"
            );

            threadGroupAvailabilityTasks.Add(
                nestedThreadGroup.Guid,
                ThreadUtils.ThreadGroupIsAvailableAsync(
                    templateThreadGroup,
                    stateMap: this,
                    parentThread
                )
            );
        }
        await Task.WhenAll(threadGroupAvailabilityTasks.Values);
        threadRep.EdgeCollections.Values.Add(
            "available_thread_groups",
            threadGroupAvailabilityTasks.Where(x => x.Value.Result).Select(x => x.Key).ToList()
        );

        var thread = Core.Node.FromRep(threadRep);
        var parentContext = parentThread ?? Node;
        var threadGroup = (EdgeCollection<Core.Node, Core.Node>)(
            await parentContext.GetValueAsync(
                "_thread_group_" + templateThreadGroup.Guid.ToString()
            )
        )!;
        threadGroup.Append(thread);
        await Task.WhenAll(
            thread.SaveAsync(),
            parentContext.SaveAsync()
        );

        return thread;
    }

    private async Task<Dictionary<string, List<Guid>>> _GetIdsInScope(
        ThreadContext threadContext, List<string> edgeCollectionKeys
    )
    {
        var idsInScope = new Dictionary<string, List<Guid>>();
        foreach (var key in edgeCollectionKeys)
            idsInScope[key] = new List<Guid>();

        foreach (var contextId in threadContext.ThreadPath)
        {
            var context = _GetNode(contextId);
            foreach (var key in edgeCollectionKeys)
            {
                var edgeCollection = (EdgeCollection<Core.Node, Core.Node>?)await context.GetValueAsync(
                    key,
                    ModeEnum.Either
                );
                if (edgeCollection == null) continue;

                idsInScope[key].AddRange(
                    edgeCollection.GetGuidsForRange(0, edgeCollection.Count, ModeEnum.Either)
                );
            }
        }
        return idsInScope;
    }

    private Core.Node _GetNode(Guid nodeGuid)
    {
        return Cursor!.NodeGuids[nodeGuid];
    }

    private Core.Node _GetNode(Core.Node node)
    {
        return Cursor!.NodeGuids[node.Guid];
    }
}

// Helper class for finding all nested threads that depend on a checkpoint from a given parent context.
// There may be many nested contexts (threads), and all must be updated in response to a checkpoint being satisfied.
public class NestedContext
{
    private StateMapExecutor _stateMap;

    private Core.Node _parentContext;

    private LinkedList<Guid> _pathFromParent;

    private List<Guid>? _threadIds = null;

    public List<Guid> ThreadIds
    {
        get
        {
            if (_threadIds == null)
                throw new Exception("Must call FindRelevantContextsAsync before accessing NestedContext.ThreadIds");

            return _threadIds;
        }
    }

    public NestedContext(StateMapExecutor stateMap, Core.Node parentContext, LinkedList<Guid> templatePathFromParent)
    {
        _stateMap = stateMap;
        _parentContext = parentContext;
        _pathFromParent = templatePathFromParent;
    }

    // Given a context in which an checkpoint was satisfied
    // and paths from said context to the nested contexts that contain dependent items,
    // returns a dictionary of nested contexts to dependent items.
    public async Task FindRelevantContextsAsync()
    {
        if (_threadIds != null) return; // already found

        // Find the threads that need to be updated
        var threadsToExplore = new List<Core.Node> { _parentContext };

        // Follow the path to the templateThreadGroup, collecting all relevant threads
        for (var ttg = _pathFromParent.First; ttg != null; ttg = ttg.Next)
        {
            var nextTemplateThreadGroup = _stateMap.Cursor!.NodeGuids[ttg.Value];
            var nextThreadsToExplore = new List<Core.Node>();
            foreach (var thread in threadsToExplore)
            {
                if (!await ThreadUtils.ThreadGroupIsAvailableAsync(
                    nextTemplateThreadGroup,
                    _stateMap,
                    parentThread: thread
                ))
                    continue;

                // All threads in the threadgroup are relevant
                nextThreadsToExplore.Concat(await Utils.GetEdgeCollectionNodesAsync(
                    thread, "_thread_group_" + nextTemplateThreadGroup.Guid
                ));
            }

            threadsToExplore = nextThreadsToExplore;
        }

        // threadsToExplore now contains all threads of the nested context
        _threadIds = threadsToExplore.Select(t => t.Guid).ToList();
    }
}

// Helper class for grouping newly available actions and thread groups by context id
public class PendingAvailabilityUpdate
{
    private List<Core.Node> _actions = new();

    private List<Core.Node> _templateThreadGroups = new();

    public Guid ContextId { get; init; }

    public List<Core.Node> Actions
    {
        get => _actions;
    }

    public List<Core.Node> TemplateThreadGroups
    {
        get => _templateThreadGroups;
    }

    public PendingAvailabilityUpdate(Guid contextId)
    {
        ContextId = contextId;
    }

    public void Add(string entityType, Core.Node entity)
    {
        if (entityType == "Action")
            AddAction(entity);
        else if (entityType == "ThreadGroup")
            AddTemplateThreadGroup(entity);
        else
            throw new Exception("Invalid entity type: " + entityType);
    }

    public void AddAction(Core.Node action)
    {
        _actions.Add(action);
    }

    public void AddActions(List<Core.Node> actions)
    {
        _actions.AddRange(actions);
    }

    public void AddTemplateThreadGroup(Core.Node templateThreadGroup)
    {
        _templateThreadGroups.Add(templateThreadGroup);
    }

    public void AddTemplateThreadGroups(List<Core.Node> templateThreadGroups)
    {
        _templateThreadGroups.AddRange(templateThreadGroups);
    }
}