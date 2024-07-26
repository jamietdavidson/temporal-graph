using Graph.Core;
using Graph.Mongo;
using Newtonsoft.Json;

namespace Graph.Templates;

public class TemplateActionOperation : NodeIntermediary
{
    public string?[]? include { get; set; }
    public string?[]? exclude { get; set; }
    public Dictionary<string, object>? default_values { get; set; }
    public Dictionary<string, string>? default_edges { get; set; }
    public string? appends_objects_to { get; set; }

    public string? Type { get; set; }

    public async override Task<Node> ToNodeAsync(Cursor cursor)
    {
        var actionOperationRep = new NodeRep("ActionOperation");

        actionOperationRep.Fields.StringListFields = new Dictionary<string, List<string?>?>
        {
            { ActionOperationKey.INCLUDE, include?.ToList() },
            { ActionOperationKey.EXCLUDE, exclude?.ToList() }
        };
        actionOperationRep.Fields.StringFields = new Dictionary<string, string?>
        {
            { ActionOperationKey.APPENDS_OBJECTS_TO, appends_objects_to },
            { ActionOperationKey.INCLUSION_TYPE, Type }
        };
        actionOperationRep.EdgeCollections.Tags.Add("default_values", "DefaultValue");
        actionOperationRep.EdgeCollections.Tags.Add("default_edges", "DefaultValue");
        actionOperationRep.EdgeCollections.Values.Add("default_edges", new List<Guid>());

        var tasks = new List<Task<Node>>();
        foreach (var (key, value) in default_values ?? new Dictionary<string, object>())
            tasks.Add(new DefaultValue(key, value).ToNodeAsync(cursor));

        var defaultValueNodes = await Task.WhenAll(tasks);
        actionOperationRep.EdgeCollections.Values.Add(
            "default_values",
            defaultValueNodes.Select(n => n.Guid).ToList()
        );

        var node = Node.FromRep(actionOperationRep);
        node.Cursor = cursor;
        await node.SaveAsync();
        Guid = node.Guid;
        return node;
    }

    public async new Task ResolveReferencesAsync(TemplateStateMap template, Cursor cursor)
    {
        var node = cursor.NodeGuids[(Guid)Guid!];

        if (default_edges != null)
        {
            var tasks = new List<Task<Node>>();
            foreach (var (key, value) in default_edges)
            {
                var defaultEdgeNode = await ReferenceResolver.ResolveReferenceAsync(
                    value,
                    template.ObjectPromises.Select(g => cursor.NodeGuids[g]).ToList()
                );
                tasks.Add(new DefaultValue(key, defaultEdgeNode.Guid.ToString()).ToNodeAsync(cursor));
            }
            var defaultValueNodes = await Task.WhenAll(tasks);

            var defaultEdges = (EdgeCollection<Node, Node>)(await node.GetValueAsync("default_edges"))!;
            defaultEdges.Append(defaultValueNodes.ToList());
        }
    }
}

public class DefaultValue : NodeIntermediary
{
    public NodeRep NodeRep { get; set; }

    public DefaultValue(string key, object value)
    {
        NodeRep = new NodeRep("DefaultValue");
        NodeRep.Fields.StringFields = new Dictionary<string, string?>
        { { "key", key } };

        var type = Utils.DetermineFieldType(value);
        NodeRep.Fields.StringFields.Add("value_type", type);
        var defaultValueKey = type + "_default_value";

        if (type == "boolean")
            NodeRep.Fields.BooleanFields = new Dictionary<string, bool?>
            { { defaultValueKey, (bool?)value } };
        else if (type == "string")
            NodeRep.Fields.StringFields.Add(defaultValueKey, (string?)value);
        else if (type == "numeric")
            NodeRep.Fields.NumericFields = new Dictionary<string, decimal?>
            { { defaultValueKey, decimal.Parse(value.ToString()!) } };
        else if (type == "boolean_list")
            NodeRep.Fields.BooleanListFields = new Dictionary<string, List<bool?>?>
            { { defaultValueKey, JsonConvert.DeserializeObject<List<bool?>>(value.ToString() ?? "") } };
        else if (type == "string_list")
            NodeRep.Fields.StringListFields = new Dictionary<string, List<string?>?>
            { { defaultValueKey, JsonConvert.DeserializeObject<List<string?>>(value.ToString() ?? "") } };
        else if (type == "numeric_list")
            NodeRep.Fields.NumericListFields = new Dictionary<string, List<decimal?>?>
            { { defaultValueKey, JsonConvert.DeserializeObject<List<decimal?>>(value.ToString() ?? "") } };
    }

    public async override Task<Node> ToNodeAsync(Cursor cursor)
    {
        var node = Node.FromRep(NodeRep);
        node.Cursor = cursor;
        await node.SaveAsync();
        Guid = node.Guid;
        return node;
    }
}