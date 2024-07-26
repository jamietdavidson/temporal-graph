namespace Graph.Api;

public interface IGraphRelationshipOperation : IOperation
{
    string Key { get; set; }

    public List<Guid> GetProvidedNodeGuids();
}

public class GraphRelationshipOperationType : InterfaceType<IGraphRelationshipOperation>
{
    protected override void Configure(
        IInterfaceTypeDescriptor<IGraphRelationshipOperation> descriptor)
    {
        descriptor.Name("GraphRelationshipOperation")
            .Implements<OperationType>();
    }
}