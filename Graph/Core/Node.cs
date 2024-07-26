using Graph.Api;

namespace Graph.Core;

public class MetaRep
{
    public string Tag { get; set; }
    public Guid Guid { get; set; }
    public string? OnChainId { get; set; }
    public ulong? SubgraphId { get; set; }
}

public class FieldsRep
{
    public Dictionary<string, bool?> BooleanFields { get; set; }
    public Dictionary<string, string?> StringFields { get; set; }
    public Dictionary<string, decimal?> NumericFields { get; set; }
    public Dictionary<string, List<bool?>?> BooleanListFields { get; set; }
    public Dictionary<string, List<string?>?> StringListFields { get; set; }
    public Dictionary<string, List<decimal?>?> NumericListFields { get; set; }
}

public class EdgesRep
{
    public Dictionary<string, string> Tags { get; set; }
    public Dictionary<string, Guid?> Values { get; set; }
    public Dictionary<string, string?> OnChainValues { get; set; }
}

public class EdgeCollectionsRep
{
    public Dictionary<string, string> Tags { get; set; }
    public Dictionary<string, List<Guid>> Values { get; set; }
    public Dictionary<string, List<string>> OnChainValues { get; set; }
}

public class NodeRep
{
    public NodeRep(string? tag = null)
    {
        Deleted = false;
        Mode = ModeEnum.Data;
        Meta = new MetaRep();
        if (tag != null) Meta.Tag = tag;

        Fields = new FieldsRep();
        Fields.BooleanFields = new();
        Fields.StringFields = new();
        Fields.NumericFields = new();
        Fields.StringListFields = new();
        Fields.NumericListFields = new();
        Fields.BooleanListFields = new();
        Edges = new EdgesRep();
        Edges.Tags = new();
        Edges.Values = new();
        Edges.OnChainValues = new();
        EdgeCollections = new EdgeCollectionsRep();
        EdgeCollections.Tags = new();
        EdgeCollections.Values = new();
        EdgeCollections.OnChainValues = new();
    }

    public Guid Guid
    {
        get => Meta.Guid;
        set => Meta.Guid = value!;
    }

    public bool Deleted { get; set; }
    public ModeEnum? Mode { get; set; }
    public MetaRep Meta { get; set; }
    public FieldsRep Fields { get; set; }
    public EdgesRep Edges { get; set; }
    public EdgeCollectionsRep EdgeCollections { get; set; }

    // Compares two NodeReps and returns a string describing the differences.
    // Returns null if the NodeReps are equivalent.
    public static string? AreEquivalent(
        NodeRep repA,
        NodeRep repB
    )
    {
        var differences = CompareMetas(repA, repB);

        return differences.Count > 0
            ? $"{repA.Meta.Tag} NodeReps do not match: {string.Join(", ", differences)}"
            : null;
    }

    public static List<string> CompareMetas(NodeRep repA, NodeRep repB)
    {
        var differences = new List<string>();
        if (repA.Deleted != repB.Deleted)
            differences.Add($"Deleted: {repA.Deleted} != {repB.Deleted}");
        if (repA.Meta.Tag != repB.Meta.Tag)
            differences.Add($"Tag: {repA.Meta.Tag} != {repB.Meta.Tag}");
        if (repA.Meta.Guid != repB.Meta.Guid)
            differences.Add($"Guid: {repA.Meta.Guid} != {repB.Meta.Guid}");
        if (repA.Meta.OnChainId != repB.Meta.OnChainId)
            differences.Add($"OnChainId: {repA.Meta.OnChainId} != {repB.Meta.OnChainId}");

        return differences;
    }

    public static List<string> CompareFields(NodeRep repA, NodeRep repB)
    {
        var differences = new List<string>();

        if (repA.Fields.BooleanFields.Count != repB.Fields.BooleanFields.Count)
            differences.Add($"BooleanFields.Count: {repA.Fields.BooleanFields.Count} != {repB.Fields.BooleanFields.Count}");
        foreach (var key in repA.Fields.BooleanFields.Keys)
        {
            if (!repB.Fields.BooleanFields.ContainsKey(key))
                differences.Add($"BooleanFields: {key} is missing");
            else if (repA.Fields.BooleanFields[key] != repB.Fields.BooleanFields[key])
                differences.Add($"BooleanFields[{key}]: {repA.Fields.BooleanFields[key]} != {repB.Fields.BooleanFields[key]}");
        }

        var stringFieldsA = repA.Fields.StringFields;
        var stringFieldsB = repB.Fields.StringFields;

        if (stringFieldsA.Count != stringFieldsB.Count)
            differences.Add(
                $"StringFields.Count: {stringFieldsA.Count} != {stringFieldsB.Count}");
        foreach (var key in stringFieldsA.Keys)
        {
            if (!stringFieldsB.ContainsKey(key))
                differences.Add($"StringFields: {key} is missing");
            else if (stringFieldsA[key] != stringFieldsB[key])
                differences.Add($"StringFields[{key}]: {stringFieldsA[key]} != {stringFieldsB[key]}");
        }

        var numericFieldsA = repA.Fields.NumericFields;
        var numericFieldsB = repB.Fields.NumericFields;

        if (numericFieldsA.Count != numericFieldsB.Count)
            differences.Add($"NumericFields.Count: {numericFieldsA.Count} != {numericFieldsB.Count}");
        foreach (var key in numericFieldsA.Keys)
        {
            if (!numericFieldsB.ContainsKey(key))
                differences.Add($"NumericFields: {key} is missing");
            else if (numericFieldsA[key] != numericFieldsB[key])
                differences.Add($"NumericFields[{key}]: {numericFieldsA[key]} != {numericFieldsB[key]}");
        }

        var stringListFieldsA = repA.Fields.StringListFields;
        var stringListFieldsB = repB.Fields.StringListFields;

        if (stringListFieldsA.Count != stringListFieldsB.Count)
            differences.Add($"StringListFields.Count: {stringListFieldsA.Count} != {stringListFieldsB.Count}");
        foreach (var key in stringListFieldsA.Keys)
        {
            if (!stringListFieldsB.ContainsKey(key))
                differences.Add($"StringListFields: {key} is missing");
            else
            {
                var listA = stringListFieldsA[key];
                var listB = stringListFieldsB[key];
                if (listA == null && listB == null)
                    continue;
                else if (listA == null || listB == null)
                    differences.Add($"StringListFields[{key}]: {listA} != {listB}");
                else if (listA.Count != listB.Count)
                    differences.Add($"StringListFields[{key}].Count: {listA.Count} != {listB.Count}");
                else
                    for (var i = 0; i < listA.Count; i++)
                        if (listA[i] != listB[i])
                            differences.Add($"StringListFields[{key}][{i}]: {listA[i]} != {listB[i]}");
            }
        }

        var numericListFieldsA = repA.Fields.NumericListFields;
        var numericListFieldsB = repB.Fields.NumericListFields;

        if (numericListFieldsA.Count != numericListFieldsB.Count)
            differences.Add($"NumericListFields.Count: {numericListFieldsA.Count} != {numericListFieldsB.Count}");
        foreach (var key in numericListFieldsA.Keys)
        {
            if (!numericListFieldsB.ContainsKey(key))
                differences.Add($"NumericListFields: {key} is missing");
            else
            {
                var listA = numericListFieldsA[key];
                var listB = numericListFieldsB[key];
                if (listA == null && listB == null)
                    continue;
                else if (listA == null || listB == null)
                    differences.Add($"NumericListFields[{key}]: {listA} != {listB}");
                else if (listA.Count != listB.Count)
                    differences.Add($"NumericListFields[{key}].Count: {listA.Count} != {listB.Count}");
                else
                    for (var i = 0; i < listA.Count; i++)
                        if (listA[i] != listB[i])
                            differences.Add($"NumericListFields[{key}][{i}]: {listA[i]} != {listB[i]}");
            }
        }

        var booleanListFieldsA = repA.Fields.BooleanListFields;
        var booleanListFieldsB = repB.Fields.BooleanListFields;

        if (booleanListFieldsA.Count != booleanListFieldsB.Count)
            differences.Add($"BooleanListFields.Count: {booleanListFieldsA.Count} != {booleanListFieldsB.Count}");
        foreach (var key in booleanListFieldsA.Keys)
        {
            if (!booleanListFieldsB.ContainsKey(key))
                differences.Add($"BooleanListFields: {key} is missing");
            else
            {
                var listA = booleanListFieldsA[key];
                var listB = booleanListFieldsB[key];
                if (listA == null && listB == null)
                    continue;
                else if (listA == null || listB == null)
                    differences.Add($"BooleanListFields[{key}]: {listA} != {listB}");
                else if (listA.Count != listB.Count)
                    differences.Add($"BooleanListFields[{key}].Count: {listA.Count} != {listB.Count}");
                else
                    for (var i = 0; i < listA.Count; i++)
                        if (listA[i] != listB[i])
                            differences.Add($"BooleanListFields[{key}][{i}]: {listA[i]} != {listB[i]}");
            }
        }

        return differences;
    }

