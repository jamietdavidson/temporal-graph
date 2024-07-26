using MongoDB.Bson.Serialization.Attributes;

[BsonIgnoreExtraElements]
internal class MongoNode
{
    // node data
    public string guid { get; set; }

    public string tag { get; set; }

    public ulong timestamp { get; set; }

    public bool deleted { get; set; }

    public Dictionary<string, string> meta { get; set; }

    public Dictionary<string, bool?> booleanFields { get; set; }

    public Dictionary<string, string?> stringFields { get; set; }

    public Dictionary<string, decimal?> numericFields { get; set; }

    public Dictionary<string, List<bool?>?> booleanListFields { get; set; }

    public Dictionary<string, List<string?>?> stringListFields { get; set; }

    public Dictionary<string, List<decimal?>?> numericListFields { get; set; }

    public Dictionary<string, string> edgeTags { get; set; }

    public Dictionary<string, string?> edges { get; set; }

    public Dictionary<string, string> edgeCollectionTags { get; set; }

    public Dictionary<string, List<string>> edgeCollections { get; set; }
}