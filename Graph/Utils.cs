namespace Graph.Core;

public static class Utils
{
    public static ulong TimestampMillis(ulong? timestamp = null)
    {
        if (timestamp is ulong)
            return (ulong)timestamp;

        return (ulong)DateTimeOffset.Now.ToUnixTimeMilliseconds();
    }

    public static ulong TimestampMillis(DateTime dt)
    {
        return (ulong)((DateTimeOffset)dt).ToUnixTimeMilliseconds();
    }

    public static ulong? TimestampMillisOrNull(ulong? timestamp)
    {
        if (timestamp == null)
            return null;

        return TimestampMillis(timestamp);
    }

    public static ulong? TimestampMillisOrNull(DateTime? dt)
    {
        if (dt == null)
            return null;

        return TimestampMillis((DateTime)dt);
    }

    public static Type? GetDerivedType(Type baseType, string derivedTypeName)
    {
        var types = baseType.Assembly.GetTypes()
            .Where(t => t != baseType
                        && baseType.IsAssignableFrom(t)
                        && t.Name == derivedTypeName)
            .ToList();

        return types.Count() == 1 ? types[0] : null;
    }

    public static NestedDictionary ToData(NestedDictionary fromDict, NestedDictionary toDict, ModeEnum fromMode)
    {
        if (!fromDict.ContainsKey(fromMode.ToString())) return toDict;

        var toDataFields = (FlatDictionary)toDict["Data"]["Fields"]!;
        var toDataEdges = (FlatDictionary)toDict["Data"]["Edges"]!;
        var edgeCollectionLists = AsLinkedListDictionary<Guid>(toDict["Data"]["EdgeCollections"]!);

        var fromDataFields = (FlatDictionary)fromDict[fromMode.ToString()]["Fields"]!;
        var fromDataEdges = (FlatDictionary)fromDict[fromMode.ToString()]["Edges"]!;
        var fromDataEdgeCollections = (FlatDictionary)fromDict[fromMode.ToString()]["EdgeCollections"]!;

        var returnFields = new FlatDictionary(toDataFields);
        var returnEdges = new FlatDictionary(toDataEdges);
        var returnEdgeCollections = new FlatDictionary();

        var returnDict = new NestedDictionary
        {
            { "Meta", new FlatDictionary(toDict["Meta"]!) },
            { ModeEnum.Data.ToString(), new FlatDictionary() }
        };

        returnDict["Meta"]["Deleted"] = fromDict["Meta"]["Deleted"];

        foreach (var field in fromDataFields) returnFields[field.Key] = field.Value;
        foreach (var edge in fromDataEdges) returnEdges[edge.Key] = edge.Value;

        if (fromMode == ModeEnum.Data)
            foreach (var edgeCollection in fromDataEdgeCollections)
                edgeCollectionLists[edgeCollection.Key] = AsLinkedList(edgeCollection.Value);
        else
            foreach (var edgeCollection in fromDataEdgeCollections)
                if (edgeCollection.Value is FlatDictionary)
                {
                    var delta = (FlatDictionary)edgeCollection.Value;
                    if (delta.ContainsKey("Added") || delta.ContainsKey("Removed"))
                        edgeCollectionLists[edgeCollection.Key] = DeltaToLinkedList(
                            delta,
                            edgeCollectionLists[edgeCollection.Key]
                        );
                    else
                        edgeCollectionLists[edgeCollection.Key] = new LinkedList<Guid>();
                }
                else
                {
                    edgeCollectionLists[edgeCollection.Key] = new LinkedList<Guid>();
                }

        foreach (var list in edgeCollectionLists)
            returnEdgeCollections[list.Key] = list.Value.ToList();

        returnDict[ModeEnum.Data.ToString()]["Fields"] = returnFields;
        returnDict[ModeEnum.Data.ToString()]["Edges"] = returnEdges;
        returnDict[ModeEnum.Data.ToString()]["EdgeCollections"] = returnEdgeCollections;
        return returnDict;
    }

    public static LinkedList<Guid> DeltaToLinkedList(FlatDictionary fromDict, LinkedList<Guid> toLinkedList)
    {
        if (fromDict.ContainsKey("Removed"))
            foreach (var guid in (IEnumerable<Guid>)fromDict["Removed"]!)
                toLinkedList.Remove(guid);

        if (fromDict.ContainsKey("Added"))
        {
            var todo = new Dictionary<Guid, Guid>();
            LinkedListNode<Guid>? nextInList;
            foreach (var item in (Dictionary<Guid, Guid?>)fromDict["Added"]!)
            {
                if (item.Value == null)
                {
                    toLinkedList.AddLast(item.Key);
                    continue;
                }

                nextInList = toLinkedList.Find((Guid)item.Value);
                if (nextInList != null)
                    toLinkedList.AddBefore(nextInList, item.Key);
                else
                    todo.Add(item.Key, (Guid)item.Value);
            }

            // This may be necessary once it's possible to
            // add nodes to edge collections at specific indices.
            while (todo.Count > 0)
            {
                var done = new List<Guid>();
                foreach (var item in todo.Reverse())
                {
                    nextInList = toLinkedList.Find(item.Value);
                    if (nextInList != null)
                    {
                        toLinkedList.AddBefore(nextInList, item.Key);
                        done.Add(item.Key);
                    }
                }

                foreach (var guid in done) todo.Remove(guid);
                done.Clear();
            }
        }

        return toLinkedList;
    }

    public static Dictionary<string, LinkedList<Guid>> AsLinkedListDictionary<T>(object dict)
    {
        if (dict is Dictionary<string, LinkedList<Guid>>)
            return (Dictionary<string, LinkedList<Guid>>)dict;

        var lld = new Dictionary<string, LinkedList<Guid>>();
        if (dict is FlatDictionary)
            foreach (var item in (FlatDictionary)dict)
                lld[item.Key] = AsLinkedList(item.Value);

        return lld;
    }

    public static LinkedList<Guid> AsLinkedList(object? dictOrList)
    {
        if (dictOrList is LinkedList<Guid>) return (LinkedList<Guid>)dictOrList;

        if (dictOrList is Dictionary<Guid, Guid?>)
            return new LinkedList<Guid>(((Dictionary<Guid, Guid?>)dictOrList).Keys.ToList());

        if (dictOrList != null)
            return new LinkedList<Guid>((List<Guid>)dictOrList);

        return new LinkedList<Guid>();
    }

    public static List<string> EnumAsList(Type enumType)
    {
        return (from object c in Enum.GetValues(enumType) select c.ToString()).ToList();
    }

    public static string ToCamelCase(string input)
    {
        return input[0] + input.Substring(1);
    }

    public static bool IsNumericType(object? value)
    {
        if (value == null) return false;

        switch (Type.GetTypeCode(value.GetType()))
        {
            case TypeCode.Byte:
            case TypeCode.SByte:
            case TypeCode.UInt16:
            case TypeCode.UInt32:
            case TypeCode.UInt64:
            case TypeCode.Int16:
            case TypeCode.Int32:
            case TypeCode.Int64:
            case TypeCode.Decimal:
            case TypeCode.Double:
            case TypeCode.Single:
                return true;
            default:
                return false;
        }
    }
}