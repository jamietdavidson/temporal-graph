using Graph.Core;

namespace Graph.Api;

public class Node
{
    private readonly Core.Node _node;

    public Node(Core.Node node)
    {
        _node = node;

        if (!_node.IsLoaded)
            throw new GraphQLException("Lazy loading not implemented.");

        Id = node.Guid;
        Tag = node.Tag;
        Timestamp = node.Timestamp;
    }

    public Guid Id { get; set; }
    public string Tag { get; set; }
    public ulong Timestamp { get; set; }

    public async Task<List<ulong>> GetTimestampsAsync()
    {
        return await _node.Cursor!.GetNodeTimestampsAsync(_node.Guid);
    }

    public async Task<List<StringFieldData>> GetStringFieldsAsync()
    {
        var tasks = new List<Task<StringFieldData>>();
        foreach (var field in _node.GetFieldTypes())
            if (field.Value == typeof(Core.String))
                tasks.Add(StringFieldData.NewFieldDataAsync(field.Key, _node));

        await Task.WhenAll(tasks);

        var fields = new List<StringFieldData>();
        foreach (var task in tasks)
            fields.Add(task.Result);

        return fields;
    }

    public async Task<List<NumericFieldData>> GetNumericFieldsAsync()
    {
        var tasks = new List<Task<NumericFieldData>>();
        foreach (var field in _node.GetFieldTypes())
            if (field.Value == typeof(Core.Decimal))
                tasks.Add(NumericFieldData.NewFieldDataAsync(field.Key, _node));

        await Task.WhenAll(tasks);

        var fields = new List<NumericFieldData>();
        foreach (var task in tasks)
            fields.Add(task.Result);

        return fields;
    }

    public async Task<List<BooleanFieldData>> GetBooleanFieldsAsync()
    {
        var tasks = new List<Task<BooleanFieldData>>();
        foreach (var field in _node.GetFieldTypes())
            if (field.Value == typeof(Core.Boolean))
                tasks.Add(BooleanFieldData.NewFieldDataAsync(field.Key, _node));

        await Task.WhenAll(tasks);

        var fields = new List<BooleanFieldData>();
        foreach (var task in tasks)
            fields.Add(task.Result);

        return fields;
    }

    public async Task<List<StringListFieldData>> GetStringListFieldsAsync()
    {
        var tasks = new List<Task<StringListFieldData>>();
        foreach (var field in _node.GetFieldTypes())
            if (field.Value == typeof(StringList))
                tasks.Add(StringListFieldData.NewFieldDataAsync(field.Key, _node));

        await Task.WhenAll(tasks);

        var fields = new List<StringListFieldData>();
        foreach (var task in tasks)
            fields.Add(task.Result);

        return fields;
    }

    public async Task<List<NumericListFieldData>> GetNumericListFieldsAsync()
    {
        var tasks = new List<Task<NumericListFieldData>>();
        foreach (var field in _node.GetFieldTypes())
            if (field.Value == typeof(NumericList))
                tasks.Add(NumericListFieldData.NewFieldDataAsync(field.Key, _node));

        await Task.WhenAll(tasks);

        var fields = new List<NumericListFieldData>();
        foreach (var task in tasks)
            fields.Add(task.Result);

        return fields;
    }

    public async Task<List<BooleanListFieldData>> GetBooleanListFieldsAsync()
    {
        var tasks = new List<Task<BooleanListFieldData>>();
        foreach (var field in _node.GetFieldTypes())
            if (field.Value == typeof(BooleanList))
                tasks.Add(BooleanListFieldData.NewFieldDataAsync(field.Key, _node));

        await Task.WhenAll(tasks);

        var fields = new List<BooleanListFieldData>();
        foreach (var task in tasks)
            fields.Add(task.Result);

        return fields;
    }

    public async Task<List<Edge>> GetEdgesAsync()
    {
        var tasks = new Dictionary<string, Task<object?>>();
        foreach (var edge in _node.GetEdgeTypes())
            tasks.Add(edge.Key, _node.GetValueAsync(edge.Key));

        await Task.WhenAll(tasks.Values);

        var edges = new List<Edge>();
        foreach (var task in tasks)
        {
            if (task.Value.Result == null) continue;
            edges.Add(new Edge(task.Key, (Core.Node)task.Value.Result));
        }

        return edges;
    }

    public async Task<List<EdgeCollection>> GetEdgeCollectionsAsync()
    {
        var tasks = new Dictionary<string, Task<object?>>();
        foreach (var edgeCollection in _node.GetEdgeCollectionTypes())
            tasks.Add(edgeCollection.Key, _node.GetValueAsync(edgeCollection.Key));

        await Task.WhenAll(tasks.Values);

        var edgeCollections = new List<EdgeCollection>();
        foreach (var task in tasks)
        {
            if (task.Value.Result == null) continue;

            edgeCollections.Add(new EdgeCollection(task.Key,
                (EdgeCollection<Core.Node, Core.Node>)task.Value.Result));
        }

        return edgeCollections;
    }
}

