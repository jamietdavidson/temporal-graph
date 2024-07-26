namespace Graph.Core;

public class SchemaDefinition
{
    // timestamp?

    public Dictionary<string, NodeSchema> NodeSchemas = new();

    // Serialize

    // Deserialize

    public SchemaDefinition(List<NodeSchema>? nodeSchemas = null)
    {
        if (nodeSchemas == null) return;

        foreach (var nodeSchema in nodeSchemas)
            AddNodeSchema(nodeSchema);
    }

    public void AddNodeSchema(NodeSchema nodeSchema)
    {
        if (NodeSchemas.ContainsKey(nodeSchema.Tag))
            throw new Exception($"Duplicate tag provided for node schema definition: \"{nodeSchema.Tag}\"");

        NodeSchemas.Add(nodeSchema.Tag, nodeSchema);
    }

    public static void AssertAreEquivalent(SchemaDefinition schemaA, SchemaDefinition? schemaB)
    {
        Assert.That(schemaA.NodeSchemas.Count, Is.EqualTo(schemaB?.NodeSchemas.Count));
        foreach (var nodeSchemaA in schemaA.NodeSchemas)
        {
            if (!schemaB!.NodeSchemas.TryGetValue(nodeSchemaA.Key, out var nodeSchemaB))
                Assert.Fail($"Schema B does not contain a node schema for tag \"{nodeSchemaA.Key}\"");

            CollectionAssert.AreEquivalent(nodeSchemaA.Value.GetFieldTypes(), nodeSchemaB!.GetFieldTypes());
            CollectionAssert.AreEquivalent(nodeSchemaA.Value.GetEdgeTypes(), nodeSchemaB.GetEdgeTypes());
            CollectionAssert.AreEquivalent(nodeSchemaA.Value.GetEdgeCollectionTypes(), nodeSchemaB.GetEdgeCollectionTypes());
        }
    }
}

public class NodeSchema
{
    private HashSet<string> _keys = new();

    public static NodeSchema FromNodeRep(NodeRep nodeRep)
    {
        var fieldTypes = new Dictionary<string, Type>();
        foreach (var booleanField in nodeRep.Fields.BooleanFields)
            fieldTypes.Add(booleanField.Key, typeof(Boolean));

        foreach (var numericField in nodeRep.Fields.NumericFields)
            fieldTypes.Add(numericField.Key, typeof(Decimal));

        foreach (var stringField in nodeRep.Fields.StringFields)
            fieldTypes.Add(stringField.Key, typeof(String));

        foreach (var booleanListField in nodeRep.Fields.BooleanListFields)
            fieldTypes.Add(booleanListField.Key, typeof(BooleanList));

        foreach (var numericListField in nodeRep.Fields.NumericListFields)
            fieldTypes.Add(numericListField.Key, typeof(NumericList));

        foreach (var stringListField in nodeRep.Fields.StringListFields)
            fieldTypes.Add(stringListField.Key, typeof(StringList));

        var edgeTypes = new Dictionary<string, string>();
        foreach (var edge in nodeRep.Edges.Tags)
            edgeTypes.Add(edge.Key, edge.Value);

        var edgeCollectionTypes = new Dictionary<string, string>();
        foreach (var edgeCollection in nodeRep.EdgeCollections.Tags)
            edgeCollectionTypes.Add(edgeCollection.Key, edgeCollection.Value);

        return new NodeSchema(nodeRep.Meta.Tag, fieldTypes, edgeTypes, edgeCollectionTypes);
    }

