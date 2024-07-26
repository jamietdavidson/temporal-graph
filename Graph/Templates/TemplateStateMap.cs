using Graph.Core;

namespace Graph.Templates;

public static class StateMapKey
{
    public static string STANDARD { get; } = "standard";
    public static string OIS_SCHEMA_PATH { get; } = "_ois_schema_path";
    public static string ALL_GUIDS { get; } = "_allGuids";
    public static string ENTRY_ACTIONS { get; } = "entry_actions";
    public static string EVERGREEN_ACTIONS { get; } = "evergreen_actions";
    public static string EVERGREEN_CREATE_ACTIONS { get; } = "evergreen_create_actions";
    public static string IMPORTS { get; } = "imports";
    public static string OBJECT_TYPES { get; } = "object_types";
    public static string OBJECT_PROMISES { get; } = "object_promises";
    public static string ACTIONS { get; } = "actions";
    public static string CHECKPOINTS { get; } = "checkpoints";
    public static string THREAD_GROUPS { get; } = "thread_groups";
    public static string PIPELINES { get; } = "pipelines";
    public static string PARTIES { get; } = "parties";
    public static string TERMS { get; } = "terms";
    public static string THREAD_SPAWN_TRIGGERS { get; } = "thread_spawn_triggers";
}

public class TemplateStateMap
{
    private string? _oisSchemaPath;

    public List<Guid> AllGuids { get; set; }

    public List<Guid> EntryActions { get; set; }

    public List<Guid> EvergreenActions { get; set; }

    public List<Guid> EvergreenCreateActions { get; set; }

    public string Standard { get; set; }

    public List<Guid> Imports { get; set; }

    public List<Guid> ObjectTypes { get; set; }

    public SchemaDefinition ObjectTypesSchemaDefinition { get; set; }

    public List<Guid> ObjectPromises { get; set; }

    public List<Guid> Actions { get; set; }

    public List<Guid> Checkpoints { get; set; }

    public List<Guid> ThreadGroups { get; set; }

    public List<Guid> ThreadSpawnTriggers { get; set; }

    public List<Guid> Pipelines { get; set; }

    public List<Guid> Parties { get; set; }

    public List<Guid> Terms { get; set; }

    public TemplateStateMap(string? oisSchemaPath)
    {
        _oisSchemaPath = oisSchemaPath;
        EntryActions = new List<Guid>();
        EvergreenActions = new List<Guid>();
        EvergreenCreateActions = new List<Guid>();
    }

    public List<Node> GetNodes(string tag, Cursor cursor)
    {
        List<Guid> guids = new List<Guid>();
        if (tag == "Import")
            guids = Imports;
        else if (tag == "ObjectType")
            guids = ObjectTypes;
        else if (tag == "ObjectPromise")
            guids = ObjectPromises;
        else if (tag == "Action")
            guids = Actions;
        else if (tag == "Checkpoint")
            guids = Checkpoints;
        else if (tag == "ThreadGroup")
            guids = ThreadGroups;
        else if (tag == "Pipeline")
            guids = Pipelines;
        else if (tag == "Party")
            guids = Parties;
        else if (tag == "Term")
            guids = Terms;
        else
            throw new Exception($"Unknown tag: {tag}");

        return guids.Select(guid => cursor.NodeGuids[guid]).ToList();
    }

