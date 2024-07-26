namespace Graph.Core;

#pragma warning disable CS1998
public class Edge<FromNode, ToNode> : IGraphObjectWithCursor where FromNode : Node where ToNode : Node
{
    private readonly FromNode _fromNode;
    private ToNode? _data;
    private ToNode? _delta;
    private bool _hasDelta;
    private bool _isLoaded;

    public Edge(FromNode fromNode, ToNode? toNode)
    {
        _fromNode = fromNode;
        _data = toNode;
    }

    public ToNode? Value
    {
        get
        {
            if (_hasDelta) return _delta;
            return _data;
        }
        set
        {
            _delta = value;
            if (_delta != null) _delta.Cursor = _fromNode.Cursor;
            ValueChanged?.Invoke(this, new NodeEventArgs());
        }
    }

    public bool IsLoaded
    {
        get => _isLoaded;
        set
        {
            _isLoaded = value;

            if (_data != null)
                _data.IsLoaded = value;
            else if (value)
                throw new Exception("Cannot mark null node as loaded.");
        }
    }

    public event EventHandler<NodeEventArgs>? ValueChanged;

    public Cursor? Cursor
    {
        get => (_hasDelta ? _delta : _data)?.Cursor;
        set
        {
            var toNode = _hasDelta ? _delta : _data;
            if (toNode != null) toNode.Cursor = value;
        }
    }

    public void SetInitialValue(object value)
    {
        _data = (ToNode)value;
        _hasDelta = false;
    }

    public async Task SetValueAsync(object? value)
    {
        Value = (ToNode?)value;
        _hasDelta = true;
    }

    public async Task<object?> GetValueAsync(ModeEnum mode = ModeEnum.Data, bool silent = false)
    {
        if (mode == ModeEnum.Data || (mode == ModeEnum.Either && !_hasDelta))
        {
            if (_hasDelta && !silent) throw new InvalidOperationException("Requested data when delta was set.");

            return _data;
        }

        if (!_hasDelta) return new Ignorable();
        return _delta;
    }

    public async Task DeltaToDataAsync()
    {
        if (!_hasDelta) return;

        _data = _delta;
        _delta = default;
        _hasDelta = false;
    }

    public async Task ClearDeltaAsync()
    {
        _delta = default;
        _hasDelta = false;
    }

    private async Task _TryLoadValue()
    {
        if (IsLoaded || _data == null) return;

        var cursor = Cursor;
        if (cursor == null) return;

        // Get the ToNode from the cursor if possible.
        if (cursor.NodeGuids.ContainsKey(_data.Guid) && cursor.NodeGuids[_data.Guid].IsLoaded)
        {
            _data = (ToNode)cursor.NodeGuids[_data.Guid];
            IsLoaded = true;
        }
        else
        {
            _data = (ToNode?)await cursor.LazyLoad(_data.Guid);
            if (_data != null) IsLoaded = true;
        }

        if (!IsLoaded && _data != null) _data.IsLoaded = false;
    }
}