    public static List<string> CompareEdges(NodeRep repA, NodeRep repB, bool comparingFromBlockchain = false)
    {
        var differences = new List<string>();

        if (!comparingFromBlockchain)
        {
            if (repA.Edges.Tags.Count != repB.Edges.Tags.Count)
                differences.Add($"Edges.Tags.Count: {repA.Edges.Tags.Count} != {repB.Edges.Tags.Count}");

            foreach (var key in repA.Edges.Tags.Keys)
            {
                if (!repB.Edges.Tags.ContainsKey(key))
                    differences.Add($"Edges.Tags: {key} is missing");
                else if (repA.Edges.Tags[key] != repB.Edges.Tags[key])
                    differences.Add($"Edges.Tags[{key}]: {repA.Edges.Tags[key]} != {repB.Edges.Tags[key]}");
            }
        }

        if (comparingFromBlockchain)
        {
            if (repA.Edges.OnChainValues.Count != repB.Edges.OnChainValues.Count)
                differences.Add(
                    $"Edges.OnChainValues.Count: {repA.Edges.OnChainValues.Count} != {repB.Edges.OnChainValues.Count}");

            foreach (var key in repA.Edges.OnChainValues.Keys)
            {
                if (!repB.Edges.OnChainValues.ContainsKey(key))
                    differences.Add($"Edges.OnChainValues: {key} is missing");
                else if (repA.Edges.OnChainValues[key] != repB.Edges.OnChainValues[key])
                    differences.Add(
                        $"Edges.OnChainValues[{key}]: {repA.Edges.OnChainValues[key]} != {repB.Edges.OnChainValues[key]}");
            }
        }
        else
        {
            if (repA.Edges.Values.Count != repB.Edges.Values.Count)
                differences.Add($"Edges.Values.Count: {repA.Edges.Values.Count} != {repB.Edges.Values.Count}");

            foreach (var key in repA.Edges.Values.Keys)
            {
                if (!repB.Edges.Values.ContainsKey(key))
                    differences.Add($"Edges.Values: {key} is missing");
                else if (repA.Edges.Values[key] != repB.Edges.Values[key])
                    differences.Add($"Edges.Values[{key}]: {repA.Edges.Values[key]} != {repB.Edges.Values[key]}");
            }
        }

        return differences;
    }

    public static List<string> CompareEdgeCollections(NodeRep repA, NodeRep repB, bool comparingFromBlockchain = false)
    {
        var differences = new List<string>();

        if (!comparingFromBlockchain)
        {
            if (repA.EdgeCollections.Tags.Count != repB.EdgeCollections.Tags.Count)
                differences.Add($"EdgeCollections.Tags.Count: {repA.EdgeCollections.Tags.Count} != {repB.EdgeCollections.Tags.Count}");
            foreach (var key in repA.EdgeCollections.Tags.Keys)
            {
                if (!repB.EdgeCollections.Tags.ContainsKey(key))
                    differences.Add($"EdgeCollections.Tags: {key} is missing");
                else
                    CollectionAssert.AreEquivalent(repA.EdgeCollections.Tags[key], repB.EdgeCollections.Tags[key]);
            }

            if (repA.EdgeCollections.Values.Count != repB.EdgeCollections.Values.Count)
                differences.Add($"EdgeCollections.Values.Count: {repA.EdgeCollections.Values.Count} != {repB.EdgeCollections.Values.Count}");
            foreach (var key in repA.EdgeCollections.Values.Keys)
            {
                if (!repB.EdgeCollections.Values.ContainsKey(key))
                    differences.Add($"EdgeCollections.Values: {key} is missing");
                else
                    CollectionAssert.AreEquivalent(repA.EdgeCollections.Values[key], repB.EdgeCollections.Values[key]);
            }
        }
        else
        {
            if (repA.EdgeCollections.OnChainValues.Count != repB.EdgeCollections.OnChainValues.Count)
                differences.Add($"EdgeCollections.OnChainValues.Count: {repA.EdgeCollections.OnChainValues.Count} != {repB.EdgeCollections.OnChainValues.Count}");
            foreach (var key in repA.EdgeCollections.OnChainValues.Keys)
            {
                if (!repB.EdgeCollections.OnChainValues.ContainsKey(key))
                    differences.Add($"EdgeCollections.OnChainValues: {key} is missing");
                else
                    CollectionAssert.AreEquivalent(repA.EdgeCollections.OnChainValues[key], repB.EdgeCollections.OnChainValues[key]);
            }
        }

        return differences;
    }
}

public class Node
{
    private static readonly bool _schemalessEnabled = true;
    // TODO: use reflection to generate runtime types that can be used as registry keys.
    //private static readonly Dictionary<Type, _Schema> _registry = new();
    private static readonly Dictionary<string, _Schema> _registry = new();
    private readonly Dictionary<string, IGraphObject> _changed = new();
    private readonly Dictionary<string, IGraphObjectWithCursor?> _edgeCollections = new();
    private readonly Dictionary<string, IGraphObjectWithCursor?> _edges = new();
    private readonly Dictionary<string, IGraphObject?> _fields = new();
    private readonly string? _tag;
    private Cursor? _cursor;
    private string? _onChainId;
    private ulong? _subgraphId;
    private bool _isReadonly = false;
    private _Schema _schema;

