namespace Graph.Core;

public class Session
{
    public Session(AbstractDataStore database)
    {
        Database = database;
    }

    public AbstractDataStore Database { get; }
}