    public NodeSchema(string tag,
        Dictionary<string, Type>? fieldTypes = null,
        Dictionary<string, Type>? edgeTypes = null,
        Dictionary<string, Type>? edgeCollectionTypes = null,
        bool isDeletable = true
    )
    {
        Tag = tag;
        IsDeletable = isDeletable;
        _AddFieldTypes(fieldTypes);

        if (edgeTypes != null)
        {
            foreach (var edgeType in edgeTypes)
            {
                _RegisterKey(edgeType.Key);
                EdgeDefinitions.Add(edgeType.Key, new EdgeDefinition(edgeType.Key, edgeType.Value));
            }
        }

        if (edgeCollectionTypes != null)
        {
            foreach (var edgeCollectionType in edgeCollectionTypes)
            {
                _RegisterKey(edgeCollectionType.Key);
                EdgeCollectionDefinitions.Add(edgeCollectionType.Key,
                    new EdgeCollectionDefinition(edgeCollectionType.Key, edgeCollectionType.Value));
            }
        }
    }

    public NodeSchema(string tag,
        Dictionary<string, Type>? fieldTypes = null,
        Dictionary<string, string>? edgeTypes = null,
        Dictionary<string, string>? edgeCollectionTypes = null,
        bool isDeletable = true
    )
    {
        Tag = tag;
        IsDeletable = isDeletable;
        _AddFieldTypes(fieldTypes);

        if (edgeTypes != null)
        {
            foreach (var edgeType in edgeTypes)
            {
                _RegisterKey(edgeType.Key);
                EdgeDefinitions.Add(edgeType.Key, new EdgeDefinition(edgeType.Key, edgeType.Value));
            }
        }

        if (edgeCollectionTypes != null)
        {
            foreach (var edgeCollectionType in edgeCollectionTypes)
            {
                _RegisterKey(edgeCollectionType.Key);
                EdgeCollectionDefinitions.Add(edgeCollectionType.Key,
                    new EdgeCollectionDefinition(edgeCollectionType.Key, edgeCollectionType.Value));
            }
        }
    }

    public string Tag { get; init; }

    public bool IsDeletable { get; init; } = true;

    public Dictionary<string, BooleanFieldDefinition> BooleanFieldDefinitions = new();

    public Dictionary<string, NumericFieldDefinition> NumericFieldDefinitions = new();

    public Dictionary<string, StringFieldDefinition> StringFieldDefinitions = new();

    public Dictionary<string, BooleanListFieldDefinition> BooleanListFieldDefinitions = new();

    public Dictionary<string, NumericListFieldDefinition> NumericListFieldDefinitions = new();

    public Dictionary<string, StringListFieldDefinition> StringListFieldDefinitions = new();

    public Dictionary<string, EdgeDefinition> EdgeDefinitions = new();

    public Dictionary<string, EdgeCollectionDefinition> EdgeCollectionDefinitions = new();

    public Dictionary<string, Type> GetFieldTypes()
    {
        var fieldTypes = new Dictionary<string, Type>();
        foreach (var booleanField in BooleanFieldDefinitions)
            fieldTypes.Add(booleanField.Key, typeof(Boolean));
        foreach (var numericField in NumericFieldDefinitions)
            fieldTypes.Add(numericField.Key, typeof(Decimal));
        foreach (var stringField in StringFieldDefinitions)
            fieldTypes.Add(stringField.Key, typeof(String));
        foreach (var booleanListField in BooleanListFieldDefinitions)
            fieldTypes.Add(booleanListField.Key, typeof(BooleanList));
        foreach (var numericListField in NumericListFieldDefinitions)
            fieldTypes.Add(numericListField.Key, typeof(NumericList));
        foreach (var stringListField in StringListFieldDefinitions)
            fieldTypes.Add(stringListField.Key, typeof(StringList));
        return fieldTypes;
    }

    public Dictionary<string, Type> GetEdgeTypes()
    {
        var edgeTypes = new Dictionary<string, Type>();
        foreach (var edge in EdgeDefinitions)
            edgeTypes.Add(edge.Key, typeof(Edge<Node, Node>));
        return edgeTypes;
    }

    public Dictionary<string, Type> GetEdgeCollectionTypes()
    {
        var edgeCollectionTypes = new Dictionary<string, Type>();
        foreach (var edgeCollection in EdgeCollectionDefinitions)
            edgeCollectionTypes.Add(edgeCollection.Key, typeof(EdgeCollection<Node, Node>));
        return edgeCollectionTypes;
    }