    public Node(
        Guid? guid,
        Cursor? cursor,
        NestedDictionary? nodeDataDict,
        ulong? timestamp,
        NodeSchema? nodeSchema = null
    )
    {
        Guid = guid ?? Guid.NewGuid();
        Cursor = cursor;
        IsDeletable = nodeSchema?.IsDeletable ?? true;
        Deleted = false;

        if (timestamp != null)
        {
            FromDataStore = true;
            Timestamp = (ulong)timestamp;
        }
        else
        {
            Timestamp = Utils.TimestampMillis();
        }

        _InitSchema(
            nodeSchema
        );
        _InitEmptyGraphObjects();

        IsLoaded = false;

        // Set graph object's initial values
        if (nodeDataDict != null)
        {
            if (nodeDataDict.ContainsKey("Fields"))
            {
                IsLoaded = true;

                foreach (var graphObjectKey in nodeDataDict["Fields"].Keys)
                {
                    var graphObject = _GetGraphObject(graphObjectKey);
                    if (graphObject == null)
                        throw new Exception(
                            $"Field key \"{graphObjectKey}\" should never exist on nodes of type \"{Tag}\"");

                    graphObject.SetInitialValue(nodeDataDict["Fields"]![graphObjectKey]!);
                }
            }

            if (nodeDataDict.ContainsKey("Edges"))
            {
                IsLoaded = true;

                foreach (var graphObjectKey in nodeDataDict["Edges"].Keys)
                {
                    var graphObject = _GetGraphObject(graphObjectKey);
                    if (graphObject == null)
                        throw new Exception(
                            $"Edge key \"{graphObjectKey}\" should never exist on nodes of type \"{Tag}\"");

                    var edgeDict = (NestedDictionary?)nodeDataDict["Edges"][graphObjectKey];
                    if (edgeDict != null)
                    {
                        ((Edge<Node, Node>)graphObject).IsLoaded = edgeDict.ContainsKey("Data");

                        graphObject.SetInitialValue(
                            FromTag(
                                (string)edgeDict["Meta"]["Tag"]!,
                                new[]
                                {
                                    edgeDict,
                                    edgeDict["Meta"]["Guid"],
                                    null,
                                    edgeDict["Meta"].ContainsKey("Timestamp") ? edgeDict["Meta"]["Timestamp"] : null
                                }
                            )
                        );
                    }
                }
            }

            if (nodeDataDict.ContainsKey("EdgeCollections"))
            {
                IsLoaded = true;

                foreach (var graphObjectKey in nodeDataDict["EdgeCollections"].Keys)
                {
                    var graphObject = _GetGraphObject(graphObjectKey);
                    if (graphObject == null)
                        throw new Exception(
                            $"Edge collection key \"{graphObjectKey}\" should never exist on nodes of type \"{Tag}\"");

                    if (graphObject is IGraphObjectWithCursor)
                        ((IGraphObjectWithCursor)graphObject).Cursor = _cursor;

                    var edgeNodes = nodeDataDict["EdgeCollections"][graphObjectKey];
                    if (edgeNodes != null) graphObject.SetInitialValue(edgeNodes);
                }
            }
        }

        if (_schema == null) throw new Exception("_schema is null");
    }

    public Node(
        Guid? guid,
        Cursor? cursor,
        NodeRep? nodeRep,
        ulong? timestamp,
        string? tag = null,
        NodeSchema? nodeSchema = null
    )
    {
        Guid = guid ?? Guid.NewGuid();
        IsLoaded = nodeRep != null; // affects whether the cursor registers this node
        Cursor = cursor;
        IsDeletable = nodeSchema?.IsDeletable ?? true;
        Deleted = false;
        _tag = nodeRep?.Meta.Tag ?? tag;
        OnChainId = nodeRep?.Meta.OnChainId;
        SubgraphId = nodeRep?.Meta.SubgraphId;

        if (timestamp != null)
        {
            FromDataStore = true;
            Timestamp = (ulong)timestamp;
        }
        else
        {
            Timestamp = Utils.TimestampMillis();
        }

        if (!_schemalessEnabled || nodeSchema != null)
            _InitSchema(
                nodeSchema
            );

        // Set graph object's initial values
        if (nodeRep != null)
        {
            if (nodeRep.Fields?.BooleanFields != null)
            {
                foreach (var graphObjectKey in nodeRep.Fields.BooleanFields.Keys)
                {
                    var value = nodeRep.Fields.BooleanFields[graphObjectKey]!;
                    var field = new Boolean(this);
                    field.SetInitialValue(value);
                    _fields.Add(graphObjectKey, field);
                }
            }

            if (nodeRep.Fields?.NumericFields != null)
            {
                foreach (var graphObjectKey in nodeRep.Fields.NumericFields.Keys)
                {
                    var value = nodeRep.Fields.NumericFields[graphObjectKey]!;
                    var field = new Decimal(this);
                    field.SetInitialValue(value);
                    _fields.Add(graphObjectKey, field);
                }
            }

            if (nodeRep.Fields?.StringFields != null)
            {
                foreach (var graphObjectKey in nodeRep.Fields.StringFields.Keys)
                {
                    var value = nodeRep.Fields.StringFields[graphObjectKey]!;
                    var field = new String(this);
                    field.SetInitialValue(value);
                    _fields.Add(graphObjectKey, field);
                }
            }

            if (nodeRep.Fields?.BooleanListFields != null)
            {
                foreach (var graphObjectKey in nodeRep.Fields.BooleanListFields.Keys)
                {
                    var value = nodeRep.Fields.BooleanListFields[graphObjectKey]!;
                    var field = new BooleanList(this);
                    field.SetInitialValue(value);
                    _fields.Add(graphObjectKey, field);
                }
            }

            if (nodeRep.Fields?.NumericListFields != null)
            {
                foreach (var graphObjectKey in nodeRep.Fields.NumericListFields.Keys)
                {
                    var value = nodeRep.Fields.NumericListFields[graphObjectKey]!;
                    var field = new NumericList(this);
                    field.SetInitialValue(value);
                    _fields.Add(graphObjectKey, field);
                }
            }

            if (nodeRep.Fields?.StringListFields != null)
            {
                foreach (var graphObjectKey in nodeRep.Fields.StringListFields.Keys)
                {
                    var value = nodeRep.Fields.StringListFields[graphObjectKey]!;
                    var field = new StringList(this);
                    field.SetInitialValue(value);
                    _fields.Add(graphObjectKey, field);
                }
            }

            if (nodeRep.Edges != null)
            {
                foreach (var graphObjectKey in nodeRep.Edges.Values.Keys)
                {
                    var edgeGuid = nodeRep.Edges.Values[graphObjectKey];
                    if (edgeGuid == null)
                    {
                        var edge = new Edge<Node, Node>(this, null);
                        _edges.Add(graphObjectKey, edge);
                    }
                    else
                    {
                        var node = new Node(
                            edgeGuid,
                            cursor,
                            nodeRep: null,
                            timestamp: timestamp,
                            tag: nodeRep.Edges.Tags[graphObjectKey]
                        );
                        var edge = new Edge<Node, Node>(this, node);

                        edge.SetInitialValue(node);
                        _edges.Add(graphObjectKey, edge);
                    }
                }
            }

            if (nodeRep.EdgeCollections != null)
            {
                foreach (var graphObjectKey in nodeRep.EdgeCollections.Values.Keys)
                {
                    var value = nodeRep.EdgeCollections.Values[graphObjectKey];
                    var edgeCollection = new EdgeCollection<Node, Node>(
                        this,
                        nodeRep.EdgeCollections.Tags[graphObjectKey],
                        null
                    );
                    edgeCollection.SetInitialValue(value);
                    _edgeCollections.Add(graphObjectKey, edgeCollection);
                }
            }

            if (_schemalessEnabled && _schema == null)
                _InitSchemaless();
        }
    }

