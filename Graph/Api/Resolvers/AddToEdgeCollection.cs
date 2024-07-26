using Graph.Core;

namespace Graph.Api;

public class AddToEdgeCollection : MutuallyExclusiveGuidResolver, IGraphRelationshipOperation
{
    private List<Guid> _toNodeGuids = new();

    private bool _guidsAreSet = false;

    public Guid ActionId { get; set; }

    public string Key { get; set; }

    [GraphQLDescription("The id of an existing node. This field and 'fromCreatedNodeIndex' are mutually exclusive.")]
    public Guid? FromNodeId { get; set; }

    [GraphQLDescription(
        "The index of a CreateInput object from the 'creations' list that is passed to the same OperationInput object. This field and 'fromNodeId' are mutually exclusive.")]
    public int? FromCreatedNodeIndex { get; set; }

    [GraphQLDescription("The ids of existing nodes. This field can be used in combination with 'toNodeIds'.")]
    public List<Guid>? ToNodeIds { get; set; }

    [GraphQLDescription(
        "The indices of CreateInput objects from the 'creations' list that is passed to the same OperationInput object. This field can be used in combination with 'toNodeIds'.")]
    public List<int>? ToCreatedNodeIndices { get; set; }

    public List<Guid> GetProvidedNodeGuids()
    {
        var guids = new List<Guid>();

        if (FromNodeId != null)
            guids.Add((Guid)FromNodeId);

        if (ToNodeIds != null)
            guids.AddRange(ToNodeIds);

        return guids;
    }

    public override void SetGuids(List<Create>? creations)
    {
        SetGuidFromOneOf("FromNodeId", "FromCreatedNodeIndex", creations);

        if (ToNodeIds != null)
            _toNodeGuids = ToNodeIds;

        if (ToCreatedNodeIndices != null)
        {
            foreach (var index in ToCreatedNodeIndices)
            {
                if (index >= creations?.Count)
                    throw new GraphQLException(
                        $"Index '{index}' is out of range for 'toCreatedNodeIndices' in AddToEdgeCollectionInput.");

                _toNodeGuids.Add(creations![index].GetNodeGuid());
            }
        }

        if (_toNodeGuids.Count == 0)
            throw new GraphQLException(
                "'toNodeIds' and 'toCreatedNodeIndices' cannot both be empty for AddToEdgeCollectionInput.");

        _guidsAreSet = true;
    }

    public async Task Execute(Cursor cursor)
    {
        try
        {
            if (!_guidsAreSet)
                throw new GraphQLException("Must call SetGuids before calling Execute for AddToEdgeCollectionInput.");

            if (FromNodeId == null)
                throw new GraphQLException("'fromNodeId' not set for AddToEdgeCollectionInput.");

            var fromNodeGuid = (Guid)FromNodeId;
            var fromNode = cursor.NodeGuids[fromNodeGuid];

            if (!cursor.NodeGuids.ContainsKey(fromNodeGuid))
                throw new GraphQLException("Node id does not exist: " + FromNodeId);

            var edgeCollection =
                (EdgeCollection<Core.Node, Core.Node>?)await fromNode.GetValueAsync(Key);

            if (edgeCollection == null && !Core.Node.SchemalessEnabled)
                throw new GraphQLException(
                    $"Edge collection key '{Key}' does not exist on node id '{fromNodeGuid}'");

            var toNodes = _GetToNodes(cursor);

            if (edgeCollection == null && Core.Node.SchemalessEnabled)
                await fromNode.SetValueAsync(Key,
                    new EdgeCollection<Core.Node, Core.Node>(fromNode, toNodes.First().Tag, toNodes));
            else
            {
                if (edgeCollection != null)
                    edgeCollection.Append(toNodes);
                else
                    throw new GraphQLException($"Failed to get edge collection '{Key}' from node id '{fromNodeGuid}'");
            }
            await fromNode.SaveAsync();
        }
        catch (Exception ex)
        {
            throw new GraphQLException(ex.Message);
        }
    }

    private List<Core.Node> _GetToNodes(Cursor cursor)
    {
        var toNodes = new List<Core.Node>();
        foreach (var guid in _toNodeGuids)
        {
            if (!cursor.NodeGuids.ContainsKey(guid))
                throw new GraphQLException("Node id does not exist: " + guid);

            toNodes.Add(cursor.NodeGuids[guid]);
        }

        return toNodes;
    }
}

public class AddToEdgeCollectionType : ObjectType<AddToEdgeCollection>
{
    protected override void Configure(
        IObjectTypeDescriptor<AddToEdgeCollection> descriptor)
    {
        descriptor.Name("AddToEdgeCollection")
            .Implements<GraphRelationshipOperationType>();
    }
}