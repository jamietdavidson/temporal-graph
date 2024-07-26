using Graph.Core;
using String = Graph.Core.String;

namespace Graph.Tests;

public class FuelTank : Node
{
    // Scalars
    public Integer? FuelRemaining;
    public String? SerialNumber;

    public FuelTank(
        NestedDictionary nodeData,
        Guid? guid = null,
        Cursor? cursor = null,
        ulong? timestamp = null
    ) : base(guid, cursor, nodeData, timestamp)
    {
    }
}