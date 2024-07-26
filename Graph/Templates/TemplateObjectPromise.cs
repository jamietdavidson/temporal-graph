using Graph.Core;
using Graph.Mongo;

namespace Graph.Templates;

public class TemplateObjectPromise : NodeIntermediary
{
    public int id { get; set; }
    public string name { get; set; }
    public string? description { get; set; }
    public string object_type { get; set; }

    public async override Task<Node> ToNodeAsync(Cursor cursor)
    {
        var objectPromiseRep = new NodeRep("ObjectPromise");
        objectPromiseRep.Fields.NumericFields = new Dictionary<string, decimal?>
        { { ObjectPromiseKey.ID, id } };
        objectPromiseRep.Fields.StringFields = new Dictionary<string, string?>
        {
            { ObjectPromiseKey.NAME, name },
            { ObjectPromiseKey.DESCRIPTION, description }
        };
        objectPromiseRep.Edges.Tags.Add(ObjectPromiseKey.OBJECT_TYPE, "ObjectType");
        objectPromiseRep.Edges.Values.Add(ObjectPromiseKey.OBJECT_TYPE, null);

        // Include a lookup for the actions that reference this object promise...
        // Action.ResolveReferencesAsync will populate this.
        objectPromiseRep.EdgeCollections.Tags.Add(ObjectPromiseKey.REFERENCED_BY_ACTIONS, "Action");
        objectPromiseRep.EdgeCollections.Values.Add(ObjectPromiseKey.REFERENCED_BY_ACTIONS, new List<Guid>());

        // Include a lookup for the checkpoints that depend on this object promise
        objectPromiseRep.EdgeCollections.Tags.Add(ObjectPromiseKey.DEPENDENT_CHECKPOINTS, "Checkpoint");
        objectPromiseRep.EdgeCollections.Values.Add(ObjectPromiseKey.DEPENDENT_CHECKPOINTS, new List<Guid>());

        var node = Node.FromRep(objectPromiseRep);
        node.Cursor = cursor;
        await node.SaveAsync();
        Guid = node.Guid;
        return node;
    }

    public async new Task ResolveReferencesAsync(TemplateStateMap template, Cursor cursor)
    {
        var objectTypeNode = await ReferenceResolver.ResolveReferenceAsync(
            object_type,
            template.ObjectTypes.Select(guid => cursor.NodeGuids[guid]).ToList()
        );
        var node = cursor.NodeGuids[(Guid)Guid!];
        await node.SetValueAsync(ObjectPromiseKey.OBJECT_TYPE, objectTypeNode);
        await node.SaveAsync();
    }
}