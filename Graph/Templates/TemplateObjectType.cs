using Graph.Core;
using Graph.Mongo;

namespace Graph.Templates;

public class TemplateObjectType : NodeIntermediary
{
    public int id { get; set; }
    public string name { get; set; }
    public string description { get; set; }
    public ObjectTypeAttribute[] attributes { get; set; }
    public NodeSchema NodeSchema { get; set; }

    public async override Task<Node> ToNodeAsync(Cursor cursor)
    {
        var objectTypeRep = new NodeRep(ObjectTypeKey.NAME);
        objectTypeRep.Fields.NumericFields = new Dictionary<string, decimal?>
        { { ObjectTypeKey.ID, id } };
        objectTypeRep.Fields.StringFields = new Dictionary<string, string?>
        {
            { ObjectTypeKey.NAME, name },
            { ObjectTypeKey.DESCRIPTION, description }
        };

        objectTypeRep.EdgeCollections.Tags.Add(ObjectTypeKey.ATTRIBUTES, "ObjectTypeAttribute");
        var tasks = new List<Task<Node>>();
        foreach (var attr in attributes)
            tasks.Add(attr.ToNodeAsync(cursor));

        var attributeNodes = await Task.WhenAll(tasks);
        objectTypeRep.EdgeCollections.Values.Add(
            ObjectTypeKey.ATTRIBUTES,
            attributeNodes.Select(node => node.Guid).ToList()
        );

        var node = Node.FromRep(objectTypeRep);
        node.Cursor = cursor;
        await node.SaveAsync();
        Guid = node.Guid;
        return node;
    }

    public void ToNodeSchema()
    {
        var fieldTypes = new Dictionary<string, Type>();
        var edgeTypes = new Dictionary<string, string>();
        var edgeCollectionTypes = new Dictionary<string, string>();

        foreach (var attr in attributes)
        {
            if (attr.type == ObjectTypeAttributeKey.BOOLEAN)
            {
                fieldTypes.Add(attr.name, typeof(Core.Boolean));
            }
            else if (attr.type == ObjectTypeAttributeKey.STRING)
            {
                fieldTypes.Add(attr.name, typeof(Core.String));
            }
            else if (attr.type == ObjectTypeAttributeKey.NUMERIC)
            {
                fieldTypes.Add(attr.name, typeof(Core.Decimal));
            }
            else if (attr.type == ObjectTypeAttributeKey.BOOLEAN_LIST)
            {
                fieldTypes.Add(attr.name, typeof(BooleanList));
            }
            else if (attr.type == ObjectTypeAttributeKey.STRING_LIST)
            {
                fieldTypes.Add(attr.name, typeof(StringList));
            }
            else if (attr.type == ObjectTypeAttributeKey.NUMERIC_LIST)
            {
                fieldTypes.Add(attr.name, typeof(NumericList));
            }
            // Note that edge and edge collection attributes are set
            // by the SetEdgeTypes method after all ObjectType node schemas have been instantiated.
        }

        NodeSchema = new NodeSchema(
            tag: name,
            fieldTypes,
            edgeTypes,
            edgeCollectionTypes
        );
    }

    public async Task SetNodeSchemaEdgeTypesAsync(List<Node> objectTypes)
    {
        var edgeTypes = new string[] { ObjectTypeAttributeKey.EDGE, ObjectTypeAttributeKey.EDGE_COLLECTION };
        foreach (var attr in attributes)
        {
            if (!edgeTypes.Contains(attr.type) || attr.object_type == null)
                continue;

            var objectTypeNode = await ReferenceResolver.ResolveReferenceAsync(
                attr.object_type,
                objectTypes
            );

            if (attr.type == ObjectTypeAttributeKey.EDGE)
            {
                if (!NodeSchema.EdgeDefinitions.ContainsKey(attr.name))
                    NodeSchema.EdgeDefinitions[attr.name] = new EdgeDefinition(attr.name, objectTypeNode.Tag);
            }
            else if (attr.type == ObjectTypeAttributeKey.EDGE_COLLECTION)
            {
                if (!NodeSchema.EdgeCollectionDefinitions.ContainsKey(attr.name))
                    NodeSchema.EdgeCollectionDefinitions[attr.name] = new EdgeCollectionDefinition(
                        attr.name,
                        objectTypeNode.Tag
                    );
            }
        }
    }
}

public class ObjectTypeAttribute : NodeIntermediary
{
    public string name { get; set; }
    public string type { get; set; }
    public string? object_type { get; set; }

    public async override Task<Node> ToNodeAsync(Cursor cursor)
    {
        var objectTypeAttributeRep = new NodeRep("ObjectTypeAttribute");
        objectTypeAttributeRep.Fields.StringFields = new Dictionary<string, string?>
        {
            { ObjectTypeAttributeKey.NAME, name },
            { ObjectTypeAttributeKey.TYPE, type },
            { ObjectTypeAttributeKey.OBJECT_TYPE, object_type }
        };

        var node = Node.FromRep(objectTypeAttributeRep);
        node.Cursor = cursor;
        await node.SaveAsync();
        Guid = node.Guid;
        return node;
    }
}