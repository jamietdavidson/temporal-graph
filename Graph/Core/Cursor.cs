namespace Graph.Core;

public class Cursor
{
    private readonly Dictionary<Guid, Node> _queue = new();

    private Query _lastQuery;

    private ulong? _requestedTimestamp;

    private List<KeyValuePair<ulong, Guid>> _shadowNodeGuids = new();

    private ulong? _timestamp;

    public Cursor(Session session, CursorStateEnum state = CursorStateEnum.Live)
    {
        _Init(session, Guid.NewGuid(), state, null);
        Session = session;
    }

    public Cursor(Session session, List<Node> nodes, CursorStateEnum state = CursorStateEnum.Live)
    {
        _Init(session, Guid.NewGuid(), state, nodes);
        Session = session;
    }

    public Cursor(Session session, List<Node>? nodes, Guid? guid, CursorStateEnum state = CursorStateEnum.Live)
    {
        _Init(session, guid ?? Guid.NewGuid(), state, nodes);
        Session = session;
    }

    public Guid Guid { get; private set; }

    public Session Session { get; set; }

    public ulong? SchemaId
    {
        get => null;
    }

    public ulong? SubgraphId
    {
        get => null;
    }

    public CursorStateEnum State { get; private set; }

    public ulong Timestamp => _GetTimestamp();

    public ulong? RequestedTimestamp => _requestedTimestamp ?? _GetTimestamp();

    public Dictionary<Guid, Node> NodeGuids { get; } = new();

    public List<Node> Nodes => NodeGuids.Values.ToList();

    public bool InTogetherBlock { get; private set; }

    private void _Init(Session session, Guid guid, CursorStateEnum state, List<Node>? nodes)
    {
        Guid = guid;
        State = state;
        _Build(null, nodes);
    }

    private void _Build(ulong? timestamp, List<Node>? nodes)
    {
        if (timestamp != null) _timestamp = timestamp;

        if (nodes == null || nodes.Count() == 0)
        {
            NodeGuids.Clear();
        }
        else
        {
            foreach (var node in nodes)
            {
                node.Cursor = this;
                AddNode(node);
            }

            if (State != CursorStateEnum.Live) _SetShadowNodeGuids();
        }
    }

    private void _SetShadowNodeGuids()
    {
        var timestamp = Timestamp;
        foreach (var node in Nodes)
            if (node.Timestamp > timestamp)
            {
                _shadowNodeGuids.Add(new KeyValuePair<ulong, Guid>(node.Timestamp, node.Guid));
                NodeGuids.Remove(node.Guid);
            }
    }

    private void _RemoveDeletedNodes()
    {
        foreach (var node in Nodes)
            if (node.Deleted) NodeGuids.Remove(node.Guid);
    }

    private List<Guid> _GetShadowNodeGuidsAfter(ulong timestamp)
    {
        var relevantGuids = new List<Guid>();
        foreach (var item in _shadowNodeGuids)
            if (item.Key >= timestamp)
                relevantGuids.Add(item.Value);

        _shadowNodeGuids = _shadowNodeGuids.Select(sg => sg).Where(sg => sg.Key >= timestamp).ToList();

        return relevantGuids;
    }

    public Node? GetNode(Guid nodeGuid)
    {
        return NodeGuids.ContainsKey(nodeGuid) ? NodeGuids[nodeGuid] : null;
    }

    public async Task<List<ulong>> GetNodeTimestampsAsync(Guid nodeGuid)
    {
        return await Session.Database.GetNodeTimestampsAsync(nodeGuid);
    }

    public async Task<Node?> LazyLoad(Guid nodeGuid)
    {
        if (NodeGuids.ContainsKey(nodeGuid) && NodeGuids[nodeGuid].IsLoaded)
            return NodeGuids[nodeGuid];

        var query = State != CursorStateEnum.Live ? _lastQuery : null;
        return await SearchAsync(nodeGuid, query);
    }

    public async Task<List<Node>> LazyLoad(List<Guid> nodeGuids)
    {
        var query = State != CursorStateEnum.Live ? _lastQuery : new Query();
        var nodes = await SearchAsync(query.Include(nodeGuids));
        return nodes.Where(n => nodeGuids.Contains(n.Guid)).ToList();
    }

    private void _SetLastQueryTimestamps(Query query)
    {
        _lastQuery = new Query();

        if (query.AtTimestamp != null)
            _lastQuery.At((ulong)query.AtTimestamp);

        if (query.AfterTimestamp != null)
            _lastQuery.After((ulong)query.AfterTimestamp);
    }

    public async Task<Node?> InceptionAsync(Guid nodeGuid)
    {
        _timestamp = (ulong)DatesEnum.Inception;

        return await SearchAsync(nodeGuid, new Query()
                .After((ulong)_timestamp),
            // TODO: .on(branchInfo)
            newState: CursorStateEnum.Rewind
        );
    }

