namespace Graph.Api;

public class OperationInput
{
    public List<Delete>? Deletions { get; set; }

    public List<Create>? Creations { get; set; }

    public List<SpawnThread>? ThreadsToSpawn { get; set; }

    public List<Update>? Updates { get; set; }

    public List<SetEdge>? EdgesToSet { get; set; }

    public List<UnsetEdge>? EdgesToUnset { get; set; }

    public List<AddToEdgeCollection>? EdgeCollectionAdditions { get; set; }

    public List<RemoveFromEdgeCollection>? EdgeCollectionRemovals { get; set; }

    public List<IOperation> GetOperations(out HashSet<Guid> providedNodeGuids)
    {
        var operations = new List<IOperation>();
        providedNodeGuids = new HashSet<Guid>();

        // The order of operations is important!
        // Deletions and Creations must be executed first.
        var operationTypeNames = new List<string>
        {
            "Deletions",
            "Creations",
            "ThreadsToSpawn",
            "Updates",
            "EdgesToSet",
            "EdgesToUnset",
            "EdgeCollectionAdditions",
            "EdgeCollectionRemovals"
        };

        var operationInputType = GetType();
        foreach (var operationTypeName in operationTypeNames)
        {
            var prop = operationInputType.GetProperty(operationTypeName);
            var operationTypeList = prop?.GetValue(this);

            if (operationTypeList == null) continue;

            foreach (var item in (IEnumerable<IOperation>)operationTypeList)
            {
                var operation = item;
                operations.Add(operation);

                if (operation is IOperationWithNodeGuid)
                    providedNodeGuids.Add(((IOperationWithNodeGuid)operation).NodeId);

                if (operation is IGraphRelationshipOperation)
                    providedNodeGuids.UnionWith(
                        ((IGraphRelationshipOperation)operation).GetProvidedNodeGuids()
                    );

                // We will be checking whether any of the provided node Guids are already in the graph.
                if (operation is Create)
                {
                    var creation = (Create)operation;
                    if (creation.Id != null)
                        providedNodeGuids.Add((Guid)creation.Id);
                }
            }
        }

        return operations;
    }

    public List<Guid>? GetDeletedNodeGuids()
    {
        return Deletions?.Select(d => d.NodeId).ToList();
    }
}

public class OperationInputType : InputObjectType<OperationInput>
{
}