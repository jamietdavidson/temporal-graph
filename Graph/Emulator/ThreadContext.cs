using Graph.Core;

namespace Graph.Mongo;

public class ThreadContext
{
    private StateMapExecutor _stateMap;

    private Dictionary<Guid, Node> _threadsByTemplateThreadGroupId;

    private Dictionary<Guid, Node> _templateThreadGroupsByThreadId;

    public LinkedList<Guid> ThreadPath { get; set; }

    public LinkedList<Guid> TemplateThreadGroupPath { get; set; }

    public Node Thread
    {
        get
        {
            return TemplateThreadGroupPath.Count > 0
                ? GetThread(TemplateThreadGroupPath.First!.Value)
                : _stateMap.Node;
        }
    }

    public Node? TemplateThreadGroup
    {
        get
        {
            return GetTemplateThreadGroup(ThreadPath.First!.Value);
        }
    }

    // Note that in its simplest form, a ThreadContext instance might include only the StateMap.Node.
    public ThreadContext(StateMapExecutor stateMap, Node thread)
    {
        _stateMap = stateMap;
        ThreadPath = ThreadUtils.ThreadPathAsLinkedListAsync(thread).Result;

        if (!ThreadPath.Contains(_stateMap.Node.Guid))
            ThreadPath.AddLast(_stateMap.Node.Guid);

        TemplateThreadGroupPath = ThreadUtils.ThreadPathAsLinkedListAsync(thread, "template_thread_group").Result;
        _PopulateLookupsAsync(stateMap).Wait();
    }

    public Node GetThread(Guid? templateThreadGroupId)
    {
        if (templateThreadGroupId == null || !_threadsByTemplateThreadGroupId.ContainsKey((Guid)templateThreadGroupId))
            return _stateMap.Node;

        return _threadsByTemplateThreadGroupId[(Guid)templateThreadGroupId];
    }

    public Node? GetTemplateThreadGroup(Guid threadId)
    {
        if (threadId == _stateMap.Node.Guid)
            return null;

        return _templateThreadGroupsByThreadId[threadId];
    }

    private async Task _PopulateLookupsAsync(StateMapExecutor stateMap)
    {
        _threadsByTemplateThreadGroupId = new Dictionary<Guid, Node>();
        _templateThreadGroupsByThreadId = new Dictionary<Guid, Node>();
        foreach (var threadId in ThreadPath)
        {
            if (threadId == stateMap.Node.Guid) break;

            var thread = stateMap.Cursor!.NodeGuids[threadId];
            var templateThreadGroup = (Node?)await thread.GetValueAsync("template_thread_group");

            if (templateThreadGroup == null)
                throw new Exception("Could not resolve template thread group from thread.");

            _threadsByTemplateThreadGroupId[templateThreadGroup.Guid] = thread;
            _templateThreadGroupsByThreadId[thread.Guid] = templateThreadGroup;
        }
        _templateThreadGroupsByThreadId[_stateMap.Node.Guid] = _stateMap.Node;
    }
}