    public static bool SchemalessEnabled
    {
        get => _schemalessEnabled;
    }

    public bool IsDeletable { get; private set; }

    public bool Deleted { get; private set; }

    public Guid Guid { get; }

    public string? OnChainId
    {
        get => _onChainId;
        set { _onChainId = value; }
    }

    public ulong? SubgraphId
    {
        get => _subgraphId;
        set { _subgraphId = value; }
    }

    public bool IsReadonly
    {
        get => _isReadonly;
        set { _isReadonly = value; }
    }

    public ulong Timestamp { get; }

    public Cursor? Cursor
    {
        get => _cursor;
        set
        {
            if (_cursor != null && value != null && _cursor.Guid != value.Guid)
                throw new Exception("Cursor cannot infect a node which already has a cursor.");

            if (_cursor?.Guid == value?.Guid) return;

            _cursor = value;
            _cursor?.AddNode(this);

            foreach (Edge<Node, Node>? edge in _edges.Values)
                if (edge != null)
                    edge.Cursor = value;

            foreach (EdgeCollection<Node, Node>? edgeCollection in _edgeCollections.Values)
                if (edgeCollection != null)
                    edgeCollection.Cursor = value;
        }
    }

    public string Tag => _tag ?? GetType().Name;

    public bool FromDataStore { get; }

    public bool IsLoaded { get; set; }

    /* Constructors and alternatives */
    private void _InitSchema(NodeSchema? nodeSchema)
    {
        var fieldTypes = new Dictionary<string, Type>();
        var edgeTypes = new Dictionary<string, Type>();
        var edgeCollectionTypes = new Dictionary<string, Type>();

        if (nodeSchema == null)
        {
            if (!_registry.ContainsKey(Tag))
            {
                foreach (var prop in GetType().GetFields())
                    if (prop.FieldType.Name.Contains("EdgeCollection"))
                        edgeCollectionTypes.Add(prop.Name, prop.FieldType);
                    else if (prop.FieldType.Name.Contains("Edge"))
                        edgeTypes.Add(prop.Name, prop.FieldType);
                    else
                        fieldTypes.Add(prop.Name, prop.FieldType);
                _schema = new _Schema(Tag, fieldTypes, edgeTypes, edgeCollectionTypes, IsDeletable);
                _registry.Add(Tag, _schema);
            }
            else
            {
                _schema = _registry[Tag];
            }
        }
        else
        {
            // Prevent compiler warnings, need to write this bit (initializing schema from dict)
            _schema = new _Schema(
                Tag,
                nodeSchema.GetFieldTypes(),
                nodeSchema.GetEdgeTypes(),
                nodeSchema.GetEdgeCollectionTypes(),
                IsDeletable
            );

            // TODO: if GetType().Name == "Node"
            // use reflection to generate a new type at runtime

            _registry.Add(Tag, _schema);
        }
    }

    private void _InitSchemaless()
    {
        // use _fields, _edges, and _edgeCollections to initialize schema
        var fieldTypes = new Dictionary<string, Type>();
        var edgeTypes = new Dictionary<string, Type>();
        var edgeCollectionTypes = new Dictionary<string, Type>();

        foreach (var field in _fields)
            if (field.Value != null)
                fieldTypes.Add(field.Key, field.Value.GetType());

        foreach (var edge in _edges)
            if (edge.Value != null)
                edgeTypes.Add(edge.Key, edge.Value.GetType());

        foreach (var edgeCollection in _edgeCollections)
            if (edgeCollection.Value != null)
                edgeCollectionTypes.Add(edgeCollection.Key, edgeCollection.Value.GetType());

        _schema = new _Schema(Tag, fieldTypes, edgeTypes, edgeCollectionTypes, IsDeletable);
        // flag node as schemaless?
    }

    private IGraphObject _CreateSchemalessGraphObject(string graphObjectKey, object? graphObjectValue)
    {
        if (graphObjectValue is bool)
        {
            var field = new Boolean(this);
            _fields.Add(graphObjectKey, field);
            _schema.AddFieldType(graphObjectKey, field.GetType());
            return field;
        }
        else if (graphObjectValue is string)
        {
            var field = new String(this);
            _fields.Add(graphObjectKey, field);
            _schema.AddFieldType(graphObjectKey, field.GetType());
            return field;
        }
        else if (graphObjectValue is decimal || graphObjectValue is int)
        {
            var field = new Decimal(this);
            _fields.Add(graphObjectKey, field);
            _schema.AddFieldType(graphObjectKey, field.GetType());
            return field;
        }
        else if (graphObjectValue is List<bool?>)
        {
            var field = new BooleanList(this);
            _fields.Add(graphObjectKey, field);
            _schema.AddFieldType(graphObjectKey, field.GetType());
            return field;
        }
        else if (graphObjectValue is List<string?>)
        {
            var field = new StringList(this);
            _fields.Add(graphObjectKey, field);
            _schema.AddFieldType(graphObjectKey, field.GetType());
            return field;
        }
        else if (graphObjectValue is List<decimal?>)
        {
            var field = new NumericList(this);
            _fields.Add(graphObjectKey, field);
            _schema.AddFieldType(graphObjectKey, field.GetType());
            return field;
        }
        else if (graphObjectValue is Node)
        {
            var edge = new Edge<Node, Node>(this, (Node)graphObjectValue);
            _edges.Add(graphObjectKey, edge);
            _schema.AddEdgeType(graphObjectKey, edge.GetType());
            return edge;
        }
        else if (graphObjectValue is EdgeCollection<Node, Node>)
        {
            var edgeCollection = (EdgeCollection<Node, Node>)graphObjectValue;
            _edgeCollections.Add(graphObjectKey, edgeCollection);
            _schema.AddEdgeCollectionType(graphObjectKey, edgeCollection.GetType());
            return edgeCollection;
        }
        else
        {
            throw new Exception("Invalid graph object type.");
        }
    }

