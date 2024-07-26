using Graph.Core;

namespace Graph.Mongo;


public class TemplateContext
{
    public TemplateContext? ParentContext { get; set; }
    public Node? TemplateThreadGroup { get; set; }
}


public class RuntimeContext
{

    public TemplateContext? TemplateContext { get; }

    public RuntimeContext? ParentContext { get; }

    public StateMapExecutor StateMapExecutor { get; }

    public Node? Thread { get; }

    private RuntimeContext(StateMapExecutor stateMapExecutor, Node? thread, Node? templateThreadGroup, RuntimeContext? parentContext = null)
    {
        StateMapExecutor = stateMapExecutor;
        Thread = thread;
        ParentContext = parentContext;
        TemplateContext = new TemplateContext()
        {
            ParentContext = parentContext?.TemplateContext,
            TemplateThreadGroup = templateThreadGroup
        };
    }

    public static async Task<RuntimeContext> FromThreadAsync(StateMapExecutor stateMapExecutor, Node thread)
    {
        var templateThreadGroup = (Node?)await thread.GetValueAsync("template_thread_group");

        var parentThread = (Node?)await thread.GetValueAsync("parent_thread");

        RuntimeContext? parentContext;
        if (parentThread != null)
        {
            parentContext = await FromThreadAsync(stateMapExecutor, parentThread);
        }
        else
        {
            parentContext = new RuntimeContext(stateMapExecutor, null, null, null);
        }

        return new RuntimeContext(stateMapExecutor, thread, templateThreadGroup, parentContext);
    }

    public static async Task<RuntimeContext> FromTemplateThreadGroupAsync(StateMapExecutor stateMapExecutor, Node templateThreadGroup)
    {

        var parentThread = (Node?)await templateThreadGroup.GetValueAsync("parent_thread");

        RuntimeContext? parentContext;
        if (parentThread != null)
        {
            parentContext = await FromThreadAsync(stateMapExecutor, parentThread);
        }
        else
        {
            parentContext = new RuntimeContext(stateMapExecutor, null, null, null);
        }

        return new RuntimeContext(stateMapExecutor, null, templateThreadGroup, parentContext);
    }
}