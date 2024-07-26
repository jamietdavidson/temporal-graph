using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace Graph.Core;

public class MongoDataStore : AbstractDataStore
{
    private readonly IMongoCollection<MongoNode> _mongoCollection;

    public MongoDataStore()
    {
        _mongoCollection = Environment.GetMongoDatabase().GetCollection<MongoNode>("default");
    }

    public async Task<Guid?> GetOisSchemaAsync(string oisSchemaPath)
    {
        var queryable = _mongoCollection.AsQueryable();
        IMongoQueryable<MongoNode>? filter = queryable.Where(n => n.stringFields["_ois_schema_path"] == oisSchemaPath);
        var filterResults = await filter.Select(n => n!).ToListAsync();
        if (filterResults != null && filterResults.Count > 0)
            return Guid.Parse(filterResults.OrderBy(n => n.timestamp).Last().guid);

        return null;
    }

    protected override async Task SerializeAsync(List<Node> nodes, ulong? timestamp, ModeEnum mode = ModeEnum.Delta)
    {
        await _PreventReuseOfDeletedGuids(nodes);

        if (timestamp == null)
            timestamp = Utils.TimestampMillis(timestamp);

        // Write to Mongo to ensure Guids are persisted
        var nodeReps = new List<NodeRep>();
        foreach (var node in nodes)
        {
            await node.DeltaToDataAsync();
            nodeReps.Add(node.AsNodeRep());
        }

        var mongoNodes = new List<MongoNode>();
        foreach (var nodeRep in nodeReps)
        {
            var mongoNode = new MongoNode
            {
                // our data
                guid = nodeRep.Meta.Guid.ToString(),
                tag = nodeRep.Meta.Tag,
                deleted = nodeRep.Deleted,
                timestamp = (ulong)timestamp,
                booleanFields = nodeRep.Fields.BooleanFields,
                stringFields = nodeRep.Fields.StringFields,
                numericFields = nodeRep.Fields.NumericFields,
                booleanListFields = nodeRep.Fields.BooleanListFields,
                stringListFields = nodeRep.Fields.StringListFields,
                numericListFields = nodeRep.Fields.NumericListFields,
                edgeTags = nodeRep.Edges.Tags,
                edges = nodeRep.Edges.Values.ToDictionary(x => x.Key, x => x.Value.ToString()),
                edgeCollectionTags = nodeRep.EdgeCollections.Tags,
                edgeCollections = nodeRep.EdgeCollections.Values.ToDictionary(x => x.Key,
                    x => x.Value.Select(y => y.ToString()).ToList())
            };
            mongoNodes.Add(mongoNode);
        }
        await _mongoCollection.InsertManyAsync(mongoNodes);
    }

    private (List<Node>, ulong) _MongoNodesToNodes(IEnumerable<MongoNode> mongoNodes, ulong timestamp)
    {
        var nodes = new List<Node>();
        foreach (var mongoNode in mongoNodes)
        {
            var nodeRep = new NodeRep();
            timestamp = Math.Max(timestamp, mongoNode.timestamp);
            nodeRep.Deleted = mongoNode.deleted;
            nodeRep.Meta.Tag = mongoNode.tag;
            nodeRep.Fields.BooleanFields = mongoNode.booleanFields;
            nodeRep.Fields.StringFields = mongoNode.stringFields;
            nodeRep.Fields.NumericFields = mongoNode.numericFields;
            nodeRep.Fields.BooleanListFields = mongoNode.booleanListFields;
            nodeRep.Fields.StringListFields = mongoNode.stringListFields;
            nodeRep.Fields.NumericListFields = mongoNode.numericListFields;
            nodeRep.Edges.Tags = mongoNode.edgeTags;
            nodeRep.Edges.Values = mongoNode.edges.ToDictionary(
                x => x.Key,
                x => x.Value! == "" ? null : (Guid?)Guid.Parse(x.Value!)
            );
            nodeRep.EdgeCollections.Tags = mongoNode.edgeCollectionTags;
            nodeRep.EdgeCollections.Values = mongoNode.edgeCollections.ToDictionary(
                x => x.Key,
                x => x.Value.Select(
                    y => Guid.Parse(y)
                ).ToList()
            );
            var node = new Node(
                Guid.Parse(mongoNode.guid),
                null,
                nodeRep,
                mongoNode.timestamp
            );
            nodes.Add(node);
        }
        return (nodes, timestamp);
    }