    private void _InitEmptyGraphObjects()
    {
        if (_schema == null) throw new Exception("_schema is null");

        var fieldTypes = _schema.GetFieldTypes();
        var edgeTypes = _schema.GetEdgeTypes();
        var edgeCollectionTypes = _schema.GetEdgeCollectionTypes();

        // Initialize graph objects instances based on schema definition
        foreach (var fieldName in fieldTypes.Keys)
        {
            Type? fieldType = null;
            fieldTypes.TryGetValue(fieldName, out fieldType);
            if (fieldType != null)
                if (Activator.CreateInstance(fieldType, this) is IGraphObject instance)
                {
                    _fields.Add(fieldName, instance);
                    instance.ValueChanged += _Instance_ValueChanged;
                }
        }

        foreach (var edgeName in edgeTypes.Keys)
        {
            Type? edgeType = null;
            edgeTypes.TryGetValue(edgeName, out edgeType);
            if (edgeType != null)
                if (Activator.CreateInstance(typeof(Edge<Node, Node>), this, null) is IGraphObjectWithCursor instance)
                {
                    _edges.Add(edgeName, instance);
                    instance.ValueChanged += _Instance_ValueChanged;
                }
        }

        foreach (var edgeCollectionName in edgeCollectionTypes.Keys)
        {
            Type? edgeCollectionType = null;
            edgeCollectionTypes.TryGetValue(edgeCollectionName, out edgeCollectionType);
            if (edgeCollectionType != null)
            {
                var toNodeType = edgeCollectionType.GetGenericArguments()[1];
                if (Activator.CreateInstance(typeof(EdgeCollection<Node, Node>), this, toNodeType.Name, new List<Node>()) is
                    IGraphObjectWithCursor instance)
                {
                    _edgeCollections.Add(edgeCollectionName, instance);
                    instance.ValueChanged += _Instance_ValueChanged;
                }
            }
        }
    }

    public async Task<Node?> LoadAsync()
    {
        if (!IsLoaded)
        {
            if (Cursor == null)
                throw new Exception("Cannot load cursorless node.");

            return await Cursor.LazyLoad(Guid);
        }

        return this;
    }

    public async Task<AnyType> DeleteAsync()
    {
        if (!IsDeletable)
            throw new Exception($"Node type is not deletable: {Tag}");

        _PreventMutationIfReadOnly();
        _CheckCursorStateIsNow();
        if (!IsLoaded)
        {
            var loadedNode = await LoadAsync();
            if (loadedNode?.OnChainId == null)
                throw new Exception($"Cannot delete {Tag} node: OnChainId is null");

            OnChainId = loadedNode!.OnChainId;
        }

        Deleted = true;
#pragma warning disable CS8603 // Possible null reference return.
        return await Cursor!.PropagateNodeDeletionAsync(this);
#pragma warning restore CS8603 // Possible null reference return.
    }

    public static SchemaDefinition GetSchemaDefinition()
    {
        var schemaDefinition = new SchemaDefinition();
        foreach (var nodeSchema in _registry)
        {
            var schema = nodeSchema.Value;
            schemaDefinition.AddNodeSchema(new NodeSchema(
                schema.Tag,
                schema.GetFieldTypes(),
                schema.GetEdgeTypes(),
                schema.GetEdgeCollectionTypes(),
                schema.IsDeletable
            ));
        }

        return schemaDefinition;
    }

    public Dictionary<string, Type> GetFieldTypes()
    {
        return _schema?.GetFieldTypes() ?? new Dictionary<string, Type>();
    }

    public Dictionary<string, Type> GetEdgeTypes()
    {
        return _schema?.GetEdgeTypes() ?? new Dictionary<string, Type>();
    }

    public Dictionary<string, Type> GetEdgeCollectionTypes()
    {
        return _schema?.GetEdgeCollectionTypes() ?? new Dictionary<string, Type>();
    }

    public static Node FromTag(string tag, object?[]? args = null)
    {
        var tagType = Utils.GetDerivedType(typeof(Node), tag);

        if (tagType is Type)
        {
            var newNode = Activator.CreateInstance(tagType, args);

            if (newNode is Node) return (Node)newNode;
        }
        else if (Node.SchemalessEnabled)
            return new Node(
                null,
                null,
                new NodeRep(),
                Utils.TimestampMillis(),
                tag
            );

        throw new ArgumentException($"\"{tag}\" is not a valid Node tag.");
    }

    public static Node FromDictionary(NestedDictionary nodeDictionary, bool fromDataStore = true)
    {
        if ((bool)nodeDictionary["Meta"]["Deleted"]!)
            throw new InvalidOperationException("Attempted to instantiate a deleted node.");

        var nodeData = new NestedDictionary
        {
            { NodeEnum.Fields.ToString(), new FlatDictionary() },
            { NodeEnum.Edges.ToString(), new FlatDictionary() },
            { NodeEnum.EdgeCollections.ToString(), new FlatDictionary() }
        };

        if (nodeDictionary.ContainsKey("Data"))
        {
            if (nodeDictionary["Data"]!.ContainsKey("Fields"))
                nodeData["Fields"] = (FlatDictionary)nodeDictionary["Data"]!["Fields"]!;

            if (nodeDictionary["Data"]!.ContainsKey("Edges"))
                nodeData["Edges"] = (FlatDictionary)nodeDictionary["Data"]!["Edges"]!;

            if (nodeDictionary["Data"]!.ContainsKey("EdgeCollections"))
                nodeData["EdgeCollections"] = (FlatDictionary)nodeDictionary["Data"]!["EdgeCollections"]!;
        }
        else
        {
            throw new Exception("No data provided for FromDictionary");
        }

        ulong? timestamp = fromDataStore ? (ulong)nodeDictionary["Meta"]["Timestamp"]! : null;

        var tag = (string)nodeDictionary["Meta"]["Tag"]!;
        var derivedType = Utils.GetDerivedType(typeof(Node), tag);

        return _schemalessEnabled && derivedType == null
            ? new Node(
                (Guid)nodeDictionary["Meta"]["Guid"]!,
                null,
                RepFromDict(nodeDictionary),
                timestamp,
                null
            )
            : FromTag(
                tag,
                new[]
                {
                    nodeData,
                    nodeDictionary["Meta"]["Guid"],
                    null,
                    timestamp
                }
            );
    }

    public static Node FromRep(NodeRep nodeRep)
    {
        return new Node(
            guid: null,
            cursor: null,
            nodeRep: nodeRep,
            timestamp: null
        );
    }

