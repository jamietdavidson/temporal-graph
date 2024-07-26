using Graph.Core;

namespace Graph.Api;

public class SetEdge : MutuallyExclusiveGuidResolver, IGraphRelationshipOperation
{
    private bool _guidsAreSet = false;

    public Guid ActionId { get; set; }

    public string Key { get; set; }

    [GraphQLDescription("The id of an existing node. This field and 'fromCreatedNodeIndex' are mutually exclusive.")]
    public Guid? FromNodeId { get; set; }

    [GraphQLDescription(
        "The index of a CreateInput object from the 'creations' list that is passed to the same OperationInput object. This field and 'fromNodeId' are mutually exclusive.")]
    public int? FromCreatedNodeIndex { get; set; }

    [GraphQLDescription("The id of an existing node. This field and 'toCreatedNodeIndex' are mutually exclusive.")]
    public Guid? ToNodeId { get; set; }

    [GraphQLDescription(
        "The index of a CreateInput object from the 'creations' list that is passed to the same OperationInput object. This field and 'toNodeId' are mutually exclusive.")]
    public int? ToCreatedNodeIndex { get; set; }

    public List<Guid> GetProvidedNodeGuids()
    {
        var nodeIds = new List<Guid>();

        if (ToNodeId != null)
            nodeIds.Add((Guid)ToNodeId);

        if (FromNodeId != null)
            nodeIds.Add((Guid)FromNodeId);

        return nodeIds;
    }

    public override void SetGuids(List<Create>? creations)
    {
        SetGuidFromOneOf("FromNodeId", "FromCreatedNodeIndex", creations);
        SetGuidFromOneOf("ToNodeId", "ToCreatedNodeIndex", creations);
        _guidsAreSet = true;
    }

    public async Task Execute(Cursor cursor)
    {
        try
        {
            if (!_guidsAreSet)
                throw new GraphQLException("Must call SetGuids before calling Execute for SetEdgeInput.");

            if (FromNodeId == null)
                throw new GraphQLException("'fromNodeId' not set for SetEdgeInput.");

            if (ToNodeId == null)
                throw new GraphQLException("'toNodeId' not set for SetEdgeInput.");

            var fromNodeGuid = (Guid)FromNodeId;
            var toNodeGuid = (Guid)ToNodeId;

            if (!cursor.NodeGuids.ContainsKey(fromNodeGuid))
                throw new GraphQLException("Node id does not exist: " + fromNodeGuid);

            var fromNode = cursor.NodeGuids[fromNodeGuid];
            var toNode = (Core.Node?)await fromNode.GetValueAsync(Key);

            if (toNode != null && toNode.Guid == toNodeGuid)
                throw new GraphQLException(
                    $"Edge key '{Key}' from node id '{fromNodeGuid}' is already set to node id '{toNodeGuid}'");

            await Task.WhenAll(new List<Task>
            {
                fromNode.SetValueAsync(Key, cursor.NodeGuids[toNodeGuid]),
                fromNode.SaveAsync()
            });
        }
        catch (Exception ex)
        {
            throw new GraphQLException(ex.Message);
        }
    }
}

public class SetEdgeType : ObjectType<SetEdge>
{
    protected override void Configure(
        IObjectTypeDescriptor<SetEdge> descriptor)
    {
        descriptor.Name("SetEdge")
            .Implements<GraphRelationshipOperationType>();
    }
}