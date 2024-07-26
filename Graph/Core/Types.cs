namespace Graph.Core;

internal abstract class NodeDataField
{
    // These readonly static fields will end up behaving essentially
    // like enum values.
    public static readonly NodeDataField<string> Name = new();
    public static readonly NodeDataField<int> Value = new();
}

internal sealed class NodeDataField<T> : NodeDataField
{
    // Don't allow external code to instantiate this type.
}