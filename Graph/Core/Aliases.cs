// using Graph.Core;

public class FlatDictionary : Dictionary<string, object?>
{
    public FlatDictionary()
    {
    }

    public FlatDictionary(FlatDictionary other) : base(other)
    {
    }
}

public class NestedDictionary : Dictionary<string, FlatDictionary>
{
    public NestedDictionary()
    {
    }

    public NestedDictionary(NestedDictionary other) : base(other)
    {
    }
}