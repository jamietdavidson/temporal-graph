using Graph.Core;
using String = Graph.Core.String;

namespace Graph.Tests;

public class Booster : Node
{
    // Edges
    public Edge<Booster, FuelTank> FuelTank;

    // Scalars
    public String SerialNumber;
    public String State;

    public Booster(
        NestedDictionary nodeData,
        Guid? guid = null,
        Cursor? cursor = null,
        ulong? timestamp = null
    ) : base(guid, cursor, nodeData, timestamp)
    {
    }
}