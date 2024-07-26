using Graph.Core;
using Graph.Mongo;

namespace Graph.Templates;

public class TemplateCheckpoint : NodeIntermediary
{
    public int id { get; set; }
    public string alias { get; set; }
    public string? description { get; set; }
    public string? abbreviated_description { get; set; }
    public string?[]? supporting_info { get; set; }
    public string? gate_type { get; set; }
    public string? context { get; set; }
    public TemplateDependency[] dependencies { get; set; }

    public async override Task<Node> ToNodeAsync(Cursor cursor)
    {
        var checkpointRep = new NodeRep("Checkpoint");
        checkpointRep.Fields.NumericFields = new Dictionary<string, decimal?>
        { { CheckpointKey.ID, id } };
        checkpointRep.Fields.StringFields = new Dictionary<string, string?>
        {
            { CheckpointKey.ALIAS, alias },
            { CheckpointKey.DESCRIPTION, description },
            { CheckpointKey.ABBREVIATED_DESCRIPTION, abbreviated_description },
            { CheckpointKey.GATE_TYPE, gate_type }
        };
        checkpointRep.Fields.StringListFields = new Dictionary<string, List<string?>?>
        { { CheckpointKey.SUPPORTING_INFO, supporting_info?.ToList() } };

        // Checkpoint references are resolved after all checkpoints are instantiated
        checkpointRep.EdgeCollections.Tags.Add(CheckpointKey.CHECKPOINT_REFERENCES, "Checkpoint");
        checkpointRep.EdgeCollections.Tags.Add(CheckpointKey.DEPENDENT_CHECKPOINTS, "Checkpoint");
        checkpointRep.EdgeCollections.Values.Add(CheckpointKey.DEPENDENT_CHECKPOINTS, new List<Guid>());

        checkpointRep.EdgeCollections.Tags.Add(CheckpointKey.DEPENDENCIES, "Dependency");
        var tasks = new List<Task<Node>>();
        foreach (var dependency in dependencies)
            if (dependency.compare != null)
                tasks.Add(dependency.ToNodeAsync(cursor));

        var dependencyNodes = await Task.WhenAll(tasks);
        checkpointRep.EdgeCollections.Values.Add(
            CheckpointKey.DEPENDENCIES,
            dependencyNodes.Select(n => n.Guid).ToList()
        );

        // The value of these will be set by the ResolveReferencesAsync method
        checkpointRep.EdgeCollections.Tags.Add(CheckpointKey.INPUT_ACTIONS, "Action");
        checkpointRep.EdgeCollections.Tags.Add(CheckpointKey.INPUT_OBJECT_PROMISES, "ObjectPromise");

        // dependent_actions edges are collected by Action.ResolveReferencesAsync
        checkpointRep.EdgeCollections.Tags.Add(CheckpointKey.DEPENDENT_ACTIONS, "Action");
        checkpointRep.EdgeCollections.Values.Add(CheckpointKey.DEPENDENT_ACTIONS, new List<Guid>());

        // dependent_thread_groups edges are collected by ThreadGroup.ResolveReferencesAsync
        checkpointRep.EdgeCollections.Tags.Add(CheckpointKey.DEPENDENT_THREAD_GROUPS, "ThreadGroup");
        checkpointRep.EdgeCollections.Values.Add(CheckpointKey.DEPENDENT_THREAD_GROUPS, new List<Guid>());

        var node = Node.FromRep(checkpointRep);
        node.Cursor = cursor;
        await node.SaveAsync();
        Guid = node.Guid;
        return node;
    }

    public async new Task ResolveReferencesAsync(TemplateStateMap template, Cursor cursor)
    {
        var node = cursor.NodeGuids[(Guid)Guid!];

        if (context != null)
        {
            var threadGroupNode = await ReferenceResolver.ResolveReferenceAsync(
                context,
                template.ThreadGroups.Select(guid => cursor.NodeGuids[guid]).ToList()
            );
            await node.SetValueAsync(CheckpointKey.THREAD_GROUP, threadGroupNode);
        }

        var checkpointReferences = new EdgeCollection<Node, Node>(node, "Checkpoint", null);
        var checkpoints = template.Checkpoints.Select(guid => cursor.NodeGuids[guid]).ToList();
        var allActions = template.Actions.Select(guid => cursor.NodeGuids[guid]).ToList();
        var referencedActionTasks = new List<Task<Node>>();
        foreach (var dependency in dependencies)
        {
            if (dependency.checkpoint != null)
                checkpointReferences.Append(await ReferenceResolver.ResolveReferenceAsync(dependency.checkpoint, checkpoints));
            else if (dependency.compare != null)
            {
                foreach (var reference in dependency.References)
                    referencedActionTasks.Add(ReferenceResolver.ResolveReferenceAsync(reference, allActions));
            }
        }

        var referencedActionNodes = (await Task.WhenAll(referencedActionTasks)).ToList();
        var actionDependees = new EdgeCollection<Node, Node>(
            node, "Action", referencedActionNodes
        );

        var objectPromiseTasks = new List<Task<object?>>();
        foreach (var actionNode in referencedActionNodes)
        {
            objectPromiseTasks.Add(actionNode.GetValueAsync(CheckpointKey.OBJECT_PROMISE, ModeEnum.Delta));

            var referencedActionDependentCheckpoints = await actionNode.GetValueAsync(CheckpointKey.DEPENDENT_CHECKPOINTS, ModeEnum.Delta);
            ((EdgeCollection<Node, Node>?)referencedActionDependentCheckpoints)!.Append(node);
        }

        var objectPromiseDependees = new EdgeCollection<Node, Node>(
            node, "ObjectPromise", (await Task.WhenAll(objectPromiseTasks)).Select(op => (Node)op!).ToList()
        );

        var referencedCheckpointTasks = new List<Task<object?>>();
        foreach (var referencedCheckpoint in await checkpointReferences.GetAllNodesAsync())
            referencedCheckpointTasks.Add(referencedCheckpoint.GetValueAsync(CheckpointKey.DEPENDENT_CHECKPOINTS, ModeEnum.Delta));

        foreach (var dependentCheckpoints in await Task.WhenAll(referencedCheckpointTasks))
            ((EdgeCollection<Node, Node>)dependentCheckpoints!).Append(node);

        await Task.WhenAll(new List<Task>
        {
            node.SetValueAsync(CheckpointKey.CHECKPOINT_REFERENCES, checkpointReferences),
            node.SetValueAsync(CheckpointKey.INPUT_ACTIONS, actionDependees),
            node.SetValueAsync(CheckpointKey.INPUT_OBJECT_PROMISES, objectPromiseDependees)
        });
    }
}