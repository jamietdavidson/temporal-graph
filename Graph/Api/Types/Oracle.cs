using Graph.Core;

namespace Graph.Api;

public class Oracle
{
    private readonly Cursor _cursor;

    public Oracle(Cursor cursor, ulong? timestamp = null, List<Guid>? createdNodeGuids = null, List<Guid>? deletedNodeGuids = null)
    {
        _cursor = cursor;
        Id = cursor.Guid;
        State = cursor.State.ToString();
        RequestedTimestamp = timestamp;
        Timestamp = cursor.Timestamp;
        Schema = new GraphSchema(cursor.Nodes);
        AdditionalContext = new AdditionalContext(cursor, createdNodeGuids, deletedNodeGuids);
    }

    public Guid Id { get; set; }
    public ulong? RequestedTimestamp { get; set; }
    public ulong Timestamp { get; set; }
    public string State { get; set; }
    public GraphSchema Schema { get; set; }
    public AdditionalContext AdditionalContext { get; set; }

    public async Task<Node?> GetNode(Guid nodeGuid)
    {
        if (!_cursor.NodeGuids.ContainsKey(nodeGuid))
            await _cursor.LazyLoad(nodeGuid);

        if (_cursor.NodeGuids.ContainsKey(nodeGuid))
            return new Node(_cursor.NodeGuids[nodeGuid]);

        throw new GraphQLException("Node id does not exist at the selected keyframe: " + nodeGuid);
    }

    public async Task<List<Node>> GetNodes(List<Guid>? nodeGuids)
    {
        if (nodeGuids == null) nodeGuids = _cursor.NodeGuids.Keys.ToList();

        var nodes = new List<Node>();
        var toLazyLoad = nodeGuids.Where(n => !_cursor.NodeGuids.ContainsKey(n)).ToList();

        if (toLazyLoad.Any())
            await _cursor.LazyLoad(toLazyLoad);

        foreach (var guid in nodeGuids)
            if (_cursor.NodeGuids.ContainsKey(guid))
                nodes.Add(new Node(_cursor.NodeGuids[guid]));

        return nodes;
    }

    public static async Task<Oracle> NewOracleAsync(List<Guid> nodeGuids, ulong? timestamp = null,
        OracleActionEnum? oracleAction = null)
    {
        var cursor = new Cursor(new Session(new MongoDataStore()));
        await _SetCursorPosition(cursor, nodeGuids, timestamp, oracleAction);

        return new Oracle(cursor, timestamp);
    }

    public static async Task<Oracle> NewOracleAsync(Cursor cursor, List<Guid> nodeGuids, ulong? timestamp = null,
        OracleActionEnum? oracleAction = null)
    {
        await _SetCursorPosition(cursor, nodeGuids, timestamp, oracleAction);

        return new Oracle(cursor, timestamp);
    }

    private static async Task _SetCursorPosition(Cursor cursor, List<Guid> nodeGuids, ulong? timestamp,
        OracleActionEnum? oracleAction)
    {
        if (oracleAction == OracleActionEnum.Now)
        {
            await cursor.NowAsync(nodeGuids);
        }
        else if (oracleAction == OracleActionEnum.Inception)
        {
            await cursor.InceptionAsync(nodeGuids);
        }
        else if (timestamp != null)
        {
            if (oracleAction == OracleActionEnum.Previous)
                await cursor.PreviousAsync(nodeGuids, timestamp);
            else if (oracleAction == OracleActionEnum.Next)
                await cursor.NextAsync(nodeGuids, timestamp);
            else
                await cursor.SearchAsync(
                    new Core.Query()
                        .At((ulong)timestamp)
                        .Include(nodeGuids)
                );
        }
    }
}

public class OracleType : ObjectType<Oracle>
{
    protected override void Configure(IObjectTypeDescriptor<Oracle> descriptor)
    {
        descriptor
            .Field("nodes")
            .Type<ListType<NodeType>>()
            .Argument("ids", a => a.Type<ListType<UuidType>>())
            .Resolve(async context =>
            {
                return await context.Parent<Oracle>().GetNodes(
                    context.ArgumentValue<List<Guid>>("ids")
                );
            });

        descriptor
            .Field("node")
            .Type<NodeType>()
            .Argument("id", a => a.Type<NonNullType<UuidType>>())
            .Resolve(context =>
            {
                return context.Parent<Oracle>().GetNode(
                    context.ArgumentValue<Guid>("id")
                );
            });
    }
}