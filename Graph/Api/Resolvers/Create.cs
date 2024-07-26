using System.Reflection;
using Graph.Core;

namespace Graph.Api;

public class Create : IOperation, IOperationWithFieldData, IOperationWithThreadId
{
    private Core.Node? _newNode;

    public Guid ActionId { get; set; }

    public Guid? ThreadId { get; set; }

    public string Tag { get; set; }

    public Guid? Id { get; set; }

    public List<StringFieldData>? StringFields { get; set; }

    public List<NumericFieldData>? NumericFields { get; set; }

    public List<BooleanFieldData>? BooleanFields { get; set; }

    public List<StringListFieldData>? StringListFields { get; set; }

    public List<NumericListFieldData>? NumericListFields { get; set; }

    public List<BooleanListFieldData>? BooleanListFields { get; set; }

    public async Task Execute(Cursor cursor)
    {
        try
        {
            await GetInstance(cursor).SaveAsync();
        }
        catch (Exception ex)
        {
            throw new GraphQLException(ex.Message);
        }
    }

    public Guid GetNodeGuid()
    {
        if (_newNode == null)
            throw new GraphQLException("Cannot access Guid of new node before instantiation.");

        return _newNode.Guid;
    }

    public Core.Node GetInstance(Cursor cursor)
    {
        if (_newNode is Core.Node) return _newNode;

        var nodeRep = new NodeRep();

        try
        {
            foreach (var field in BooleanFields ?? new List<BooleanFieldData>())
                nodeRep.Fields.BooleanFields.Add(field.Key, field.Value);

            foreach (var field in StringFields ?? new List<StringFieldData>())
                nodeRep.Fields.StringFields.Add(field.Key, field.Value);

            foreach (var field in NumericFields ?? new List<NumericFieldData>())
                nodeRep.Fields.NumericFields.Add(field.Key, Utils.ToDecimal(field.Value, field.Key));

            foreach (var field in BooleanListFields ?? new List<BooleanListFieldData>())
                nodeRep.Fields.BooleanListFields.Add(field.Key, field.Value);

            foreach (var field in StringListFields ?? new List<StringListFieldData>())
                nodeRep.Fields.StringListFields.Add(field.Key, field.Value);

            foreach (var field in NumericListFields ?? new List<NumericListFieldData>())
                nodeRep.Fields.NumericListFields.Add(field.Key, Utils.ToDecimalList(field.Value, field.Key));

            _newNode = new Core.Node(
                Id,
                null,
                nodeRep,
                null,
                Tag
            );
        }
        catch (TargetInvocationException ex)
        {
            var InnerException = ex.InnerException;
            throw new GraphQLException(ex.InnerException != null ? ex.InnerException.Message : ex.Message);
        }
        catch (Exception ex)
        {
            throw new GraphQLException(ex.Message);
        }

        _newNode.Cursor = cursor;
        return _newNode;
    }
}

public class CreateType : ObjectType<Create>
{
    protected override void Configure(
        IObjectTypeDescriptor<Create> descriptor)
    {
        descriptor.Name("Create")
            .Implements<OperationType>();
    }
}