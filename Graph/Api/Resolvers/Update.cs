using Graph.Core;

namespace Graph.Api;

public class Update : IOperationWithNodeGuid, IOperationWithFieldData, IOperationWithThreadId
{
    public Guid ActionId { get; set; }

    public Guid? ThreadId { get; set; }

    public List<StringFieldData>? StringFields { get; set; }

    public List<NumericFieldData>? NumericFields { get; set; }

    public List<BooleanFieldData>? BooleanFields { get; set; }

    public List<StringListFieldData>? StringListFields { get; set; }

    public List<NumericListFieldData>? NumericListFields { get; set; }

    public List<BooleanListFieldData>? BooleanListFields { get; set; }

    public Guid NodeId { get; set; }

    public async Task Execute(Cursor cursor)
    {
        if (!cursor.NodeGuids.ContainsKey(NodeId))
            throw new GraphQLException("Node id does not exist: " + NodeId);

        var node = cursor.NodeGuids[NodeId];

        try
        {
            var tasks = new List<Task>();
            foreach (var field in StringFields ?? new List<StringFieldData>())
                tasks.Add(node.SetValueAsync(field.Key, field.Value));

            foreach (var field in NumericFields ?? new List<NumericFieldData>())
                tasks.Add(node.SetValueAsync(field.Key, Utils.ToDecimal(field.Value, field.Key)));

            foreach (var field in BooleanFields ?? new List<BooleanFieldData>())
                tasks.Add(node.SetValueAsync(field.Key, field.Value));

            foreach (var field in StringListFields ?? new List<StringListFieldData>())
                tasks.Add(node.SetValueAsync(field.Key, field.Value));

            foreach (var field in NumericListFields ?? new List<NumericListFieldData>())
                tasks.Add(node.SetValueAsync(field.Key, Utils.ToDecimalList(field.Value, field.Key)));

            foreach (var field in BooleanListFields ?? new List<BooleanListFieldData>())
                tasks.Add(node.SetValueAsync(field.Key, field.Value));

            tasks.Add(node.SaveAsync());

            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            throw new GraphQLException(ex.Message);
        }
    }
}

public class UpdateType : ObjectType<Update>
{
    protected override void Configure(
        IObjectTypeDescriptor<Update> descriptor)
    {
        descriptor.Name("Update");
        descriptor.Implements<OperationWithNodeGuidType>();
    }
}