using Graph.Core;

namespace Graph.Mongo;

public class Comparison
{
    private bool _throwIfTypesNotComparable;

    private bool? _result;

    public object? Left { get; private set; }

    public object? Right { get; }

    public string Operator { get; }

    public bool Result
    {
        get
        {
            _result ??= _GetResult();
            return (bool)_result;
        }
    }

    public Comparison(object? left, string @operator, object? right, bool throwIfTypesNotComparable = true)
    {
        Left = left;
        Right = right;
        Operator = @operator;
        _throwIfTypesNotComparable = throwIfTypesNotComparable;
    }

    private bool _GetResult()
    {
        if (Left == null)
            return _CompareNull();

        var scalarWithListOperators = new List<string> { "ONE_OF", "NONE_OF" };

        if (Left is string)
        {
            if (scalarWithListOperators.Contains(Operator))
                return _CompareScalarWithList<string?>();

            return _CompareStrings();
        }

        if (Left is decimal || Left is int || Left is double)
        {
            if (scalarWithListOperators.Contains(Operator))
                return _CompareDecimalWithList();

            return _CompareDecimals();
        }

        if (Left is bool)
            return _CompareBooleans();

        if (Left is Node)
        {
            if (scalarWithListOperators.Contains(Operator))
                return _CompareObjectWithList();

            return _CompareObjects();
        }

        if (Left.GetType().Name == "List`1")
        {
            var isListWithNonListComparison = new List<string> { "CONTAINS", "DOES_NOT_CONTAIN" }.Contains(Operator);

            if (Left is List<Node?>)
            {
                if (isListWithNonListComparison)
                    return _CompareObjectListWithObject();

                return _CompareLists<string?>(
                    left: _ObjectListToGuidList((List<Node?>)Left),
                    right: _ObjectListToGuidList((List<Node?>?)Right)
                );
            }

            // It's sometimes necessary to convert List<object?> to the correct type
            if (Left is List<object?>)
            {
                var objectList = (List<object?>)Left;
                if (objectList[0] is string)
                    Left = objectList.Select(o => (string?)o).ToList();
                else if (objectList[0] is bool)
                    Left = objectList.Select(o => (bool?)o).ToList();
                else if (objectList[0] is decimal || objectList[0] is int || objectList[0] is float)
                    Left = objectList.Select(o => (decimal?)decimal.Parse(o?.ToString() ?? "0")).ToList();
            }

            if (Left is List<string?>)
            {
                if (isListWithNonListComparison)
                    return _CompareListWithScalar<string?>();

                return _CompareLists<string?>();
            }

            if (Left is List<bool?>)
            {
                if (isListWithNonListComparison)
                    return _CompareListWithScalar<bool?>();

                return _CompareLists<bool?>();
            }

            if (Left is List<decimal?>)
            {
                if (isListWithNonListComparison)
                    return _CompareListWithDecimal();

                return _CompareLists<decimal?>();
            }
        }

        return _ThrowExceptionOrReturnFalse($"Invalid type: {Left.GetType().Name}");
    }

    private bool _CompareNull()
    {
        if (Right == null || _IsEmptyList(Right))
        {
            return new List<string> {
                "EQUALS",
                "DOES_NOT_CONTAIN",
                "IS_SUBSET_OF",
                "IS_SUPERSET_OF",
                "NONE_OF",
                "CONTAINS_NONE_OF"
            }.Contains(Operator);
        }

        if (new List<string> { "ONE_OF", "NONE_OF" }.Contains(Operator))
        {
            if (Right == null) return Operator == "NONE_OF";

            if (Right is List<string?>)
                return _CompareNullWithList<string?>(null);

            if (Right is List<decimal?>)
                return _CompareNullWithList<decimal?>(null);

            if (Right is List<bool?>)
                return _CompareNullWithList<bool?>(null);

            if (Right is List<Node?>)
                return _CompareNullWithList<Node?>(null);
        }

        return new List<string> {
            "DOES_NOT_EQUAL",
            "DOES_NOT_CONTAIN",
            "CONTAINS_NONE_OF",
            "IS_SUBSET_OF",
        }.Contains(Operator);

        /*
        Any other operator evaluates to false:
            "CONTAINS",
            "CONTAINS_ANY_OF",
            "GREATER_THAN",
            "LESS_THAN",
            "GREATER_THAN_OR_EQUAL_TO",
            "LESS_THAN_OR_EQUAL_TO"
        */
    }

