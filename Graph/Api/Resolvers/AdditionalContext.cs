namespace Graph.Api;

public class AdditionalContext
{
    public List<Node>? CreatedNodes { get; set; }

    public List<Guid>? DeletedNodeIds { get; set; }

    public AdditionalContext(Core.Cursor cursor, List<Guid>? createdNodeGuids, List<Guid>? deletedNodeGuids)
    {
        CreatedNodes = createdNodeGuids?.Select(g => new Node(cursor.NodeGuids[g])).ToList();
        DeletedNodeIds = deletedNodeGuids;
    }
}

public class AdditionalContextType : ObjectType<AdditionalContext>
{
}