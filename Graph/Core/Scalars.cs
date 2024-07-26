namespace Graph.Core;

public abstract class Scalar<T> : IGraphObject
{
    private T? _data;
    private T? _delta;
    private bool _hasDelta;
    protected Node _node;

    public Scalar(Node node)
    {
        _node = node ?? throw new ArgumentNullException(nameof(node));
    }

    public T? Value
    {
        get
        {
            if (_hasDelta) return _delta;
            return _data;
        }
        set
        {
            _delta = value;

            NodeEventArgs args = new();
            args.Value = value;
            OnValueChanged(args);
        }
    }

    public event EventHandler<NodeEventArgs>? ValueChanged;

    public virtual void OnValueChanged(NodeEventArgs e)
    {
        EventHandler<NodeEventArgs>? handler = ValueChanged;
        if (handler != null) handler(this, e);
    }

    public void SetInitialValue(object value)
    {
        if (value == null) return;
        _data = (T)value;
        _hasDelta = false;
    }

    public Task SetValueAsync(object? value)
    {
        Value = (T?)value;
        _hasDelta = true;
        return Task.CompletedTask;
    }

    public Task DeltaToDataAsync()
    {
        if (!_hasDelta)
            return Task.CompletedTask;
        _data = _delta;
        _delta = default;
        _hasDelta = false;
        return Task.CompletedTask;
    }

    public Task ClearDeltaAsync()
    {
        _delta = default;
        _hasDelta = false;
        return Task.CompletedTask;
    }

    public Task<object?> GetValueAsync(ModeEnum mode, bool silent = false)
    {
        if (mode == ModeEnum.Data || (mode == ModeEnum.Either && !_hasDelta))
        {
            if (_hasDelta && !silent) throw new InvalidOperationException("Requested data when delta was set.");
            return Task.FromResult<object?>(_data);
        }

        if (!_hasDelta) return Task.FromResult<object?>(new Ignorable());
        return Task.FromResult<object?>(_delta);
    }

    // Necessary for setting dynamically loaded values during Node instantiation.
    // We don't want to trigger the ValueChanged event handler.
    public void SetLoadedValue(string fieldName, Dictionary<string, object>? values)
    {
        if (values != null && values.TryGetValue(fieldName, out var value))
        {
            _data = (T)value;
            _hasDelta = false;
        }
    }
}

public class Boolean : Scalar<bool?>
{
    public Boolean(Node node) : base(node)
    {
    }
}

public class String : Scalar<string>
{
    public String(Node node) : base(node)
    {
    }
}

public class Integer : Scalar<int?>
{
    public Integer(Node node) : base(node)
    {
    }
}

public class Decimal : Scalar<decimal?>
{
    public Decimal(Node node) : base(node)
    {
    }
}

public class BooleanList : Scalar<List<bool?>?>
{
    public BooleanList(Node node) : base(node)
    {
        ValueChanged += _ValidateList;
    }

    static void _ValidateList(object? sender, NodeEventArgs e)
    {
        if (e.Value == null) return;
    
        var val = (List<bool?>)e.Value;
        if (val.Where(x => x is bool || x == null).Count() != val.Count)
            throw new InvalidOperationException("List values must booleans.");
    }
}
public class StringList : Scalar<List<string?>?>
{
    public StringList(Node node) : base(node)
    {
        ValueChanged += _ValidateList;
    }

    static void _ValidateList(object? sender, NodeEventArgs e)
    {
        if (e.Value == null) return;
    
        var val = (List<string?>)e.Value;
        if (val.Where(x => x is string || x == null).Count() != val.Count)
            throw new InvalidOperationException("List values must strings.");
    }
}

public class NumericList : Scalar<List<decimal?>?>
{
    public NumericList(Node node) : base(node)
    {
        ValueChanged += _ValidateList;
    }

    static void _ValidateList(object? sender, NodeEventArgs e)
    {
        if (e.Value == null) return;
    
        var val = (List<decimal?>)e.Value;
        if (val.Where(x => x is decimal || x == null).Count() != val.Count)
            throw new InvalidOperationException("List values must be numeric.");
    }
}