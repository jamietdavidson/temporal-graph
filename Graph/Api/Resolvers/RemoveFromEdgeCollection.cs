using Graph.Core;

namespace Graph.Api;

public class RemoveFromEdgeCollection : IGraphRelationshipOperation
{
    public Guid ActionId { get; set; }

    public string Key { get; set; }

    public Guid FromNodeId { get; set; }

    public List<Guid> ToNodeIds { get; set; }

    public List<Guid> GetProvidedNodeGuids()
    {
        return new List<Guid> { FromNodeId }.Concat(ToNodeIds).ToList();
    }

    public async Task Execute(Cursor cursor)
    {
        try
        {
            if (ToNodeIds.Count == 0)
                throw new GraphQLException("No toNodeIds provided for RemoveFromEdgeCollectionInput.");

            if (!cursor.NodeGuids.ContainsKey(FromNodeId))
                throw new GraphQLException("Node id does not exist: " + FromNodeId);

            var fromNode = cursor.NodeGuids[FromNodeId];

            var edgeCollection =
                (EdgeCollection<Core.Node, Core.Node>?)await fromNode.GetValueAsync(Key);

            if (edgeCollection == null)
                throw new GraphQLException(
                    $"Edge collection key '{Key}' does not exist on node id '{FromNodeId}'");

            edgeCollection.Remove(ToNodeIds);
            await fromNode.SaveAsync();
        }
        catch (Exception ex)
        {
            throw new GraphQLException(ex.Message);
        }
    }
}

public class RemoveFromEdgeCollectionType : ObjectType<RemoveFromEdgeCollection>
{
    protected override void Configure(
        IObjectTypeDescriptor<RemoveFromEdgeCollection> descriptor)
    {
        descriptor.Name("RemoveFromEdgeCollection")
            .Implements<GraphRelationshipOperationType>();
    }
}