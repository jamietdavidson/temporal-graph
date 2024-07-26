using Graph.Core;
using Graph.Mongo;

namespace Graph.Templates;

public class TemplateThreadGroup : NodeIntermediary
{
    public int id { get; set; }
    public string name { get; set; }
    public string? description { get; set; }
    public string? depends_on { get; set; }
    public ThreadSpawn spawn { get; set; }
    public string? context { get; set; }

    public async override Task<Node> ToNodeAsync(Cursor cursor)
    {
        var threadGroupRep = new NodeRep("ThreadGroup");
        threadGroupRep.Fields.NumericFields = new Dictionary<string, decimal?>
        { { ThreadGroupKey.ID, id } };
        threadGroupRep.Fields.StringFields = new Dictionary<string, string?>
        {
            { ThreadGroupKey.NAME, name },
            { ThreadGroupKey.DESCRIPTION, description },
            { ThreadGroupKey.SPAWN_FOREACH, spawn.@foreach },
            { ThreadGroupKey.SPAWN_AS, spawn.@as }
        };

        // ThreadGroup context must be resolved after all ThreadGroups have been instantiated.
        threadGroupRep.Edges.Tags.Add(ThreadGroupKey.THREAD_GROUP, "ThreadGroup");
        threadGroupRep.Edges.Values.Add(ThreadGroupKey.THREAD_GROUP, null);

        // ThreadGroup dependency must be resolved after all Checkpoints have been instantiated.
        threadGroupRep.Edges.Tags.Add(ThreadGroupKey.DEPENDS_ON, "Checkpoint");
        threadGroupRep.Edges.Values.Add(ThreadGroupKey.DEPENDS_ON, null);

        threadGroupRep.EdgeCollections.Tags.Add(ThreadGroupKey.THREADED_ACTIONS, "Action");
        threadGroupRep.EdgeCollections.Values.Add(ThreadGroupKey.THREADED_ACTIONS, new List<Guid>());
        threadGroupRep.EdgeCollections.Tags.Add(ThreadGroupKey.NESTED_THREAD_GROUPS, "ThreadGroup");
        threadGroupRep.EdgeCollections.Values.Add(ThreadGroupKey.NESTED_THREAD_GROUPS, new List<Guid>());

        var node = Node.FromRep(threadGroupRep);
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
            var parentThreadGroup = await ReferenceResolver.ResolveReferenceAsync(
                context,
                template.ThreadGroups.Select(guid => cursor.NodeGuids[guid]).ToList()
            );
            await node.SetValueAsync(ThreadGroupKey.THREAD_GROUP, parentThreadGroup);

            // Add this ThreadGroup to the parent ThreadGroup's nested_thread_groups lookup
            var nestedThreadGroups = (EdgeCollection<Node, Node>?)await parentThreadGroup.GetValueAsync(ThreadGroupKey.NESTED_THREAD_GROUPS);
        }

        if (depends_on != null)
        {
            var checkpointNode = await ReferenceResolver.ResolveReferenceAsync(
                depends_on,
                template.Checkpoints.Select(guid => cursor.NodeGuids[guid]).ToList()
            );
            await node.SetValueAsync(ThreadGroupKey.DEPENDS_ON, checkpointNode);

            // Add this ThreadGroup to the Checkpoint's dependent_thread_groups lookup
            var dependentThreadGroups = (EdgeCollection<Node, Node>?)await checkpointNode.GetValueAsync(ThreadGroupKey.DEPENDS_ON);
            dependentThreadGroups!.Append(node);
        }
    }
}

public class ThreadSpawn
{
    public string @foreach { get; set; }
    public string @as { get; set; }
}