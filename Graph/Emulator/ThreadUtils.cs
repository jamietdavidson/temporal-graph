using Graph.Core;

namespace Graph.Mongo;

public static class ThreadUtils
{
    public static async Task<LinkedList<Guid>> ThreadPathAsLinkedListAsync(
        Node toThread,
        string parentPropName = "parent_thread"
    )
    {
        var threadPath = new LinkedList<Guid>();
        var thread = toThread;
        while (thread != null)
        {
            threadPath.AddLast(thread.Guid);
            thread = (Node?)await thread.GetValueAsync(parentPropName);
        }
        return threadPath;
    }

    public static async Task<bool> ThreadedActionIsAvailableAsync(
        StateMapExecutor stateMap,
        Node templateAction,
        Node threadGroup,
        Node? thread,
        Node parentContext
    )
    {
        var templateThreadGroup = (Node?)await templateAction.GetValueAsync("thread_group");
        if (templateThreadGroup == null)
            throw new Exception("Action is not threaded.");

        var threadPath = await ThreadPathAsLinkedListAsync(thread ?? parentContext);

        // Is the thread group's dependency satisfied?
        if (thread == null)
        {
            var threadGroupCheckpoint = (Node?)await threadGroup.GetValueAsync("depends_on");
            if (threadGroupCheckpoint != null && !await CheckpointIsSatisfiedAsync(stateMap, threadPath, threadGroupCheckpoint))
                return false;
        }
        // else the thread already exists, so its dependencies must be satisfied.

        // Is the action's dependency satisfied?
        var actionCheckpoint = (Node?)await templateAction.GetValueAsync("checkpoint");
        if (actionCheckpoint != null && !await CheckpointIsSatisfiedAsync(stateMap, threadPath, actionCheckpoint))
            return false;

        // Is the action available?
        return thread == null || (await Utils.GetEdgeCollectionNodesAsync(thread, "available_actions")).Contains(templateAction);
    }

    public static async Task<bool> ThreadGroupIsAvailableAsync(
        Node templateThreadGroup,
        StateMapExecutor stateMap,
        Node? parentThread
    )
    {
        if (parentThread == null)
        {
            // The parent context is the state map
            return (
                await Utils.GetEdgeCollectionNodesAsync(stateMap.Node, "satisfied_checkpoints")
            ).Contains(templateThreadGroup);
        }

        var threadGroupCheckpoint = (Node?)await templateThreadGroup.GetValueAsync("depends_on");
        return threadGroupCheckpoint == null || await CheckpointIsSatisfiedAsync(
            stateMap,
            threadPath: await ThreadPathAsLinkedListAsync(parentThread),
            threadGroupCheckpoint
        );
    }

    // Move to CONTEXT
    public static async Task<bool> CheckpointIsSatisfiedAsync(StateMapExecutor stateMap, LinkedList<Guid> threadPath, Node checkpoint)
    {
        var checkpointContext = (Node?)await checkpoint.GetValueAsync("template_thread_group");
        Node? checkpointThread = null;
        if (checkpointContext == null)
            checkpointThread = stateMap.Node;
        else
        {
            for (var item = threadPath.First; item != null; item = item.Next)
            {
                if (!stateMap.Cursor!.NodeGuids.ContainsKey(item.Value))
                    throw new Exception("Thread path contains a node that is not part of the state map.");

                var thread = stateMap.Cursor.NodeGuids[item.Value];
                var templateThreadGroup = (Node)(await thread.GetValueAsync("template_thread_group"))!;
                if (checkpointContext.Guid == templateThreadGroup.Guid)
                {
                    checkpointThread = thread;
                    break;
                }
            }

            if (checkpointThread == null)
                throw new Exception("Could not resolve checkpoint context as it is not part of the thread path.");
        }

        return (
            await Utils.GetEdgeCollectionNodesAsync(checkpointThread, "satisfied_checkpoints")
        ).Contains(checkpoint);
    }
}