    public Node ToNode()
    {
        var templateRep = new NodeRep("Template");
        templateRep.Fields.StringFields = new Dictionary<string, string?>
        {
            { StateMapKey.OIS_SCHEMA_PATH, _oisSchemaPath },
            { StateMapKey.STANDARD, Standard },
        };
        templateRep.Fields.StringListFields = new Dictionary<string, List<string?>?>
        {
            { StateMapKey.ALL_GUIDS, AllGuids.Select(guid => (string?)guid.ToString()).ToList() }
        };
        templateRep.EdgeCollections.Tags = new Dictionary<string, string>
        {
            {StateMapKey.IMPORTS, "Import"},
            {StateMapKey.OBJECT_TYPES, "ObjectType"},
            {StateMapKey.OBJECT_PROMISES, "ObjectPromise"},
            {StateMapKey.ACTIONS, "Action"},
            {StateMapKey.CHECKPOINTS, "Checkpoint"},
            {StateMapKey.THREAD_GROUPS, "ThreadGroup"},
            {StateMapKey.PIPELINES, "Pipeline"},
            {StateMapKey.PARTIES, "Party"},
            {StateMapKey.TERMS, "Term"},
            {StateMapKey.ENTRY_ACTIONS, "Action"},
            {StateMapKey.EVERGREEN_ACTIONS, "Action"},
            {StateMapKey.EVERGREEN_CREATE_ACTIONS, "Action"},
            {StateMapKey.THREAD_SPAWN_TRIGGERS, "ObjectPromise"}
        };
        templateRep.EdgeCollections.Values = new Dictionary<string, List<Guid>>
        {
            {StateMapKey.IMPORTS, Imports},
            {StateMapKey.OBJECT_TYPES, ObjectTypes},
            {StateMapKey.OBJECT_PROMISES, ObjectPromises},
            {StateMapKey.ACTIONS, Actions},
            {StateMapKey.CHECKPOINTS, Checkpoints},
            {StateMapKey.THREAD_GROUPS, ThreadGroups},
            {StateMapKey.PIPELINES, Pipelines},
            {StateMapKey.PARTIES, Parties},
            {StateMapKey.TERMS, Terms},
            {StateMapKey.ENTRY_ACTIONS, EntryActions},
            {StateMapKey.EVERGREEN_ACTIONS, EvergreenActions},
            {StateMapKey.EVERGREEN_CREATE_ACTIONS, EvergreenCreateActions},
            {StateMapKey.THREAD_SPAWN_TRIGGERS, ThreadSpawnTriggers}
        };

        return Node.FromRep(templateRep);
    }

    public async Task SetEvergreenActionsAsync(Cursor cursor)
    {
        var allActions = GetNodes("Action", cursor);

        var tasks = new Dictionary<Guid, Task<object?>>();
        foreach (var action in allActions)
            tasks.Add(action.Guid, action.GetValueAsync(ActionKey.DEPENDENT_CHECKPOINTS, ModeEnum.Delta));

        await Task.WhenAll(tasks.Values);
        foreach (var (actionGuid, dependentCheckpointsTask) in tasks)
        {
            // Evergreen actions are those that have no checkpoints that depend on them.
            if (((EdgeCollection<Node, Node>?)dependentCheckpointsTask.Result)?.Count == 0)
                EvergreenActions.Add(actionGuid);
        }

        tasks.Clear();
        foreach (var evergreenActionId in EvergreenActions)
            tasks.Add(evergreenActionId, cursor.NodeGuids[evergreenActionId].GetValueAsync(ActionKey.OBJECT_PROMISE, ModeEnum.Delta));

        await Task.WhenAll(tasks.Values);
        var secondaryTasks = new Dictionary<Guid, Task<object?>>();
        foreach (var (evergreenActionId, objectPromiseTask) in tasks)
        {
            if (objectPromiseTask.Result == null) continue;
            secondaryTasks.Add(
                evergreenActionId,
                ((Node)objectPromiseTask.Result).GetValueAsync(ObjectPromiseKey.REFERENCED_BY_ACTIONS)
            );
        }

        await Task.WhenAll(secondaryTasks.Values);
        foreach (var (evergreenActionId, referencedByActionsTask) in secondaryTasks)
        {
            var referencedByActions = (EdgeCollection<Node, Node>?)referencedByActionsTask.Result;

            // If an evergreen action is the only action to reference its object promise,
            // we can deduce that it is a CREATE action.
            if (
                (referencedByActions?.Count ?? 0) == 1
                && referencedByActions!.GetGuidsForRange(0, 1, ModeEnum.Delta).FirstOrDefault() == evergreenActionId
            )
                EvergreenCreateActions.Add(evergreenActionId);
        }
    }
}
