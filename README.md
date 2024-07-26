# core-graph

## Developer Guide

We use a few different naming conventions to represent different parts of a `Node`. They are to be used as follows:

### NestedDictionary nodeDataDict

```csharp
var nodeValueDict = new NestedDictionary()
{
    {
        "Fields", new FlatDictionary
        {
            { "fieldKey", Field },
        }
    },
    {
        "Edge", new FlatDictionary
        {
            { "edgeKey", Edge },
        }
    },
    {
        "EdgeCollections", new FlatDictionary
        {
            { "edgeCollectionKey", EdgeCollection }
        }
    }
}
```

`NestedDictionary nodeMetaDict`

```csharp
var nodeDict = new NestedDictionary()
{
    "Meta", new FlatDictionary
    {
        { "Guid", Guid },
        { "Tag", string },
        { "Deleted", boolean }
    }
}
```

`NestedDictionary nodeRepDict`

Note that the `nodeRepDict` contains both of the `nodeMetaDict` and `nodeValuesDict`. The `nodeValuesDict` lies as a first layer dictionary as either `data` or `delta`.

```c#
var nodeDict = new NestedDictionary()
{
    {
        "Meta", new FlatDictionary
        {
            { "Guid", Guid },
            { "Tag", string },
            { "Deleted", boolean }
        }
    },
    {
        "Data", new FlatDictionary
        {
            {
                "Fields", new FlatDictionary
                {
                    { "fieldKey", Field },
                }
            },
            {
                "Edge", new FlatDictionary
                {
                    { "edgeKey", Edge },
                }
            },
            {
                "EdgeCollections", new FlatDictionary
                {
                    { "edgeCollectionKey", EdgeCollection }
                }
            }
        }
    }
}
```
