namespace Graph.Api;

public class Edge
{
    public Edge(string edgeKey, Core.Node node)
    {
        Key = edgeKey;
        Tag = node.Tag;
        Id = node.Guid;
    }

    public string Key { get; set; }

    public string Tag { get; set; }

    public Guid Id { get; set; }
}

public class EdgeType : ObjectType<Edge>
{
}