    public async Task<List<Node>> InceptionAsync(List<Guid>? nodeGuids = null)
    {
        _timestamp = (ulong)DatesEnum.Inception;

        var query = new Query()
            .After((ulong)_timestamp);
        // TODO: .on(branchInfo)

        if (nodeGuids != null) query.Include(nodeGuids);

        return await SearchAsync(query, newState: CursorStateEnum.Rewind);
    }

    public async Task<Node?> PreviousAsync(Guid nodeGuid, ulong? timestamp = null, int steps = 1)
    {
        if (steps != 1) throw new NotImplementedException();

        var ts = timestamp ?? _GetTimestamp();
        ts = await _GetPreviousRelevantTimestamp(nodeGuid, ts) ?? ts;
        return await SearchAsync(nodeGuid, new Query()
                .At(ts),
            // TODO: .on(branchInfo)
            newState: CursorStateEnum.Rewind
        );
    }

    public async Task<List<Node>> PreviousAsync(List<Guid>? nodeGuids = null, ulong? timestamp = null, int steps = 1)
    {
        if (steps != 1) throw new NotImplementedException();

        var query = new Query()
            .At((timestamp ?? _GetTimestamp()) - 1);
        // TODO: .on(branchInfo)

        if (nodeGuids != null) query.Include(nodeGuids);

        return await SearchAsync(query, newState: CursorStateEnum.Rewind);
    }

    public async Task<Node?> NextAsync(Guid nodeGuid, ulong? timestamp = null, int steps = 1)
    {
        if (steps != 1) throw new NotImplementedException();

        var ts = timestamp ?? _GetTimestamp();
        var nextRelevantTimestamp = await _GetNextRelevantTimestamp(nodeGuid, ts) ?? ts;

        return await SearchAsync(nodeGuid, new Query()
                .At(ts)
                .After(nextRelevantTimestamp - 1)
        // TODO: .on(branchInfo)
        );
    }

    public async Task<List<Node>> NextAsync(List<Guid>? nodeGuids = null, ulong? timestamp = null, int steps = 1)
    {
        if (steps != 1) throw new NotImplementedException();

        var ts = timestamp ?? _GetTimestamp();
        var query = new Query()
            .At(ts)
            .After(ts);
        // TODO: .on(branchInfo)

        if (nodeGuids != null) query.Include(nodeGuids);

        return await SearchAsync(query);
    }

    public async Task<Node?> NowAsync(Guid nodeGuid)
    {
        return await SearchAsync(nodeGuid,
            new Query().At(Utils.TimestampMillis()),
            // TODO: .on(branchInfo)
            newState: CursorStateEnum.Live
        );
    }

    public async Task<List<Node>> NowAsync(List<Guid>? nodeGuids = null)
    {
        var query = new Query()
            .At(Utils.TimestampMillis());
        // TODO: .on(branchInfo)

        if (nodeGuids != null) query.Include(nodeGuids);

        return await SearchAsync(query, newState: CursorStateEnum.Live);
    }

    public async Task SaveAsync(Node node, ModeEnum mode = ModeEnum.Delta)
    {
        _timestamp = _GetTimestamp();
        if (InTogetherBlock)
            _queue[node.Guid] = node;
        else
            await Session.Database.SaveAsync(new List<Node> { node }, _timestamp, mode);
    }

    public async Task KeyframeAsync(ulong? timestamp = null)
    {
        if (InTogetherBlock) throw new InvalidOperationException("Cannot keyframe during atomic block.");

        _CheckStateIsNow();
        await Session.Database.SaveAsync(Nodes, timestamp, ModeEnum.Data);
    }

    public async Task<Node?> SearchAsync(Guid nodeGuid, Query? query = null, bool setRequestedTimestamp = false,
        CursorStateEnum? newState = null, bool lazyLoading = false)
    {
        query = query ?? new Query();
        query.Include(nodeGuid);

        return _GetNodeByGuid(await SearchAsync(query, setRequestedTimestamp, newState), nodeGuid);
    }

    public async Task<List<Node>> SearchAsync(Query? query = null, bool setRequestedTimestamp = false,
        CursorStateEnum? newState = null)
    {
        if (query == null)
            query = new Query();

        var now = Utils.TimestampMillis();
        if (!query.HasTimestamp)
            // Query now if query does not have timestamp set
            query.At(_GetTimestamp(now));
        else if (query.AtTimestamp == null && Nodes.Any())
            // If querying after, IDataStore can apply deltas to current nodes
            query.CurrentNodes = Nodes;

        if (newState != null)
            State = (CursorStateEnum)newState;
        else if (query.AtTimestamp < now)
            State = CursorStateEnum.Rewind;

        if (State != CursorStateEnum.Live)
            // We must preserve the timestamps in case lazy-loading is necessary
            _SetLastQueryTimestamps(query);

        query.Include(NodeGuids.Keys.ToList())
            .Include(_GetShadowNodeGuidsAfter((ulong)(query.AfterTimestamp ?? query.AtTimestamp)!));

        if (!query.IncludeGuids.Any()) return new List<Node>(); // don't even bother...

        // filter nodes
        var result = await Session.Database.SearchAsync(query);
        result.nodes = result.nodes.OrderBy(n => n.Guid).ToList();

        var timestamp = Utils.TimestampMillis(result.timestamp);

        if (setRequestedTimestamp)
        {
            if (query.AfterTimestamp != null)
                throw new ArgumentException("Cursor state can only be requested at a particular time, not after.");

            timestamp = (ulong)query.AtTimestamp!;
        }

        // update cursor nodes
        _Build(timestamp, result.nodes);

        if (!result.nodes.Any())
            return result.nodes;

        // fin
        return result.nodes;
    }

