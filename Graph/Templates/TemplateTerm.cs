using Graph.Core;

namespace Graph.Templates;

public class TemplateTerm : NodeIntermediary
{
    public string name { get; set; }
    public string description { get; set; }
    public string?[]? attributes { get; set; }

    public async override Task<Node> ToNodeAsync(Cursor cursor)
    {
        var termRep = new NodeRep("Term");
        termRep.Fields.StringFields = new Dictionary<string, string?>
        {
            { TermKey.NAME, name },
            { TermKey.DESCRIPTION, description }
        };
        termRep.Fields.StringListFields = new Dictionary<string, List<string?>?>
        { { TermKey.ATTRIBUTES, attributes?.ToList() } };

        var node = Node.FromRep(termRep);
        node.Cursor = cursor;
        await node.SaveAsync();
        Guid = node.Guid;
        return node;
    }
}