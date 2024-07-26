using Graph.Core;

namespace Graph.Api;

public class UnsetEdge : IGraphRelationshipOperation
{
    public Guid ActionId { get; set; }

    public string Key { get; set; }

    public Guid FromNodeId { get; set; }

    public Guid ToNodeId { get; set; }

    public List<Guid> GetProvidedNodeGuids()
    {
        return new List<Guid> { FromNodeId, ToNodeId };
    }

    public async Task Execute(Cursor cursor)
    {
        try
        {
            if (!cursor.NodeGuids.ContainsKey(FromNodeId))
                throw new GraphQLException("Node id does not exist: " + FromNodeId);

            var fromNode = cursor.NodeGuids[FromNodeId];
            var toNode = (Core.Node?)await fromNode.GetValueAsync(Key);

            if (toNode == null)
                throw new GraphQLException($"Cannot unset null edge key '{Key}' from node id '{FromNodeId}'");

            if (toNode.Guid != ToNodeId)
                throw new GraphQLException(
                    $"The provided node id '{ToNodeId}' does not match the current node id '{toNode.Guid}' for the edge key '{Key}' from node id '{FromNodeId}'");

            await Task.WhenAll(new List<Task>
            {
                fromNode.SetValueAsync(Key, null),
                fromNode.SaveAsync()
            });
        }
        catch (Exception ex)
        {
            throw new GraphQLException(ex.Message);
        }
    }
}

public class UnsetEdgeType : ObjectType<UnsetEdge>
{
    protected override void Configure(
        IObjectTypeDescriptor<UnsetEdge> descriptor)
    {
        descriptor.Name("UnsetEdge")
            .Implements<GraphRelationshipOperationType>();
    }
}