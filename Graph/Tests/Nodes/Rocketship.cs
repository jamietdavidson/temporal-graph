using Graph.Core;
using String = Graph.Core.String;

namespace Graph.Tests;

public class Rocketship : Node
{
    // EdgeCollections
    public EdgeCollection<Rocketship, Booster>? Boosters;

    // Scalars
    public String? Name;
    public String? SerialNumber;

    public Rocketship(
        NestedDictionary nodeData,
        Guid? guid = null,
        Cursor? cursor = null,
        ulong? timestamp = null
    ) : base(guid, cursor, nodeData, timestamp)
    {
    }
}