public class NodeType : ObjectType<Node>
{
    protected override void Configure(IObjectTypeDescriptor<Node> descriptor)
    {
        descriptor
            .Field("stringFields")
            .Type<ListType<StringFieldDataType>>()
            .Resolve(async context => await context.Parent<Node>().GetStringFieldsAsync());

        descriptor
            .Field("numericFields")
            .Type<ListType<NumericFieldDataType>>()
            .Resolve(async context => await context.Parent<Node>().GetNumericFieldsAsync());

        descriptor
            .Field("booleanFields")
            .Type<ListType<BooleanFieldDataType>>()
            .Resolve(async context => await context.Parent<Node>().GetBooleanFieldsAsync());

        descriptor
            .Field("stringListFields")
            .Type<ListType<StringListFieldDataType>>()
            .Resolve(async context => await context.Parent<Node>().GetStringListFieldsAsync());

        descriptor
            .Field("numericListFields")
            .Type<ListType<NumericListFieldDataType>>()
            .Resolve(async context => await context.Parent<Node>().GetNumericListFieldsAsync());

        descriptor
            .Field("booleanListFields")
            .Type<ListType<BooleanListFieldDataType>>()
            .Resolve(async context => await context.Parent<Node>().GetBooleanListFieldsAsync());

        descriptor
            .Field("edges")
            .Type<ListType<EdgeType>>()
            .Resolve(async context => await context.Parent<Node>().GetEdgesAsync());

        descriptor
            .Field("edgeCollections")
            .Type<ListType<EdgeCollectionType>>()
            .Resolve(async context => await context.Parent<Node>().GetEdgeCollectionsAsync());

        descriptor
            .Field("timestamps")
            .Type<ListType<UnsignedLongType>>()
            .Resolve(async context => await context.Parent<Node>().GetTimestampsAsync());
    }
}

public class StringFieldData : IFieldData
{
    public string Key { get; set; }

    public string? Value { get; set; }

    public static async Task<StringFieldData> NewFieldDataAsync(string fieldName, Core.Node node)
    {
        return new StringFieldData
        {
            Key = fieldName,
            Value = (string?)await node.GetValueAsync(fieldName)
        };
    }
}

public class StringFieldDataType : ObjectType<StringFieldData>
{
}

public class NumericFieldData : IFieldData
{
    public string Key { get; set; }

    // Strings are used here to avoid precision loss,
    // but the values are still stored in the graph as decimals.
    public string? Value { get; set; }

    public static async Task<NumericFieldData> NewFieldDataAsync(string fieldName, Core.Node node)
    {
        return new NumericFieldData
        {
            Key = fieldName,
            Value = (await node.GetValueAsync(fieldName))?.ToString()
        };
    }
}

public class NumericFieldDataType : ObjectType<NumericFieldData>
{
}

public class BooleanFieldData : IFieldData
{
    public string Key { get; set; }

    public bool? Value { get; set; }

    public static async Task<BooleanFieldData> NewFieldDataAsync(string fieldName, Core.Node node)
    {
        return new BooleanFieldData
        {
            Key = fieldName,
            Value = (bool?)await node.GetValueAsync(fieldName)
        };
    }
}

public class BooleanFieldDataType : ObjectType<BooleanFieldData>
{
}

public class StringListFieldData : IFieldData
{
    public string Key { get; set; }

    public List<string?>? Value { get; set; }

    public static async Task<StringListFieldData> NewFieldDataAsync(string fieldName, Core.Node node)
    {
        return new StringListFieldData
        {
            Key = fieldName,
            Value = (List<string?>?)await node.GetValueAsync(fieldName)
        };
    }
}

public class StringListFieldDataType : ObjectType<StringListFieldData>
{
}

public class NumericListFieldData : IFieldData
{
    public string Key { get; set; }

    // Strings are used here to avoid precision loss,
    // but the values are still stored in the graph as decimals.
    public List<string?>? Value { get; set; }

    public static async Task<NumericListFieldData> NewFieldDataAsync(string fieldName, Core.Node node)
    {
        var decimals = (List<decimal?>?)await node.GetValueAsync(fieldName);
        return new NumericListFieldData
        {
            Key = fieldName,
            Value = decimals?.Select(d => d?.ToString()).ToList()
        };
    }
}

public class NumericListFieldDataType : ObjectType<NumericListFieldData>
{
}

public class BooleanListFieldData : IFieldData
{
    public string Key { get; set; }

    public List<bool?>? Value { get; set; }

    public static async Task<BooleanListFieldData> NewFieldDataAsync(string fieldName, Core.Node node)
    {
        return new BooleanListFieldData
        {
            Key = fieldName,
            Value = (List<bool?>?)await node.GetValueAsync(fieldName)
        };
    }
}

public class BooleanListFieldDataType : ObjectType<BooleanListFieldData>
{
}