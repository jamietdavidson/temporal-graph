using Graph.Core;
using Newtonsoft.Json.Linq;

namespace Graph.Templates;

public class TemplateDependency : NodeIntermediary
{
    public NodeRep NodeRep { get; set; }
    public string? checkpoint { get; set; }
    public DependencyComparison? compare { get; set; }
    public List<string> References { get; set; } = new List<string>();

    public async override Task<Node> ToNodeAsync(Cursor cursor)
    {
        NodeRep = new NodeRep("Dependency");
        NodeRep.Fields.StringFields = new Dictionary<string, string?>
        {{ DependencyKey.CHECKPOINT, checkpoint } };
        NodeRep.Edges.Tags.Add(DependencyKey.CHECKPOINT, "Checkpoint");

        if (compare != null)
        {
            NodeRep.Fields.StringFields.Add(DependencyKey.OPERATOR, compare.@operator);

            if (compare.left.@ref != null)
            {
                NodeRep.Fields.StringFields.Add(DependencyKey.LEFT_REFERENCE, compare.left.@ref);
                NodeRep.Fields.StringFields.Add(DependencyKey.LEFT_TYPE, DependencyKey.OPERAND_TYPE);
                References.Add(compare.left.@ref);
            }
            else
                _SetValue(DependencyKey.LEFT, compare.left.value);

            if (compare.right.@ref != null)
            {
                NodeRep.Fields.StringFields.Add(DependencyKey.RIGHT_REFERENCE, compare.right.@ref);
                NodeRep.Fields.StringFields.Add(DependencyKey.RIGHT_TYPE, DependencyKey.OPERAND_TYPE);
                References.Add(compare.right.@ref);
            }
            else
                _SetValue(DependencyKey.RIGHT, compare.right.value);
        }

        var node = Node.FromRep(NodeRep);
        node.Cursor = cursor;
        await node.SaveAsync();
        Guid = node.Guid;
        return node;
    }

    private void _SetValue(string operandSide, object? value)
    {
        var type = value != null ? Utils.DetermineFieldType(value) : "null";
        NodeRep.Fields.StringFields.Add($"{operandSide}_{DependencyKey.OPERAND_TYPE}", type);
        var key = $"{operandSide}_{type}_{DependencyKey.OPERAND_VALUE}";

        if (type == "null")
            NodeRep.Fields.StringFields.Add(key, null);
        else if (type == "boolean")
            NodeRep.Fields.BooleanFields = new Dictionary<string, bool?>
            { { key, (bool?)value } };
        else if (type == "string")
            NodeRep.Fields.StringFields.Add(key, (string?)value);
        else if (type == "numeric")
            NodeRep.Fields.NumericFields = new Dictionary<string, decimal?>
            { { key, value != null ? decimal.Parse(value.ToString() ?? "") : null } };
        else if (type == "boolean_list")
            NodeRep.Fields.BooleanListFields = new Dictionary<string, List<bool?>?>
            { { key, ((JArray)value!).Children().Select(item => item.Value<bool?>()).ToList() } };
        else if (type == "string_list")
            NodeRep.Fields.StringListFields = new Dictionary<string, List<string?>?>
            { { key, ((JArray)value!).Children().Select(item => item.Value<string?>()).ToList() } };
        else if (type == "numeric_list")
            NodeRep.Fields.NumericListFields = new Dictionary<string, List<decimal?>?>
            { { key, ((JArray)value!).Children().Select(item => item.Value<decimal?>()).ToList() } };
    }
}

public class DependencyComparison
{
    public string @operator { get; set; }
    public Operand left { get; set; }
    public Operand right { get; set; }
}

public class Operand
{
    public string? @ref { get; set; }
    public object? value { get; set; }
}