    public static NodeRep RepFromDict(NestedDictionary nodeDict)
    {
        var meta = (FlatDictionary)nodeDict["Meta"]!;
        var data = (FlatDictionary)nodeDict["Data"]!;

        var nodeRep = new NodeRep();
        nodeRep.Meta.Tag = (string)meta["Tag"]!;

        foreach (var field in (FlatDictionary)data[NodeEnum.Fields.ToString()]!)
        {
            if (field.Value is bool)
                nodeRep.Fields.BooleanFields.Add(field.Key, (bool)field.Value);
            else if (field.Value is string)
                nodeRep.Fields.StringFields.Add(field.Key, (string?)field.Value);
            else if (field.Value is decimal || field.Value is int)
                nodeRep.Fields.NumericFields.Add(field.Key, decimal.Parse(field.Value.ToString()!));
            else if (field.Value is List<bool?>)
                nodeRep.Fields.BooleanListFields.Add(field.Key, (List<bool?>)field.Value);
            else if (field.Value is List<decimal?>)
                nodeRep.Fields.NumericListFields.Add(field.Key, (List<decimal?>)field.Value);
            else if (field.Value is List<string?>)
                nodeRep.Fields.StringListFields.Add(field.Key, (List<string?>)field.Value);
        }

        foreach (var edge in (FlatDictionary)data[NodeEnum.Edges.ToString()]!)
        {
            if (edge.Value is null) continue;

            var edgeMeta = ((NestedDictionary)edge.Value!)["Meta"]!;
            nodeRep.Edges.Tags.Add(edge.Key, (string)edgeMeta["Tag"]!);
            nodeRep.Edges.Values.Add(edge.Key, (Guid)edgeMeta["Guid"]!);
        }

        foreach (var edgeCollection in (FlatDictionary)data[NodeEnum.EdgeCollections.ToString()]!)
        {
            nodeRep.EdgeCollections.Tags.Add(edgeCollection.Key, "Node");
            //(string)((NestedDictionary)edgeCollection.Value!)["Meta"]!["Tag"]!);

            var valueToSet = new List<Guid>();
            if (edgeCollection.Value != null)
            {
                if (edgeCollection.Value is List<Guid>)
                    valueToSet = (List<Guid>)edgeCollection.Value!;
                else if (edgeCollection.Value is Dictionary<Guid, Guid?>)
                    valueToSet = ((Dictionary<Guid, Guid?>)edgeCollection.Value!).Keys.ToList();
                else
                    throw new Exception("Invalid edge collection value type.");
            }

            nodeRep.EdgeCollections.Values.Add(edgeCollection.Key, valueToSet);
        }

        return nodeRep;
    }

    /* IO */
    public async Task<NestedDictionary?> AsDictionaryAsync(
        ModeEnum mode = ModeEnum.Data,
        bool silent = false,
        bool includeOnChainIds = false
    )
    {
        var dict = await GetMetaDictionary();
        var fields = new FlatDictionary();
        var edges = new FlatDictionary();
        var edgeCollections = new FlatDictionary();
        var edgeCollectionOnChainIds = new FlatDictionary();
        var lookup = new Dictionary<string, List<string>>();
        lookup.Add(NodeEnum.Fields.ToString(), new List<string>());
        lookup.Add(NodeEnum.Edges.ToString(), new List<string>());
        lookup.Add(NodeEnum.EdgeCollections.ToString(), new List<string>());

        // Get values in parallel
        var tasks = new Dictionary<string, Task<object?>>();
        foreach (var field in _fields)
        {
            tasks[field.Key] = GetValueAsync(field.Key, mode, silent);
            lookup[NodeEnum.Fields.ToString()]!.Add(field.Key);
        }

        foreach (var edge in _edges)
        {
            tasks[edge.Key] = GetValueAsync(edge.Key, mode, silent);
            lookup[NodeEnum.Edges.ToString()]!.Add(edge.Key);
        }

        foreach (var edgeCollection in _edgeCollections)
        {
            tasks[edgeCollection.Key] = GetValueAsync(edgeCollection.Key, mode, silent);
            lookup[NodeEnum.EdgeCollections.ToString()]!.Add(edgeCollection.Key);
        }

        await Task.WhenAll(tasks.Values.ToArray());

        var edgeCollectionTasks = new Dictionary<string, Task<object?>>();
        // Skip any Ignorable values
        foreach (var item in tasks)
        {
            if (item.Value.Result is Ignorable) continue;

            if (lookup[NodeEnum.Fields.ToString()]!.Contains(item.Key))
            {
                fields[item.Key] = item.Value.Result;
            }
            else if (lookup[NodeEnum.Edges.ToString()]!.Contains(item.Key))
            {
                var edgeNode = (Node?)item.Value.Result;
                edges[item.Key] = edgeNode != null
                    ? await edgeNode.GetMetaDictionary(includeOnChainIds)
                    : null;
            }
            else if (lookup[NodeEnum.EdgeCollections.ToString()]!.Contains(item.Key))
            {
                var edgeCollection = (EdgeCollection<Node, Node>?)item.Value.Result;

                var result = edgeCollection?.AsDictionary(mode, silent);
                if (result is Ignorable) continue;
                edgeCollections[item.Key] = result;

                if (includeOnChainIds)
                {
                    if (edgeCollection != null)
                        edgeCollectionOnChainIds[item.Key] = (await edgeCollection.GetNodesAsync(
                            ((Dictionary<Guid, Guid?>)edgeCollections[item.Key]!).Keys.ToList()
                        )).Select(x => x.OnChainId).Where(x => x != null).ToList();
                    else
                        edgeCollectionOnChainIds[item.Key] = new List<ulong>();
                }
            }
        }

        if (mode == ModeEnum.Delta && !Deleted && !fields.Any() && !edges.Any() && !edgeCollections.Any())
            return null; // node has no delta

        dict[mode.ToString()] = new FlatDictionary
        {
            { NodeEnum.Fields.ToString(), fields },
            { NodeEnum.Edges.ToString(), edges },
            { NodeEnum.EdgeCollections.ToString(), edgeCollections }
        };

        if (includeOnChainIds)
            dict[mode.ToString()]["EdgeCollectionOnChainIds"] = edgeCollectionOnChainIds;

        return dict;
    }

