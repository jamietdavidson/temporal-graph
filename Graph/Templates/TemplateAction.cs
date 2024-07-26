using Graph.Core;
using Graph.Mongo;
using Newtonsoft.Json.Linq;

namespace Graph.Templates;

public class TemplateAction : NodeIntermediary
{
    public int id { get; set; }
    public string name { get; set; }
    public string? description { get; set; }
    public string? party { get; set; }
    public string? object_promise { get; set; }
    public string? depends_on { get; set; }
    public string? context { get; set; }
    public string?[]? supporting_info { get; set; }
    public ActionStep[]? steps { get; set; }
    public TemplateActionOperation operation { get; set; }

    public void SetOperationType(JObject actionToken)
    {
        var inclusionTypes = new List<string> { ActionOperationKey.INCLUDE, ActionOperationKey.EXCLUDE };

        foreach (var item in actionToken.Properties())
        {
            if (item.Name == ActionKey.OPERATION)
            {
                foreach (var prop in ((JObject)item.Value).Properties())
                {
                    if (inclusionTypes.Contains(prop.Name))
                    {
                        operation.Type = prop.Name;
                        return;
                    }
                }
            }

        }

        throw new Exception("Invalid operation");
    }

    public async override Task<Node> ToNodeAsync(Cursor cursor)
    {
        var actionRep = new NodeRep("Action");
        actionRep.Fields.NumericFields = new Dictionary<string, decimal?>
        { { ActionKey.ID, id } };
        actionRep.Fields.StringFields = new Dictionary<string, string?>
        {
            { ActionKey.NAME, name },
            { ActionKey.DESCRIPTION, description },
        };
        actionRep.Fields.StringListFields = new Dictionary<string, List<string?>?>
        { { ActionKey.SUPPORTING_INFO, supporting_info?.ToList() } };

        actionRep.EdgeCollections.Tags.Add(ActionKey.STEPS, "ActionStep");
        var tasks = new List<Task<Node>>();
        foreach (var step in steps ?? new ActionStep[] { })
            tasks.Add(step.ToNodeAsync(cursor));

        if (tasks.Count > 0)
        {
            var stepNodes = await Task.WhenAll(tasks);
            actionRep.EdgeCollections.Values.Add(
                ActionKey.STEPS,
                stepNodes.Select(n => n.Guid).ToList()
            );
        }

        var nullEdges = new Dictionary<string, string>
        {
            { ActionKey.PARTY, "Party" },
            { ActionKey.OBJECT_PROMISE, "ObjectPromise" },
            { ActionKey.OPERATION, "ActionOperation" },
            { ActionKey.DEPENDS_ON, "Checkpoint" },
            { ActionKey.THREAD_GROUP, "ThreadGroup" }
        };
        foreach (var (edgeKey, edgeTag) in nullEdges)
        {
            actionRep.Edges.Tags.Add(edgeKey, edgeTag);
            actionRep.Edges.Values.Add(edgeKey, null);
        }

        // Include a lookup for the checkpoints that depend on this action
        actionRep.EdgeCollections.Tags.Add(ActionKey.DEPENDENT_CHECKPOINTS, "Checkpoint");
        actionRep.EdgeCollections.Values.Add(ActionKey.DEPENDENT_CHECKPOINTS, new List<Guid>());

        var node = Node.FromRep(actionRep);
        node.Cursor = cursor;
        var actionOperation = await operation.ToNodeAsync(cursor);
        await Task.WhenAll(new List<Task>
        {
            node.SetValueAsync(ActionKey.OPERATION, actionOperation),
            node.SaveAsync()
        });
        Guid = node.Guid;
        return node;
    }

    public async new Task ResolveReferencesAsync(TemplateStateMap template, Cursor cursor)
    {
        var toResolve = new List<(string?, List<Guid>, string)>()
        {
            (party, template.Parties, ActionKey.PARTY),
            (object_promise, template.ObjectPromises, ActionKey.OBJECT_PROMISE)
        };

        var node = cursor.NodeGuids[(Guid)Guid!];
        var tasks = new List<Task>();
        foreach (var (reference, fromGuids, edgeKey) in toResolve)
        {
            if (reference != null)
            {
                var referencedNode = await ReferenceResolver.ResolveReferenceAsync(
                    reference,
                    fromGuids.Select(guid => cursor.NodeGuids[guid]).ToList()
                );
                tasks.Add(node.SetValueAsync(edgeKey, referencedNode));

                if (edgeKey == ActionKey.OBJECT_PROMISE)
                {
                    // Add this action to the object promise's lookup
                    var referencedByActions = (EdgeCollection<Node, Node>?)await referencedNode.GetValueAsync("referenced_by_actions");
                    referencedByActions?.Append(node);
                }
            }
        }

        if (depends_on != null)
        {
            var checkpoint = await ReferenceResolver.ResolveReferenceAsync(
                depends_on,
                template.Checkpoints.Select(guid => cursor.NodeGuids[guid]).ToList()
            );
            tasks.Add(node.SetValueAsync(ActionKey.DEPENDS_ON, checkpoint));

            // Add this action to the checkpoint's dependent_actions lookup
            var dependentEdgeCollection = (EdgeCollection<Node, Node>?)await checkpoint.GetValueAsync("dependent_actions");
            dependentEdgeCollection?.Append(node);
        }
        else
            template.EntryActions.Add((Guid)Guid);

        if (context != null)
        {
            var threadGroup = await ReferenceResolver.ResolveReferenceAsync(
                context,
                template.ThreadGroups.Select(guid => cursor.NodeGuids[guid]).ToList()
            );
            tasks.Add(node.SetValueAsync(ActionKey.THREAD_GROUP, threadGroup));

            // Add this action to the thread group's threaded_actions lookup
            var threadedActions = (EdgeCollection<Node, Node>)(await threadGroup.GetValueAsync("threaded_actions"))!;
            threadedActions.Append(node);
        }

        tasks.Add(operation.ResolveReferencesAsync(template, cursor));
        await Task.WhenAll(tasks);
    }
}

public class ActionStep : NodeIntermediary
{
    public string title { get; set; }
    public string description { get; set; }

    public async override Task<Node> ToNodeAsync(Cursor cursor)
    {
        var actionStepRep = new NodeRep("ActionStep");
        actionStepRep.Fields.StringFields = new Dictionary<string, string?>
        {
            { ActionStepKey.TITLE, title },
            { ActionStepKey.DESCRIPTION, description }
        };

        var node = Node.FromRep(actionStepRep);
        node.Cursor = cursor;
        await node.SaveAsync();
        Guid = node.Guid;
        return node;
    }
}