    private bool _CompareNullWithList<T>(T? value)
    {
        // Why even use the value parameter, you ask?
        // Because the compiler won't allow explicitly passing null to .Contains.
        if (value != null) throw new Exception("value must be null");

        var rightContainsLeft = ((List<T?>)Right!).Contains(value);
        return Operator switch
        {
            "ONE_OF" => rightContainsLeft,
            "NONE_OF" => !rightContainsLeft,
            _ => _ThrowExceptionOrReturnFalse($"Invalid operator: {Operator}")
        };
    }

    private bool _CompareStrings()
    {
        if (Right == null)
            return new List<string> { "DOES_NOT_EQUAL", "DOES_NOT_CONTAIN" }.Contains(Operator);

        var left = (string)Left!;
        var right = (string)Right!;
        if (Operator == "EQUALS") return left == right;
        if (Operator == "DOES_NOT_EQUAL") return left != right;

        if (Operator == "CONTAINS") return left.Contains(right);
        if (Operator == "DOES_NOT_CONTAIN") return !left.Contains(right);

        return _ThrowExceptionOrReturnFalse($"Invalid operator: {Operator}");
    }

    private bool _CompareDecimals()
    {
        if (Right == null) return Operator == "DOES_NOT_EQUAL";

        var left = decimal.Parse(Left?.ToString() ?? "0");
        var right = decimal.Parse(Right?.ToString() ?? "0");
        return Operator switch
        {
            "EQUALS" => left == right,
            "DOES_NOT_EQUAL" => left != right,
            "GREATER_THAN" => left > right,
            "LESS_THAN" => left < right,
            "GREATER_THAN_OR_EQUAL_TO" => left >= right,
            "LESS_THAN_OR_EQUAL_TO" => left <= right,
            _ => _ThrowExceptionOrReturnFalse($"Invalid operator: {Operator}"),
        };
    }

    private bool _CompareBooleans()
    {
        var areEqual = (bool?)Left == (bool?)Right;
        return Operator switch
        {
            "EQUALS" => areEqual,
            "DOES_NOT_EQUAL" => !areEqual,
            _ => _ThrowExceptionOrReturnFalse($"Invalid operator: {Operator}")
        };
    }

    private bool _CompareScalarWithList<T>()
    {
        var right = Right != null ? (List<T?>)Right! : new List<T?>();
        return Operator switch
        {
            "ONE_OF" => right.Contains((T?)Left),
            "NONE_OF" => !right.Contains((T?)Left),
            _ => _ThrowExceptionOrReturnFalse($"Invalid operator: {Operator}"),
        };
    }

    private bool _CompareDecimalWithList()
    {
        var left = decimal.Parse(Left?.ToString() ?? "0");
        var right = Right != null ? (List<decimal?>)Right! : new List<decimal?>();
        return Operator switch
        {
            "ONE_OF" => right.Contains(left),
            "NONE_OF" => !right.Contains(left),
            _ => _ThrowExceptionOrReturnFalse($"Invalid operator: {Operator}"),
        };
    }

    private bool _CompareListWithScalar<T>()
    {
        var left = (List<T?>)Left!;
        return Operator switch
        {
            "CONTAINS" => left.Contains((T?)Right),
            "DOES_NOT_CONTAIN" => !left.Contains((T?)Right),
            _ => _ThrowExceptionOrReturnFalse($"Invalid operator: {Operator}"),
        };
    }

