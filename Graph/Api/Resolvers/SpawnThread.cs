using Graph.Core;

namespace Graph.Api;

public class SpawnThread : IOperation
{
    public Guid ActionId { get; set; }

    // TODO: use enum for variable type
    public string VariableType { get; set; }

    public string? StringVariableValue { get; set; }

    public string? NumericVariableValue { get; set; }

    public bool? BooleanVariableValue { get; set; }

    public Guid? ReferenceVariableValue { get; set; }

    public async Task Execute(Cursor cursor)
    {
        // Nothing to do here.
        // The mongo will spawn the thread based on
        // the other operations passed to the mutation which share the ActionId.
        await Task.CompletedTask;
    }
}

public class SpawnThreadType : ObjectType<SpawnThread>
{
    protected override void Configure(
        IObjectTypeDescriptor<SpawnThread> descriptor)
    {
        descriptor.Name("SpawnThread")
            .Implements<OperationType>();
    }
}