    private void _AddFieldTypes(Dictionary<string, Type>? fieldTypes)
    {
        if (fieldTypes != null)
        {
            foreach (var fieldType in fieldTypes)
            {
                _RegisterKey(fieldType.Key);

                var baseType = fieldType.Value.BaseType;
                if (baseType?.Name != "Scalar`1")
                    throw new Exception($"Field type must be a scalar type: \"{baseType?.Name}\"");

                var scalarType = baseType.GetGenericArguments()[0];
                if (scalarType == typeof(bool?))
                    BooleanFieldDefinitions.Add(fieldType.Key, new BooleanFieldDefinition(fieldType.Key));
                else if (scalarType == typeof(int?) || scalarType == typeof(decimal?))
                    NumericFieldDefinitions.Add(fieldType.Key, new NumericFieldDefinition(fieldType.Key));
                else if (scalarType == typeof(string))
                    StringFieldDefinitions.Add(fieldType.Key, new StringFieldDefinition(fieldType.Key));
                else if (scalarType == typeof(List<bool?>))
                    BooleanListFieldDefinitions.Add(fieldType.Key, new BooleanListFieldDefinition(fieldType.Key));
                else if (scalarType == typeof(List<decimal?>))
                    NumericListFieldDefinitions.Add(fieldType.Key, new NumericListFieldDefinition(fieldType.Key));
                else if (scalarType == typeof(List<string?>))
                    StringListFieldDefinitions.Add(fieldType.Key, new StringListFieldDefinition(fieldType.Key));
                else
                    throw new Exception("Unrecognized scalar type: " + scalarType.Name);
            }
        }
    }

    private void _RegisterKey(string key)
    {
        if (_keys.Contains(key))
            throw new Exception($"Duplicate key provided for type definition: \"{key}\"");

        _keys.Add(key);
    }
}

public class BooleanFieldDefinition
{
    public BooleanFieldDefinition(string key)
    {
        Key = key;
    }

    public string Key { get; init; }
}

public class NumericFieldDefinition
{
    public NumericFieldDefinition(string key)
    {
        Key = key;
    }

    public string Key { get; init; }

    public bool Required { get; init; }

    public int? Min { get; init; }

    public int? Max { get; init; }

    public int? Precision { get; init; }
}

public class StringFieldDefinition
{
    public StringFieldDefinition(string key)
    {
        Key = key;
    }

    public string Key { get; init; }

    public bool Required { get; init; }

    public int? MaxLength { get; init; }
}

public class BooleanListFieldDefinition
{
    public BooleanListFieldDefinition(string key)
    {
        Key = key;
    }

    public string Key { get; init; }

    public bool Required { get; init; }

    public int? MaxLength { get; init; }
}

public class NumericListFieldDefinition
{
    public NumericListFieldDefinition(string key)
    {
        Key = key;
    }

    public string Key { get; init; }

    public bool Required { get; init; }

    public int? MaxLength { get; init; }
}

public class StringListFieldDefinition
{
    public StringListFieldDefinition(string key)
    {
        Key = key;
    }

    public string Key { get; init; }

    public bool Required { get; init; }

    public int? MaxLength { get; init; }
}

public class EdgeDefinition
{
    public EdgeDefinition(string key, Type type)
    {
        Key = key;

        var gta = type.GetGenericArguments();
        Tag = gta.Count() > 1 ? gta[1].Name : type.Name;
    }

    public EdgeDefinition(string key, string tag)
    {
        Key = key;
        Tag = tag;
    }

    public string Key { get; init; }

    public string Tag { get; init; }
}

public class EdgeCollectionDefinition
{
    public EdgeCollectionDefinition(string key, Type type)
    {
        Key = key;

        var gta = type.GetGenericArguments();
        Tag = gta.Count() > 1 ? gta[1].Name : type.Name;
    }

    public EdgeCollectionDefinition(string key, string tag)
    {
        Key = key;
        Tag = tag;
    }

    public string Key { get; init; }

    public string Tag { get; init; }
}