    private bool _CompareListWithDecimal()
    {
        var left = (List<decimal?>)Left!;
        decimal? right = Right != null ? decimal.Parse(Right?.ToString() ?? "0") : null;
        return Operator switch
        {
            "CONTAINS" => left.Contains(right),
            "DOES_NOT_CONTAIN" => !left.Contains(right),
            _ => _ThrowExceptionOrReturnFalse($"Invalid operator: {Operator}"),
        };
    }

    private bool _CompareLists<T>(List<T?>? left = null, List<T?>? right = null)
    {
        left ??= (List<T?>)Left!;
        if (right == null)
        {
            if (Right != null)
            {
                if (Right is List<T>)
                    right = ((List<T>)Right).Select(n => (T?)n).ToList();
                else if (Right is List<object?>)
                    right = ((List<object?>)Right).Select(n => (T?)n).ToList();
                else
                    right = (List<T?>)Right;
            }
            else
                right = new List<T?>();
        }

        var equalityOperators = new List<string> { "EQUALS", "DOES_NOT_EQUAL" };
        if (equalityOperators.Contains(Operator))
        {
            var sortedLeft = left.OrderBy(n => n).ToList();
            var sortedRight = right!.OrderBy(n => n).ToList();
            var areEqual = sortedLeft.SequenceEqual(sortedRight);
            return Operator == "EQUALS" ? areEqual : !areEqual;
        }

        var intersectionOperators = new List<string> { "CONTAINS_ANY_OF", "CONTAINS_NONE_OF" };
        if (intersectionOperators.Contains(Operator))
        {
            var hasIntersection = left.Intersect(right).Any();
            return Operator == "CONTAINS_ANY_OF" ? hasIntersection : !hasIntersection;
        }

        var leftSet = new HashSet<T?>(left);
        var rightSet = new HashSet<T?>(right);
        return Operator switch
        {
            "IS_SUBSET_OF" => leftSet.IsSubsetOf(rightSet),
            "IS_SUPERSET_OF" => leftSet.IsSupersetOf(rightSet),
            _ => _ThrowExceptionOrReturnFalse($"Invalid operator: {Operator}"),
        };
    }

    private bool _CompareObjects()
    {
        var isSameInstance = ((Node)Left!).Guid == ((Node?)Right)?.Guid;
        return Operator switch
        {
            "EQUALS" => isSameInstance,
            "DOES_NOT_EQUAL" => !isSameInstance,
            _ => _ThrowExceptionOrReturnFalse($"Invalid operator: {Operator}"),
        };
    }

    private bool _CompareObjectWithList()
    {
        if (Right == null) return Operator == "NONE_OF";

        var rightContainsLeft = ((List<Node>)Right).Select(n => n.Guid).Contains(((Node)Left!).Guid);
        return Operator switch
        {
            "ONE_OF" => rightContainsLeft,
            "NONE_OF" => !rightContainsLeft,
            _ => _ThrowExceptionOrReturnFalse($"Invalid operator: {Operator}"),
        };
    }

    private bool _CompareObjectListWithObject()
    {
        var leftContainsRight = ((List<Node?>)Left!).Select(n => n?.Guid).Contains(((Node?)Right)?.Guid);
        return Operator switch
        {
            "CONTAINS" => leftContainsRight,
            "DOES_NOT_CONTAIN" => !leftContainsRight,
            _ => _ThrowExceptionOrReturnFalse($"Invalid operator: {Operator}"),
        };
    }

    private List<string?>? _ObjectListToGuidList(List<Node?>? nodes)
    {
        return nodes?.Select(n => n?.Guid.ToString()).ToList();
    }

    private bool _ThrowExceptionOrReturnFalse(string message)
    {
        if (_throwIfTypesNotComparable)
            throw new Exception(message);

        return false;
    }

    private bool _IsEmptyList(object? value)
    {
        if (value is List<string?>)
            return ((List<string?>)value).Count == 0;

        if (value is List<decimal?>)
            return ((List<decimal?>)value).Count == 0;

        if (value is List<bool?>)
            return ((List<bool?>)value).Count == 0;

        if (value is List<Node?>)
            return ((List<Node?>)value).Count == 0;

        return false;
    }
}