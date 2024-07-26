namespace Graph.Api;

public class GraphSchema
{
    public GraphSchema(List<Core.Node> nodes)
    {
        var nodeTags = new List<string>();
        NodeTypes = new List<NodeSchema>();
        foreach (var node in nodes)
        {
            if (nodeTags.Contains(node.Tag)) continue;

            nodeTags.Add(node.Tag);
            NodeTypes.Add(new NodeSchema(node));
        }
    }

    public List<NodeSchema> NodeTypes { get; set; }
}

public class GraphSchemaType : ObjectType<GraphSchema>
{
}

public class NodeSchema
{
    public NodeSchema(Core.Node node)
    {
        Tag = node.Tag;

        Fields = new List<Field>();
        foreach (var field in node.GetFieldTypes())
            Fields.Add(new Field
            {
                Key = field.Key,
                Type = field.Value.ToString()
            });

        Edges = new List<string>();
        foreach (var edge in node.GetEdgeTypes())
            Edges.Add(edge.Key);

        EdgeCollections = new List<string>();
        foreach (var edgeCollection in node.GetEdgeCollectionTypes())
            EdgeCollections.Add(edgeCollection.Key);
    }

    public string Tag { get; set; }
    public List<Field> Fields { get; set; }
    public List<string> Edges { get; set; }
    public List<string> EdgeCollections { get; set; }
}

public class NodeSchemaType : ObjectType<NodeSchema>
{
}

public class Field
{
    public string Key { get; set; }
    public string Type { get; set; }
}

public class FieldType : ObjectType<Field>
{
}