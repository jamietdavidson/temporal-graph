namespace Graph.Core;

public enum ModeEnum
{
    Data,
    Delta,
    Either
}

public enum CursorStateEnum
{
    Live,
    Rewind
}

public enum DatesEnum : ulong
{
    Inception = 1577854800000 // Utils.TimestampMillis(new DateTime(2020, 1, 1));
}

public enum NodeEnum
{
    Fields,
    Edges,
    EdgeCollections
}