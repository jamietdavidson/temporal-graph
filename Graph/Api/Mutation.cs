using Graph.Core;
using Graph.Mongo;
using Graph.Templates;

namespace Graph.Api;

public class Mutation
{
    private bool _useMongo = true;

    public async Task<Oracle> UpdateGraph(Guid stateMapId, Guid partyId, OperationInput input)
    {
        var cursor = new Cursor(new Session(
            new MongoDataStore()
        ));
        if (cursor.State != CursorStateEnum.Live)
            throw new GraphQLException("Cannot update graph when in rewind state.");

        StateMapExecutor? stateMap = null;
        if (_useMongo)
        {
            // Delete operations are not allowed
            // until deletion actions are added to the schema spec.
            if (input.Deletions?.Count > 0)
                throw new GraphQLException("Deletions are not supported.");

            stateMap = await StateMapExecutor.LoadAsync(stateMapId, partyId, cursor);
        }

        ModeEnum mode = ModeEnum.Data;
        List<Guid>? createdNodeGuids = null;
        if (input.Creations?.Count > 0)
        {
            mode = ModeEnum.Data;
            createdNodeGuids = new List<Guid>();
        }

        var operations = input.GetOperations(out HashSet<Guid> includedNodeGuids);
        if (operations.Count == 0)
            throw new GraphQLException("No operations were provided.");

        var deletedNodeGuids = input.GetDeletedNodeGuids();

        await cursor.SearchAsync(new Core.Query().Include(includedNodeGuids.ToList()));

        var graphOperation = async Task () =>
        {
            // If creating any nodes, we must instantiate them before executing any operations.
            if (createdNodeGuids != null)
                foreach (var creation in input.Creations!)
                {
                    if (creation.Id != null && cursor.NodeGuids.ContainsKey((Guid)creation.Id))
                        throw new GraphQLException($"Cannot create node with id '{creation.Id}' because it already exists in the graph.");

                    if (_useMongo)
                    {
                        operations.AddRange(await stateMap!.SetDefaultValuesAsync(
                            creation,
                            input.EdgesToSet
                        ));
                    }

                    var createdNodeGuid = creation.GetInstance(cursor).Guid;
                    createdNodeGuids.Add(createdNodeGuid);
                    includedNodeGuids.Add(createdNodeGuid);
                }

            var tasks = new List<Task>();
            var actionOperations = new Dictionary<Guid, List<IOperation>>();
            foreach (var operation in operations)
            {
                // Now that newly created nodes have been instantiated,
                // make sure that the relevant Guids are set before the operation is executed.
                if (operation is IGuidSetter)
                    ((IGuidSetter)operation).SetGuids(input.Creations);

                tasks.Add(operation.Execute(cursor));

                // Group operations by action id
                if (!actionOperations.ContainsKey(operation.ActionId))
                    actionOperations.Add(operation.ActionId, new List<IOperation>());

                actionOperations[operation.ActionId].Add(operation);
            }

            await Task.WhenAll(tasks);

            if (_useMongo)
            {
                await stateMap!.ValidateActionsAsync(partyId, actionOperations);
                await stateMap.UpdateStateAsync(actionOperations);
            }
        };

        try
        {
            await cursor.TogetherAsync(mode, operation: graphOperation);
            await cursor.NowAsync(includedNodeGuids.Except(deletedNodeGuids ?? new List<Guid>()).ToList());

            return new Oracle(cursor, null, createdNodeGuids, deletedNodeGuids);
        }
        catch (Exception ex)
        {
            throw new GraphQLException(ex.Message);
        }
    }

    public async Task<StateMapKeySet> NewStateMap(string oisSchemaPath)
    {
        var ds = new MongoDataStore();
        var cursor = new Cursor(new Session(ds));
        var templateGuid = await ds.GetOisSchemaAsync(oisSchemaPath);

        if (templateGuid == null)
        {
            try
            {
                templateGuid = await Templates.Utils.SaveTemplateAsync(oisSchemaPath, cursor);
            }
            catch
            {
                throw new GraphQLException("Invalid oisSchemaPath");
            }
        }

        var stateMap = new StateMapExecutor((Guid)templateGuid);
        await stateMap.FromTemplateAsync((Guid)templateGuid, cursor);

        return new StateMapKeySet(stateMap);
    }
}

public class MutationType : ObjectType<Mutation>
{
    protected override void Configure(
        IObjectTypeDescriptor<Mutation> descriptor)
    {
        descriptor.Name(OperationTypeNames.Mutation);

        descriptor.Field("updateGraph")
            .Type<OracleType>()
            .Argument("stateMapId", a => a.Type<NonNullType<UuidType>>())
            .Argument("partyId", a => a.Type<NonNullType<UuidType>>())
            .Argument("operations", a => a.Type<OperationInputType>())
            .Resolve(async context =>
            {
                return await new Mutation().UpdateGraph(
                    context.ArgumentValue<Guid>("stateMapId"),
                    context.ArgumentValue<Guid>("partyId"),
                    context.ArgumentValue<OperationInput>("operations")
                );
            });
    }
}