    public NodeRep AsNodeRep(
        ulong? subgraphId = null,
        bool includeOnChainIds = false,
        Dictionary<Guid, string?>? onChainIds = null
    )
    {
        var nodeRepDict = AsDictionaryAsync(includeOnChainIds: includeOnChainIds).Result!;
        var nodeValuesDict = nodeRepDict["Data"]!;
        var fieldsDict = (FlatDictionary)nodeValuesDict["Fields"]!;
        var edgeDict = (FlatDictionary)nodeValuesDict["Edges"]!;
        var edgeCollectionsDict = (FlatDictionary)nodeValuesDict["EdgeCollections"]!;
        var edgeCollectionsOnChainDict = includeOnChainIds
            ? (FlatDictionary)nodeValuesDict["EdgeCollectionOnChainIds"]!
            : new FlatDictionary();

        var fieldTypes = GetFieldTypes();
        var edgeTypes = GetEdgeTypes();
        var edgeCollectionTypes = GetEdgeCollectionTypes();

        var metaDict = nodeRepDict["Meta"]!;

        var nodeRep = new NodeRep();

        nodeRep.Meta.Guid = Guid;
        nodeRep.Deleted = Deleted;
        nodeRep.Meta.Tag = (string)metaDict["Tag"]!;
        nodeRep.Meta.OnChainId = OnChainId;
        nodeRep.Meta.SubgraphId = SubgraphId ?? subgraphId;
        nodeRep.Fields.BooleanFields = fieldsDict.Where(x => fieldTypes[x.Key].Name == "Boolean")
            .ToDictionary(
                x => x.Key,
                x => (bool?)x.Value
            );
        nodeRep.Fields.StringFields = fieldsDict.Where(x => fieldTypes[x.Key].Name == "String")
            .ToDictionary(
                x => x.Key,
                x => (string?)x.Value
            );
        nodeRep.Fields.NumericFields = fieldsDict.Where(x => fieldTypes[x.Key].Name == "Decimal")
            .ToDictionary(
                x => x.Key,
                x => (decimal?)x.Value
            );
        nodeRep.Fields.BooleanListFields = fieldsDict.Where(x => fieldTypes[x.Key].Name == "BooleanList")
            .ToDictionary(
                x => x.Key,
                x => (List<bool?>?)x.Value
            );
        nodeRep.Fields.StringListFields = fieldsDict.Where(x => fieldTypes[x.Key].Name == "StringList")
            .ToDictionary(
                x => x.Key,
                x => (List<string?>?)x.Value
            );
        nodeRep.Fields.NumericListFields = fieldsDict.Where(x => fieldTypes[x.Key].Name == "NumericList")
            .ToDictionary(
                x => x.Key,
                x => (List<decimal?>?)x.Value
            );

        nodeRep.Edges.Tags = edgeDict.ToDictionary(
            x => x.Key,
            x => (string)((NestedDictionary?)x.Value)?["Meta"]["Tag"]! ?? ""
        );
        nodeRep.Edges.Values = edgeDict.ToDictionary(
            x => x.Key,
            x => (Guid?)((NestedDictionary?)x.Value)?["Meta"]["Guid"] ?? null
        );
        if (includeOnChainIds)
        {
            if (onChainIds != null)
                nodeRep.Edges.OnChainValues = edgeDict.ToDictionary(
                    x => x.Key,
                    x => x.Value != null
                        ? onChainIds[(Guid)((NestedDictionary)x.Value!)["Meta"]["Guid"]!]
                        : null
                );
            else
                nodeRep.Edges.OnChainValues = edgeDict.ToDictionary(
                    x => x.Key,
                    x => (string?)((NestedDictionary?)x.Value)?["Meta"]["OnChainId"]
                );
        }
        nodeRep.EdgeCollections.Tags = edgeCollectionsDict.ToDictionary(
            x => x.Key,
            x => ((EdgeCollection<Node, Node>?)_edgeCollections[x.Key])?.Tag ?? ""
        );
        nodeRep.EdgeCollections.Values = edgeCollectionsDict.ToDictionary(
            x => x.Key,
            x => ((Dictionary<Guid, Guid?>)x.Value!).Keys.ToList()
        );
        if (includeOnChainIds)
        {
            foreach (var item in edgeCollectionsOnChainDict)
            {
                var list = new List<string>();
                if (item.Value != null)
                    foreach (var onChainId in (List<string?>)item.Value)
                        list.Add(onChainId!); // actual values are not nullable

                nodeRep.EdgeCollections.OnChainValues.Add(item.Key, list);
            }
        }
        return nodeRep;
    }

    public async Task<NestedDictionary> GetMetaDictionary(bool includeOnChainIds = false)
    {
        var metaDict = new FlatDictionary
        {
            { "Guid", Guid },
            { "Tag", Tag },
            { "Deleted", Deleted }
        };

        if (includeOnChainIds)
        {
            if (!IsLoaded)
                OnChainId = (await LoadAsync())?.OnChainId;

            metaDict.Add("OnChainId", OnChainId);
        }

        return new NestedDictionary
        {
            { "Meta", metaDict }
        };
    }

    public async Task PropagateDeletionOfReferencedNodeAsync(Node deletedNode)
    {
        var affectedByPropogation = false;

        var edges = new Dictionary<string, Task<object?>>();
        foreach (var edge in _edges)
            edges.Add(edge.Key, GetValueAsync(edge.Key, ModeEnum.Either));

        var edgeCollections = new List<Task<object?>>();
        foreach (var edgeCollection in _edgeCollections)
            edgeCollections.Add(GetValueAsync(edgeCollection.Key));

        var tasks = new List<Task>();

        await Task.WhenAll(edges.Values);
        foreach (var item in edges)
        {
            if (item.Value.Result == null) continue;

            var node = (Node)item.Value.Result;
            if (node.Guid != deletedNode.Guid) continue;

            if (Cursor == null) Cursor = deletedNode.Cursor;
            tasks.Add(SetValueAsync(item.Key, null));
            affectedByPropogation = true;
        }

        foreach (EdgeCollection<Node, Node>? edgeCollection in edgeCollections.Select(x => x.Result))
        {
            if (edgeCollection == null || !edgeCollection.Contains(deletedNode.Guid)) continue;

            if (Cursor == null) Cursor = deletedNode.Cursor;
            edgeCollection.Remove(deletedNode.Guid);
            affectedByPropogation = true;
        }

        await Task.WhenAll(tasks);
        if (affectedByPropogation) await SaveAsync();
    }

    /* Getters & Setters */
    public async Task SetValueAsync(string graphObjectKey, object? graphObjectValue)
    {
        _PreventMutationIfReadOnly();
        _CheckCursorStateIsNow();

        if (Deleted)
            throw new InvalidOperationException("Attempted to modify a deleted node.");

        if (!IsLoaded)
            throw new InvalidOperationException("Attempted to modify a node that is not loaded.");

        var graphObject = _GetGraphObject(graphObjectKey);
        if (graphObject == null)
        {
            if (_schema.ContainsGraphObject(graphObjectKey))
            {
                var lookup = _schema.LookupEnum(graphObjectKey);
                var graphObjectType = _schema.GetGraphObjectType(graphObjectKey);

                // TODO: perform type checking on graphObjectValue to see if it can be casted
                if (lookup != null && graphObjectType != null)
                {
                    if (lookup == NodeEnum.Fields)
                    {
                        _fields.Add(graphObjectKey, (Scalar<object>?)graphObjectValue);
                    }
                    else if (lookup == NodeEnum.Edges)
                    {
                        var graphObjectTypedValue = (Node?)graphObjectValue;

                        var edge = (IGraphObjectWithCursor?)new Edge<Node, Node>(this, graphObjectTypedValue);

                        if (edge != null)
                        {
                            edge.Cursor = _cursor;
                            _edges.Add(graphObjectKey, edge);
                        }
                        else
                        {
                            throw new Exception("Unable to instantiate edge");
                        }
                    }
                    else if (lookup == NodeEnum.EdgeCollections)
                    {
                        var graphObjectTypedValue = (List<Node>?)graphObjectValue;

                        var edgeCollection =
                            (IGraphObjectWithCursor?)new EdgeCollection<Node, Node>
                            (
                                this,
                                GetEdgeCollectionTypes()[graphObjectKey].GetGenericArguments()[1].Name,
                                graphObjectTypedValue
                            );

                        if (edgeCollection != null)
                        {
                            edgeCollection.Cursor = _cursor;
                            _edgeCollections.Add(graphObjectKey, edgeCollection);
                        }
                        else
                        {
                            throw new Exception("Unable to instantiate edge collection");
                        }
                    }
                }
            }
            else if (_schemalessEnabled && !_schema.ContainsGraphObject(graphObjectKey))
            {
                graphObject = _CreateSchemalessGraphObject(graphObjectKey, graphObjectValue);

                if (graphObject is Decimal && Utils.IsNumericType(graphObjectValue))
                    await graphObject.SetValueAsync(graphObjectValue != null
                        ? decimal.Parse(graphObjectValue.ToString()!)
                        : null
                    );
                else
                    await graphObject.SetValueAsync(graphObjectValue);
            }
        }
        else
        {
            if (graphObject is Decimal && Utils.IsNumericType(graphObjectValue))
                await graphObject.SetValueAsync(graphObjectValue != null
                    ? decimal.Parse(graphObjectValue.ToString()!)
                    : null
                );
            else
                await graphObject.SetValueAsync(graphObjectValue);
        }
    }

