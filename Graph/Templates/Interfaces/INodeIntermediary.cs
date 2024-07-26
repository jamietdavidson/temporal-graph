using Graph.Core;

namespace Graph.Templates;

public interface INodeIntermediary
{
    Task<Node> ToNodeAsync(Cursor cursor);
    Task ResolveReferencesAsync(TemplateStateMap template, Cursor cursor);
}

public abstract class NodeIntermediary : INodeIntermediary
{
    public Guid? Guid { get; set; }
    public abstract Task<Node> ToNodeAsync(Cursor cursor);

    public Task ResolveReferencesAsync(TemplateStateMap template, Cursor cursor)
    {
        return Task.CompletedTask;
    }
}