    public override async Task<(ulong? timestamp, List<Node> nodes)> SearchAsync(Query query,
        bool includeRemoved = false)
    {
        // Inception is a special case.
        if (query.AfterTimestamp == (ulong)DatesEnum.Inception && query.AtTimestamp == null)
            return await SearchInceptionAsync(query, includeRemoved);

        // see https://mongodb.github.io/mongo-csharp-driver/2.17/reference/driver/crud/linq/
        var queryable = _mongoCollection.AsQueryable();
        var nodeGuids = query.IncludeGuids.Select(g => g.ToString()).ToList();

        IMongoQueryable<MongoNode?>? atFilter = null;
        IMongoQueryable<MongoNode?>? afterFilter = null;

        if (query.AtTimestamp != null)
            atFilter = (IMongoQueryable<MongoNode?>?)queryable.Where(n => n.timestamp <= query.AtTimestamp);
        if (query.AfterTimestamp != null)
        {
            // Get the next timestamp for each node in nodeGuids
            var nextTimestamps = queryable.Where(n => nodeGuids.Contains(n.guid) && n.timestamp > query.AfterTimestamp)
                .GroupBy(n => n.guid, (key, g) => g.OrderBy(n => n.timestamp).First());

            if (nextTimestamps.Any())
            {
                // We are only interested in results at the very next timestamp of any single node in the group
                var nextTimestamp = nextTimestamps.Min(n => n.timestamp);
                afterFilter = (IMongoQueryable<MongoNode?>?)queryable.Where(n => n!.timestamp == nextTimestamp);
            }
        }

        // limit the filter to the specified guids
        if (nodeGuids.Count > 0)
        {
            atFilter = atFilter?.Where(n => nodeGuids.Contains(n!.guid));
            afterFilter = afterFilter?.Where(n => nodeGuids.Contains(n!.guid));
        }

        // order by timestamp
        atFilter = atFilter?.OrderBy(n => n!.timestamp);

        // group by id then return the latest
        atFilter = atFilter?.GroupBy(
            n => n!.guid,
            (key, g) => g.First()!.timestamp <= query.AtTimestamp && !g.Where(n => n!.deleted).Any() ? g.Last() : null
        );

        // find removed nodes in the after filter and remove them from the at filter
        var removedNodes = afterFilter?.Where(n => n!.deleted).Select(n => n!.guid).ToList();
        if (removedNodes != null && removedNodes.Count > 0)
            atFilter = atFilter?.Where(n => !removedNodes.Contains(n!.guid));

        var atTimestampFilterResults = atFilter.Select(n => n!).ToListAsync();
        var afterTimestampFilterResults = afterFilter?.Select(n => n!).ToListAsync();

        await Task.WhenAll(new List<Task>
            { atTimestampFilterResults ?? Task.CompletedTask, afterTimestampFilterResults ?? Task.CompletedTask });

        var atTimestampResults = atTimestampFilterResults?.Result.Where(n => n != null)
            .ToDictionary(n => n!.guid, n => n) ?? new Dictionary<string, MongoNode>();

        var afterTimestampResults = afterTimestampFilterResults?.Result;
        if (afterTimestampResults != null)
        {
            foreach (var item in afterTimestampResults)
            {
                if (atTimestampResults.ContainsKey(item.guid))
                    atTimestampResults[item.guid] = item;
                else
                    atTimestampResults.Add(item.guid, item);
            }
        }
        var mongoNodes = atTimestampResults.Values.ToList();

        var (nodes, timestamp) = _MongoNodesToNodes(mongoNodes!, 0ul);

        return (timestamp == 0ul ? null : timestamp, nodes);
    }

    public async Task<(ulong? timestamp, List<Node> nodes)> SearchInceptionAsync(Query query,
        bool includeRemoved = false)
    {
        var queryable = _mongoCollection.AsQueryable();
        var nodeGuids = query.IncludeGuids.Select(g => g.ToString()).ToList();

        // Get the earliest timestamp for each node in nodeGuids
        var earliestTimestamps = queryable.Where(n => nodeGuids.Contains(n.guid))
            .GroupBy(n => n.guid, (key, g) => g.OrderBy(n => n.timestamp).First());

        // Get the min timestamp from earliestTimestamps
        var minTimestamp = earliestTimestamps.Min(n => n.timestamp);

        var earliestTimestampFilterResults = earliestTimestamps.Where(n => n.timestamp == minTimestamp)
            .Select(n => n!).OrderBy(n => n.guid).ToListAsync();

        await Task.WhenAll(new List<Task>
            { earliestTimestampFilterResults ?? Task.CompletedTask });

        var earliestTimestampResults = earliestTimestampFilterResults?.Result.Where(n => n != null)
            .ToDictionary(n => n.guid, n => n) ?? new Dictionary<string, MongoNode>();

        var mongoNodes = earliestTimestampResults.Values.ToList();

        var (nodes, timestamp) = _MongoNodesToNodes(mongoNodes, 0ul);

        return (timestamp == 0ul ? null : timestamp, nodes);
    }

    public override Task<List<Node>> GetReferencingNodesAsync(Guid toNodeGuid)
    {
        var now = Utils.TimestampMillis();

        var queryable = (IMongoQueryable<MongoNode?>?)_mongoCollection.AsQueryable()
            .Where(n => n.timestamp <= now)
            .OrderBy(n => n!.timestamp);

        queryable = queryable.GroupBy(
            n => n!.guid,
            (key, mongoNodes) => mongoNodes.Where(n => n!.deleted).Any() ? null : mongoNodes.Last()
        );

        var toNodeId = toNodeGuid.ToString();
        var results = queryable.AsEnumerable().Where(n =>
            n != null &&
            (
                n.edges.Values.Contains(toNodeId) ||
                n.edgeCollections.Values.Any(l => l.Contains(toNodeId))
            )
        );

        var (nodes, timestamp) = _MongoNodesToNodes((IEnumerable<MongoNode>)results, now);
        return Task.FromResult(nodes);
    }

    private async Task _PreventReuseOfDeletedGuids(List<Node> nodes)
    {
        var nodeGuids = nodes.Select(n => n.Guid.ToString());
        var deletedGuids = await _mongoCollection.Find(n => nodeGuids.Contains(n.guid) && n.deleted)
            .Project(n => n.guid)
            .ToListAsync();

        if (deletedGuids.Any())
            throw new Exception($"The following node ids have been deleted and cannot be reused: {string.Join(", ", deletedGuids)}");
    }

    public override Task<List<ulong>> GetNodeTimestampsAsync(Guid nodeGuid)
    {
        throw new NotImplementedException();
    }
}