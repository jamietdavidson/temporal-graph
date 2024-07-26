using Graph.Core;

namespace Graph.Templates;

public static class PartyKey
{
    public static string ID { get; } = "id";
    public static string NAME { get; } = "name";
    public static string DESCRIPTION { get; } = "description";
}

public class TemplateParty : NodeIntermediary
{
    public int id { get; set; }
    public string name { get; set; }
    public string? description { get; set; }

    public async override Task<Node> ToNodeAsync(Cursor cursor)
    {
        var partyRep = new NodeRep("Party");
        partyRep.Fields.NumericFields = new Dictionary<string, decimal?>
        { { PartyKey.ID, id } };
        partyRep.Fields.StringFields = new Dictionary<string, string?>
        {
            { PartyKey.NAME, name },
            { PartyKey.DESCRIPTION, description }
        };

        var node = Node.FromRep(partyRep);
        node.Cursor = cursor;
        await node.SaveAsync();
        Guid = node.Guid;
        return node;
    }
}