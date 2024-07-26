using Graph.Core;

namespace Graph.Api;

public class Delete : IOperationWithNodeGuid
{
    public Guid ActionId { get; set; }

    public Guid NodeId { get; set; }

    public async Task Execute(Cursor cursor)
    {
        if (!cursor.NodeGuids.ContainsKey(NodeId))
            throw new GraphQLException("Node id does not exist: " + NodeId);

        var node = cursor.NodeGuids[NodeId];
        if (node.Deleted) return;

        await node.DeleteAsync();
    }
}

public class DeleteType : ObjectType<Delete>
{
    protected override void Configure(
        IObjectTypeDescriptor<Delete> descriptor)
    {
        descriptor.Name("Delete");
        descriptor.Implements<OperationWithNodeGuidType>();
    }
}