namespace Graph.Core;

public interface IGraphObject
{
    event EventHandler<NodeEventArgs>? ValueChanged;

    void SetInitialValue(object value);

    Task DeltaToDataAsync();

    Task ClearDeltaAsync();

    Task SetValueAsync(object? value);

    Task<object?> GetValueAsync(ModeEnum mode = ModeEnum.Data, bool silent = false);
}

public interface IGraphObjectWithCursor : IGraphObject
{
    Cursor? Cursor { get; set; }
}