    public async Task<object?> GetValueAsync(string graphObjectKey, ModeEnum mode = ModeEnum.Data, bool silent = false)
    {
        if (!IsLoaded)
            return (await LoadAsync())?.GetValueAsync(graphObjectKey, mode, silent).Result;

        var graphObject = _GetGraphObject(graphObjectKey);
        if (graphObject == null) return null;

        return await graphObject.GetValueAsync(mode, silent);
    }

    /* State modulators */
    public async Task DeltaToDataAsync()
    {
        foreach (var field in _fields.Values)
            if (field != null)
                await field.DeltaToDataAsync();
        foreach (Edge<Node, Node>? edge in _edges.Values)
            if (edge != null)
                await edge.DeltaToDataAsync();
        foreach (EdgeCollection<Node, Node>? edgeCollection in _edgeCollections.Values)
            if (edgeCollection != null)
                await edgeCollection.DeltaToDataAsync();
    }

    public async Task ClearDeltaAsync()
    {
        foreach (Scalar<object>? field in _fields.Values)
            if (field != null)
                await field.DeltaToDataAsync();
    }

    public async Task SaveAsync(ModeEnum mode = ModeEnum.Delta)
    {
        _PreventMutationIfReadOnly();

        if (Cursor == null)
            throw new InvalidOperationException("Cannot save cursorless node.");

        await Cursor.SaveAsync(this, mode);
    }

    /* All other internals */
    private void _Instance_ValueChanged(object? sender, EventArgs e)
    {
        if (sender == null)
            return;

        /* if (_fields.TryGetValue(sender, out FieldInfo? fieldInfo))
        {
            var name = fieldInfo.Name;
        } */
    }

    private void _PreventMutationIfReadOnly()
    {
        if (IsReadonly)
            throw new InvalidOperationException("Attempted to save or modify a readonly node.");
    }

    private void _CheckCursorStateIsNow()
    {
        if (Cursor == null)
            throw new InvalidOperationException("Node has no cursor (cursor state must be Live). Node tag: " + Tag);

        if (Cursor.State != CursorStateEnum.Live)
            throw new InvalidOperationException("Cursor state is not Live.");
    }

    private IGraphObject? _GetGraphObject(string graphObjectKey)
    {
        if (_fields.ContainsKey(graphObjectKey))
            return _fields[graphObjectKey];
        if (_edges.ContainsKey(graphObjectKey))
            return _edges[graphObjectKey];
        if (_edgeCollections.ContainsKey(graphObjectKey))
            return _edgeCollections[graphObjectKey];

        return null;
    }

    private sealed class _Schema
    {
        private readonly Dictionary<string, Dictionary<string, Type>> _cache = new();
        private readonly Dictionary<string, Type> _edgeCollectionTypes = new();

        private readonly Dictionary<string, Type> _edgeTypes = new();

        // TODO: figure out to further specify Type as a subclass of Node
        private readonly Dictionary<string, Type> _fieldTypes = new();

        private readonly string _tag;

        private readonly Dictionary<string, NodeEnum> _lookup = new();

        private readonly bool _isDeletable;

        public _Schema(
            string tag,
            Dictionary<string, Type> fieldTypes,
            Dictionary<string, Type> edgeTypes,
            Dictionary<string, Type> edgeCollectionTypes,
            bool isDeletable
        )
        {
            _tag = tag;
            _fieldTypes = fieldTypes;
            _edgeTypes = edgeTypes;
            _edgeCollectionTypes = edgeCollectionTypes;
            _isDeletable = isDeletable;

            // Assign keys to cache for quick lookup
            foreach (var item in _fieldTypes)
            {
                _cache.Add(item.Key, _fieldTypes);
                _lookup.Add(item.Key, NodeEnum.Fields);
            }

            foreach (var item in _edgeTypes)
            {
                _cache.Add(item.Key, _edgeTypes);
                _lookup.Add(item.Key, NodeEnum.Edges);
            }

            foreach (var item in _edgeCollectionTypes)
            {
                _cache.Add(item.Key, _edgeCollectionTypes);
                _lookup.Add(item.Key, NodeEnum.EdgeCollections);
            }
        }

        public string Tag => _tag;
        public bool IsDeletable => _isDeletable;

        public bool ContainsGraphObject(string key)
        {
            return _cache.ContainsKey(key);
        }

        public Dictionary<string, Type> GetFieldTypes()
        {
            return _fieldTypes;
        }

        public Dictionary<string, Type> GetEdgeTypes()
        {
            return _edgeTypes;
        }

        public Dictionary<string, Type> GetEdgeCollectionTypes()
        {
            return _edgeCollectionTypes;
        }

        public Type? GetGraphObjectType(string key)
        {
            if (_cache.ContainsKey(key)) return _cache[key][key];
            return null;
        }

        public List<string> GetAllKeys()
        {
            return _cache.Keys.Select(key => key.ToString()).ToList();
        }

        public NodeEnum? LookupEnum(string key)
        {
            NodeEnum lookupValue;
            var exists = _lookup.TryGetValue(key, out lookupValue);
            return exists ? lookupValue : null;
        }

        public string? LookupString(string key)
        {
            NodeEnum lookupValue;
            var exists = _lookup.TryGetValue(key, out lookupValue);

            if (lookupValue == NodeEnum.Fields)
                return "Fields";
            if (lookupValue == NodeEnum.Edges)
                return "Edges";
            if (lookupValue == NodeEnum.EdgeCollections) return "EdgeCollections";
            return null;
        }

        public void AddFieldType(string key, Type type)
        {
            if (!_schemalessEnabled)
                throw new Exception("Cannot add field type: schemaless mode is disabled.");

            _fieldTypes.Add(key, type);
            _cache.Add(key, _fieldTypes);
            _lookup.Add(key, NodeEnum.Fields);
        }

        public void AddEdgeType(string key, Type type)
        {
            if (!_schemalessEnabled)
                throw new Exception("Cannot add edge type: schemaless mode is disabled.");

            _edgeTypes.Add(key, type);
            _cache.Add(key, _edgeTypes);
            _lookup.Add(key, NodeEnum.Edges);
        }

        public void AddEdgeCollectionType(string key, Type type)
        {
            if (!_schemalessEnabled)
                throw new Exception("Cannot add edge collection type: schemaless mode is disabled.");

            _edgeCollectionTypes.Add(key, type);
            _cache.Add(key, _edgeCollectionTypes);
            _lookup.Add(key, NodeEnum.EdgeCollections);
        }
    }
}

public class NodeEventArgs : EventArgs
{
    public object? Value { get; set; }
}