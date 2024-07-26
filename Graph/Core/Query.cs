namespace Graph.Core;

public class Query
{
    //private Dictionary<string, string> _branchInfo = new() { { "path", Branches.ROOT } };

    private HashSet<Guid> _excludeGuids = new();

    public HashSet<Guid> IncludeGuids { get; } = new();

    public ulong? AtTimestamp { get; private set; }

    public ulong? AfterTimestamp { get; private set; }

    public List<Node>? CurrentNodes { get; set; }

    public bool HasTimestamp => AtTimestamp != null || AfterTimestamp != null;

    public Query At(ulong timestamp)
    {
        AtTimestamp = timestamp;
        return this;
    }

    public Query After(ulong timestamp)
    {
        AfterTimestamp = timestamp;
        return this;
    }

    public Query Include(List<Node> nodes)
    {
        IncludeGuids.UnionWith(nodes.Select(n => n.Guid));
        return this;
    }

    public Query Include(List<Guid> nodeGuids)
    {
        IncludeGuids.UnionWith(nodeGuids);
        return this;
    }

    public Query Include(Guid nodeGuid)
    {
        IncludeGuids.Add(nodeGuid);
        return this;
    }

    public Query Exclude(List<Node> nodes)
    {
        throw new NotImplementedException();
    }

    public Query On(List<int> branch)
    {
        throw new NotImplementedException();
    }
}