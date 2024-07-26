using Graph.Api;
using Graph.Core;

namespace Graph.Mongo;

public class ThreadValidator
{
    private StateMapExecutor _stateMap;

    // The same thread variable cannot be used more than once by SpawnThread operations.
    // (not to be confused with duplicate thread variable values, which are not considered the "the same" variable)
    private List<object?> _spawningFromThreadVariables = new();

    public ThreadValidator(StateMapExecutor stateMap)
    {
        _stateMap = stateMap;
    }

    public async Task ValidateThreadSpawnActionAsync(
        Core.Node templateAction,
        Create operation,
        SpawnThread threadSpawnOperation
    )
    {
        var templateThreadGroup = (Core.Node?)await templateAction.GetValueAsync("thread_group");
        if (templateThreadGroup == null)
            throw new Exception($"Cannot spawn thread: action {templateAction.Guid} does not have a threaded context.");

        // Which state should be used to evaluate the action?
        // This could be a parent thread or the state map itself.
        Core.Node parentStateNode;

        // Is it a nested context?
        var parentTemplateThreadGroup = (Core.Node?)await templateThreadGroup.GetValueAsync("thread_group");
        if (parentTemplateThreadGroup != null)
        {
            // Use the parent thread's state...
            // The operation must specify the id of the parent thread.
            if (operation.ThreadId == null || !_stateMap.Cursor!.NodeGuids.ContainsKey((Guid)operation.ThreadId))
                throw new Exception($"Cannot spawn nested thread: no parent ThreadId was specified as part of the Create operation.");

            parentStateNode = _stateMap.Cursor.NodeGuids[(Guid)operation.ThreadId];
        }
        else // Spawning a top-level thread -- use the state map node's state.
            parentStateNode = _stateMap.Node;

        var availableThreadGroups = (EdgeCollection<Core.Node, Core.Node>)(await parentStateNode.GetValueAsync("available_thread_groups"))!;
        if (!availableThreadGroups.Contains(templateThreadGroup.Guid))
            throw new Exception($"ThreadGroup {templateThreadGroup.Guid} is not available.");

        // If the action has an explicit dependency, verify that it is satisfied.
        var templateActionCheckpoint = (Core.Node?)await templateAction.GetValueAsync("depends_on");
        if (templateActionCheckpoint != null)
        {
            EdgeCollection<Core.Node, Core.Node> satisfiedCheckpoints;

            var templateActionCheckpointThreadGroup = (Core.Node?)await templateActionCheckpoint.GetValueAsync("thread_group");
            if (templateActionCheckpointThreadGroup == null)
            {
                satisfiedCheckpoints = (EdgeCollection<Core.Node, Core.Node>)(
                    await _stateMap.Node.GetValueAsync("satisfied_checkpoints")
                )!;
            }
            else if (templateActionCheckpointThreadGroup.Guid == templateThreadGroup.Guid)
            {
                // If the checkpoint shares the same context as the action,
                // then the action is not an entry action for the thread group.
                // Therefore that action cannot be used to spawn a new thread. 
                throw new Exception("Cannot spawn thread using an action that is not an entry action for the thread group.");
            }
            else
            {
                // Recursively search parent contexts for the checkpointContext.
                Core.Node? contextToCheck = parentStateNode;
                var contextGuids = new List<Guid> { parentStateNode.Guid };
                while (true)
                {
                    if (contextToCheck.Guid == templateActionCheckpointThreadGroup.Guid)
                    {
                        satisfiedCheckpoints = (EdgeCollection<Core.Node, Core.Node>)(
                            await contextToCheck.GetValueAsync("satisfied_checkpoints")
                        )!;
                        break;
                    }

                    contextToCheck = (Core.Node?)await contextToCheck.GetValueAsync("context");
                    if (contextToCheck == null || contextGuids.Contains(contextToCheck.Guid))
                        // This should not happen, but just in case, we gotta throw...
                        throw new Exception("Checkpoint context not found: " + templateActionCheckpointThreadGroup.Guid);

                    contextGuids.Add(contextToCheck.Guid); // just to avoid any infinite loop shenanigans
                }
            }

            if (!satisfiedCheckpoints.Contains(templateActionCheckpoint.Guid))
                throw new Exception($"Action {templateAction.Guid} cannot be performed because its dependency checkpoint {templateActionCheckpoint.Guid} is not satisfied.");
        }

        // The specified thread variable must still be available.
        await CheckVariableAvailability(
            parentStateNode,
            templateThreadGroup,
            threadSpawnOperation
        );
    }

