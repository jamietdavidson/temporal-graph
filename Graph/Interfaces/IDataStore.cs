namespace Graph.Core;

public interface IDataStore
{
    Task SaveAsync(List<Node> nodes, ulong? timestamp, ModeEnum mode = ModeEnum.Delta);

    Task<List<ulong>> GetNodeTimestampsAsync(Guid nodeGuid);

    Task<(ulong? timestamp, List<Node> nodes)> SearchAsync(Query query, bool includeRemoved = false);
}