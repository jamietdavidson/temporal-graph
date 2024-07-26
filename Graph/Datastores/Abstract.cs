namespace Graph.Core;

public abstract class AbstractDataStore : IDataStore
{
    public async Task SaveAsync(List<Node> nodes, ulong? timestamp, ModeEnum mode = ModeEnum.Delta)
    {
        if (mode == ModeEnum.Data) await _DeltaToDataAsync(nodes);

        await SerializeAsync(nodes, timestamp, mode);

        if (mode == ModeEnum.Delta) await _DeltaToDataAsync(nodes);
    }

    public abstract Task<List<ulong>> GetNodeTimestampsAsync(Guid nodeGuid);

    public abstract Task<(ulong? timestamp, List<Node> nodes)> SearchAsync(Query query, bool includeRemoved = false);

    public abstract Task<List<Node>> GetReferencingNodesAsync(Guid toNodeGuid);

    protected abstract Task SerializeAsync(List<Node> nodes, ulong? timestamp, ModeEnum mode = ModeEnum.Delta);

    private async Task _DeltaToDataAsync(List<Node> nodes)
    {
        foreach (var node in nodes) await node.DeltaToDataAsync();
    }
}