    public async Task<AnyType?> TogetherAsync(ModeEnum mode = ModeEnum.Delta, ulong? timestamp = null,
        Func<Task>? operation = null)
    {
        _CheckStateIsNow();

        InTogetherBlock = true;

        if (operation is Func<Task>) await operation();

        if (_queue.Any())
        {
            if (mode == ModeEnum.Data)
            {
                var tasks = new List<Task>();
                foreach (var node in _queue.Values) tasks.Add(node.DeltaToDataAsync());
                await Task.WhenAll(tasks);
            }

            await Session.Database.SaveAsync(_queue.Values.ToList(), timestamp, mode);

            _RemoveDeletedNodes();
        }

        _queue.Clear();
        InTogetherBlock = false;

        return null;
    }

    public void AddNode(Node node)
    {
        if (node.IsLoaded)
        {
            // Preserve readonly state....
            // TODO: decide whether to set the node owner as an attribute on the node
            // (Node.IsReadonly could be inferred from the cursor's associated flow account)
            if (NodeGuids.ContainsKey(node.Guid) && NodeGuids[node.Guid].IsReadonly)
                node.IsReadonly = true;

            NodeGuids[node.Guid] = node;
        }
        // else LazyLoad will be called by the Edge or EdgeCollection when the node is explicitly requested.
    }

    public async Task<AnyType?> PropagateNodeDeletionAsync(Node node)
    {
        if (!node.Deleted)
            throw new InvalidOperationException("Node must be deleted before propagation can occur.");

        var propagate = async () =>
        {
            var referencingNodes = await Session.Database.GetReferencingNodesAsync(node.Guid);

            var tasks = new List<Task> { node.SaveAsync() };
            foreach (var referencingNode in referencingNodes)
            {
                var fromNode = referencingNode;
                if (_queue.ContainsKey(referencingNode.Guid))
                    fromNode = _queue[referencingNode.Guid];
                if (fromNode.Deleted) continue;
                else if (NodeGuids.ContainsKey(referencingNode.Guid))
                    fromNode = NodeGuids[referencingNode.Guid];

                tasks.Add(fromNode.PropagateDeletionOfReferencedNodeAsync(node));
            }

            await Task.WhenAll(tasks);
        };

        if (InTogetherBlock)
        {
            await propagate();
            return null;
        }
        else
            return await TogetherAsync(ModeEnum.Data, operation: propagate);
    }

    private ulong _GetTimestamp(ulong? now = null)
    {
        if (State == CursorStateEnum.Live)
            return Utils.TimestampMillis(now);

        return Utils.TimestampMillis(_timestamp);
    }

    private void _CheckStateIsNow()
    {
        if (State != CursorStateEnum.Live) throw new InvalidOperationException("Cursor state is not Live.");
    }

    private Node? _GetNodeByGuid(List<Node> nodes, Guid guid)
    {
        var matching = nodes.Where(n => n.Guid == guid).ToList();
        return matching.Any() ? matching[0] : null;
    }

    private async Task<ulong?> _GetNextRelevantTimestamp(Guid nodeGuid, ulong timestamp)
    {
        var relevantTimestamps = await Session.Database.GetNodeTimestampsAsync(nodeGuid);

        foreach (var ts in relevantTimestamps)
            if (ts > timestamp)
                return ts;

        return null;
    }

    private async Task<ulong?> _GetPreviousRelevantTimestamp(Guid nodeGuid, ulong timestamp)
    {
        var relevantTimestamps = await Session.Database.GetNodeTimestampsAsync(nodeGuid);

        relevantTimestamps.Reverse();
        foreach (var ts in relevantTimestamps)
            if (ts < timestamp)
                return ts;

        return null;
    }

    private void _SetReadonlyNodes(string mutateGraphErrorMessage)
    {
        // From the error message, parse on-chain ids of nodes that are missing from the active account's subgraph
        var split = mutateGraphErrorMessage
            .Split("Subgraph does not contain Node IDs: ")[1];
        var onChainIds = split.Substring(0, split.IndexOf("-->")).Trim()
            .Split(",");

        // Set those nodes to be readonly
        foreach (var onChainId in onChainIds)
        {
            var node = Nodes.FirstOrDefault(n => n.OnChainId == onChainId);
            if (node != null) node.IsReadonly = true;
        }
    }
}