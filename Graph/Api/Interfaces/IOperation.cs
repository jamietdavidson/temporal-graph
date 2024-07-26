using Graph.Core;

namespace Graph.Api;

public interface IOperation
{
    Guid ActionId { get; set; }

    Task Execute(Cursor cursor);
}

public class OperationType : InterfaceType<IOperation>
{
    protected override void Configure(
        IInterfaceTypeDescriptor<IOperation> descriptor)
    {
        descriptor.Name("Operation");
    }
}

public interface IOperationWithNodeGuid : IOperation
{
    Guid NodeId { get; set; }
}

public class OperationWithNodeGuidType : InterfaceType<IOperationWithNodeGuid>
{
    protected override void Configure(
        IInterfaceTypeDescriptor<IOperationWithNodeGuid> descriptor)
    {
        descriptor.Name("OperationWithNodeGuid");
    }
}

public interface IOperationWithFieldData : IOperation
{
    List<StringFieldData>? StringFields { get; set; }

    List<NumericFieldData>? NumericFields { get; set; }

    List<BooleanFieldData>? BooleanFields { get; set; }

    List<StringListFieldData>? StringListFields { get; set; }

    List<NumericListFieldData>? NumericListFields { get; set; }

    List<BooleanListFieldData>? BooleanListFields { get; set; }
}

public interface IOperationWithThreadId : IOperation
{
    Guid? ThreadId { get; set; }
}