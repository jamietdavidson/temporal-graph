using Graph.Core;

namespace Graph.Mongo;

public static class Utils
{
    public static async Task<Dictionary<string, Node>> GetActionsByNameAsync(
        Node template, List<string>? actionNames = null
    )
    {
        var actionsByName = new Dictionary<string, Node>();

        var actions = await GetEdgeCollectionNodesAsync(template, "actions");
        if (actionNames != null)
        {
            foreach (var actionName in actionNames)
            {
                actionsByName[actionName] = actions
                    .Where(a => (string?)a.GetValueAsync("name").Result == actionName)
                    .First();
            }
        }
        else
            return actions.ToDictionary(a => (string?)a.GetValueAsync("name").Result!);

        return actionsByName;
    }

    public static async Task<List<Node>> AssertStateAsync(
        StateMapExecutor stateMap,
        List<decimal> satisfiedCheckpointIds,
        List<decimal> availableActionIds
    )
    {
        var collectionsToAssert = new Dictionary<string, List<decimal>>
        {
            { "satisfied_checkpoints", satisfiedCheckpointIds },
            { "available_actions", availableActionIds }
        };

        var nodes = new List<Node>();
        foreach (var (key, collection) in collectionsToAssert)
        {
            nodes = await GetEdgeCollectionNodesAsync(stateMap.Node, key);
            CollectionAssert.AreEquivalent(
                collection,
                nodes.Select(n => n.GetValueAsync("id").Result)
            );
        }

        return nodes;
    }

    public static async Task<List<Node>> GetEdgeCollectionNodesAsync(Node fromNode, string key)
    {
        var nodes = (EdgeCollection<Node, Node>?)await fromNode.GetValueAsync(key);
        return nodes?.GetAllNodesAsync().Result ?? new List<Node>();
    }

    public static async Task<Node> GetEntryActionAsync(StateMapExecutor stateMap)
    {
        var entryActions = (EdgeCollection<Node, Node>?)await stateMap.Template.GetValueAsync("entry_actions");
        Assert.That(entryActions?.Count, Is.GreaterThan(0));
        return (await entryActions.GetNodeAtIndexAsync(0))!;
    }

    public static async Task<string> GetActionTagAsync(Node action)
    {
        var objectPromise = ((Node?)await action.GetValueAsync("object_promise"))!;
        var objectType = ((Node?)await objectPromise.GetValueAsync("object_type"))!;
        return (string)(await objectType!.GetValueAsync("name"))!;
    }

    public static async Task<Guid> GetPartyGuidAsync(StateMapExecutor stateMap, string partyName)
    {
        var cursor = stateMap.Cursor!;
        var stateMapGuid = (Guid)stateMap.Guid!;
        await _LoadNodes(stateMap.Cursor!, new List<Guid> { stateMapGuid, stateMap.TemplateGuid });

        var node = cursor.NodeGuids[stateMapGuid];
        var template = cursor.NodeGuids[stateMap.TemplateGuid];
        var templateParties = ((EdgeCollection<Node, Node>?)await template.GetValueAsync("parties"))!;

        Guid? templatePartyGuid = null;
        foreach (var templateParty in await templateParties.GetAllNodesAsync())
        {
            if ((string)(await templateParty.GetValueAsync("name"))! == partyName)
            {
                templatePartyGuid = templateParty.Guid;
                break;
            }
        }

        if (templatePartyGuid == null)
            throw new Exception("Party not found");

        return ((Node?)await node.GetValueAsync("_party_" + templatePartyGuid.ToString()))!.Guid;
    }

    private static async Task _LoadNodes(Cursor cursor, List<Guid> nodeGuids)
    {
        var toLoad = new List<Guid>();
        foreach (var guid in nodeGuids)
        {
            if (!cursor!.NodeGuids.ContainsKey(guid))
                toLoad.Add(guid);
        }

        if (toLoad.Count > 0) await cursor!.NowAsync(toLoad);
    }
}