using Graph.Core;

namespace Graph.Mongo;

public static class ReferenceResolver
{
    private static Dictionary<string, string> _referenceTypesToEdgeCollections = new()
    {
        { "object_type", "object_types" },
        { "object_promise", "object_promises" },
        { "action", "actions" },
        { "checkpoint", "checkpoints" },
        { "thread_group", "thread_groups" },
        { "party", "parties" }
    };

    private static Dictionary<string, string> _referenceableAliasFields = new()
    {
        { "term", "name" },
        { "party", "name" },
        { "object_type", "name" },
        { "object_promise", "name" },
        { "checkpoint", "alias" }
    };

    public static async Task<Guid> ResolveReferenceAsync(
        Node templateNode,
        string reference,
        string fromEdgeCollection
    )
    {
        var edgeCollection = (EdgeCollection<Node, Node>)(await templateNode!.GetValueAsync(fromEdgeCollection))!;
        return (await ResolveReferenceAsync(reference, await edgeCollection.GetAllNodesAsync())).Guid;
    }

    // This overload is useful for TemplateParser
    public static async Task<Node?> ResolveReferenceAsync(string reference, List<Node> fromNodes)
    {
        // TODO: account for imported references

        var refDetails = new RefDetails(reference);

        string fieldToFind;
        if (refDetails.isAliasReference)
        {
            fieldToFind = _referenceableAliasFields[refDetails?.Type!];
            foreach (var node in fromNodes)
                if ((string?)await node.GetValueAsync(fieldToFind) == refDetails?.Identifier!)
                    return node;
        }
        else if (refDetails.isRuntimeVariable)
        {

        }
        else
        {
            var refId = decimal.Parse(refDetails?.Identifier!);
            foreach (var node in fromNodes)
                if ((decimal)(await node.GetValueAsync("id"))! == refId)
                    return node;
        }

        throw new Exception($"Could not resolve reference: {reference}");
    }

    public static async Task<object?> ResolveReferencePathAsync(
        StateMapExecutor stateMap,
        string referencePath,
        ThreadContext? threadContext = null,
        string? returnOnTag = null
    )
    {
        var pathSegments = referencePath.Split(".").ToList();

        Node? referencedTemplateEntity = await _GetReferencedTemplateEntityAsync(stateMap, pathSegments[0]);
        pathSegments.RemoveAt(0);

        if (pathSegments.Count > 0)
            return await _ResolvePathAsync(
                stateMap,
                referencedTemplateEntity,
                pathSegments,
                threadContext,
                returnOnTag
            );

        return referencedTemplateEntity;
    }

    private static async Task<object?> _ResolvePathAsync(
        StateMapExecutor stateMap,
        Node fromNode,
        List<string> pathSegments,
        ThreadContext? threadContext = null,
        string? returnOnTag = null
    )
    {
        object? currentObject = fromNode;
        while (pathSegments.Count > 0)
        {
            var currentNode = (Node)currentObject;
            if (currentNode.Tag == returnOnTag) return currentNode;

            // Traversing edges
            var nextSegment = pathSegments[0];
            pathSegments.RemoveAt(0);

            var nextGraphObject = await currentNode.GetValueAsync(nextSegment, ModeEnum.Either);
            if (nextGraphObject == null) return null;

            if (fromNode.Tag == "Action" && nextSegment == "object_promise" && nextGraphObject is Node)
            {
                // Determine the context from which to resolve the promised object instance
                Node resolutionContext = stateMap.Node;
                var templateThreadGroup = (Node?)await fromNode.GetValueAsync("thread_group");
                if (templateThreadGroup != null)
                {
                    if (threadContext == null)
                        throw new Exception("Cannot resolve threaded object promise without a ThreadContext.");

                    resolutionContext = threadContext.GetThread(templateThreadGroup.Guid);
                }

                // Resolve the promised object instance from the appropriate context
                nextGraphObject = await stateMap.GetPromisedObjectAsync(
                    ((Node)nextGraphObject).Guid,
                    resolutionContext,
                    ModeEnum.Either
                );
                if (nextGraphObject == null) return null;
            }

            if (nextGraphObject is EdgeCollection<Node, Node> edgeCollection)
                // Logic must diverge here to handle list interpretation
                return await _ResolvePathFromEdgeCollectionAsync(edgeCollection, pathSegments);
            else
                currentObject = nextGraphObject;
        }

        return currentObject;
    }

    private static async Task<object?> _ResolvePathFromEdgeCollectionAsync(
        EdgeCollection<Node, Node> edgeCollection,
        List<string> pathSegments
    )
    {
        var nodes = await edgeCollection.GetAllNodesAsync();
        if (nodes.Count == 0) return new List<object?>();

        while (pathSegments.Count > 0)
        {
            // Traversing lists of edges
            var nextSegment = pathSegments[0];
            pathSegments.RemoveAt(0);
            var tasks = new List<Task<object?>>();
            foreach (var node in nodes)
                tasks.Add(node != null ? node.GetValueAsync(nextSegment) : Task.FromResult<object?>(null));

            var items = (await Task.WhenAll(tasks)).ToList();
            if (items.Count == 0) return new List<object?>();

            if (items[0] is Node)
                nodes = (await Task.WhenAll(tasks)).Select(o => (Node)o!).ToList();
            else if (pathSegments.Count != 0)
                throw new Exception("Cannot traverse a list of non-Node objects");
            else // path ends with non-node attribute
                return items;
        }

        return nodes;
    }

    private static async Task<Node> _GetReferencedTemplateEntityAsync(StateMapExecutor stateMap, string reference)
    {
        var refDetails = new RefDetails(reference);
        var referencedGuid = await ResolveReferenceAsync(
            stateMap.Template,
            reference,
            fromEdgeCollection: _referenceTypesToEdgeCollections[refDetails.Type]
        );
        return stateMap.Cursor!.NodeGuids[referencedGuid];
    }
}

public class RefDetails
{
    public string? Type { get; set; }
    public string? Identifier { get; set; }
    public bool isAliasReference { get; set; }

    public bool isRuntimeVariable { get; set; }

    public RefDetails(string reference)
    {

        // Runtime variables have a $ prefix
        if (reference.StartsWith("$"))
        {
            isRuntimeVariable = true;
            isAliasReference = false;
            Type = null;
            Identifier = null;
            return;
        }
        else
        {
            var split_path = reference.Split(".");
            var splitRef = split_path[0].Split(':');
            Type = splitRef[0];
            var id = splitRef[1];
            isAliasReference = id.StartsWith("{") && id.EndsWith("}");
            isRuntimeVariable = false;
            Identifier = isAliasReference ? id[1..^1] : id;
        }
    }
}