    public async Task CheckVariableAvailability(
        Core.Node parentStateNode,
        Core.Node templateThreadGroup,
        SpawnThread threadSpawnOperation
    )
    {
        var availableThreadVariables = await _GetAvailableThreadVariablesAsync(
            templateThreadGroup,
            threadGroup: await _stateMap.GetThreadGroupAsync(
                fromNode: parentStateNode,
                templateThreadGroup.Guid
            )
        );

        object? value;
        switch (threadSpawnOperation.VariableType)
        {
            case "STRING":
                value = threadSpawnOperation.StringVariableValue;
                break;
            case "NUMERIC":
                value = threadSpawnOperation.NumericVariableValue;
                break;
            case "BOOLEAN":
                value = threadSpawnOperation.BooleanVariableValue;
                break;
            case "REFERENCE":
                value = threadSpawnOperation.ReferenceVariableValue;
                break;
            default:
                throw new Exception("Invalid VariableType: " + threadSpawnOperation.VariableType);
        }

        if (!availableThreadVariables.Contains(value))
            // TODO: differentiate between non-existent and taken thread variables
            throw new Exception("The specified thread variable hash does not match any of the available values from the ThreadGroup.spawn.foreach collection.");

        // Keep track of used thread variables when spawning multiple threads.
        _spawningFromThreadVariables.Add(value);
    }

    public async Task ValidateThreadedActionAsync(
        Core.Node templateAction,
        IOperationWithThreadId operation
    )
    {
        if (operation.ThreadId == null)
            throw new Exception("ThreadId is required for threaded actions");

        var thread = _stateMap.Cursor!.NodeGuids[(Guid)operation.ThreadId];

        // Confirm that the thread exists within the state map
        await VerifyThreadPathAsync(thread);

        var stateNode = (Core.Node?)await thread.GetValueAsync("parent_thread") ?? _stateMap.Node;
        var templateThreadGroup = (Core.Node?)await templateAction.GetValueAsync("thread_group");
        if (templateThreadGroup == null)
            throw new Exception("Action does not have a threaded context");

        if (((Core.Node?)await thread.GetValueAsync("template_thread_group"))?.Guid != templateThreadGroup.Guid)
            throw new Exception("Action is not part of the specified thread");

        var availableActions = (EdgeCollection<Core.Node, Core.Node>)(await stateNode.GetValueAsync("available_actions"))!;
        if (!availableActions.Contains(templateAction.Guid))
            throw new Exception("Action not available in the specified thread");
    }

    public async Task VerifyThreadPathAsync(Core.Node thread)
    {
        var threadPath = new List<Core.Node> { thread };
        // Explore parent threads until the non-threaded context is reached
        Core.Node? parentThread = null;
        while (true)
        {
            parentThread = (Core.Node?)await thread.GetValueAsync("parent_thread");
            if (parentThread == null) break;

            threadPath.Add(parentThread);
            thread = parentThread;
        }

        // Include the non-threaded context in the path
        threadPath.Add(_stateMap.Node);

        // Confirm that each edge collection contains the threads in the path
        for (var i = threadPath.Count - 1; i > 0; i--)
        {
            var templateThreadGroup = (Core.Node?)await threadPath[i - 1].GetValueAsync("template_thread_group");
            if (templateThreadGroup == null)
                throw new Exception("Invalid ThreadId");

            var threadGroup = await _stateMap.GetThreadGroupAsync(
                fromNode: threadPath[i],
                templateThreadGroup.Guid
            );
            if (threadGroup == null || !threadGroup.Contains(threadPath[i - 1].Guid))
                throw new Exception("Invalid ThreadId");
        }
    }

    private async Task<List<object?>> _GetAvailableThreadVariablesAsync(
        Core.Node templateThreadGroup,
        EdgeCollection<Core.Node, Core.Node> threadGroup
    )
    {
        var threadVariableType = (string)(await templateThreadGroup.GetValueAsync("thread_variable_type"))!;
        var spawnFromCollection = await ReferenceResolver.ResolveReferencePathAsync(
            _stateMap,
            (string)(await templateThreadGroup.GetValueAsync("spawn_from_collection"))!
        ) ?? new List<object?>();

        var activeThreadVariableTasks = (threadGroup != null ? await threadGroup.GetAllNodesAsync() : new List<Core.Node>())
            .Select(n => n.GetValueAsync("thread_variable"));
        var activeThreadVariables = (await Task.WhenAll(activeThreadVariableTasks))
            .Concat(_spawningFromThreadVariables) // Prevent reuse of the same thread variable when spawning multiple threads.
            .ToList();

        // Based on the active thread variables, determine which ones are available.
        var availableThreadVariables = new List<object?>();
        foreach (var item in (List<object?>)spawnFromCollection)
        {
            // The Comparison class is leveraged here because the type of the items in a given collection will vary.
            if (new Comparison(activeThreadVariables, "CONTAINS", item).Result)
                // The collection may contain duplicate items, so remove the first occurance.
                activeThreadVariables.Remove(item);
            else
                availableThreadVariables.Add(item);
        }

        return availableThreadVariables;
    }
}