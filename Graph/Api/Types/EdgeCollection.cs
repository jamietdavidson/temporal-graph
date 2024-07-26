using Graph.Core;

namespace Graph.Api;

public class EdgeCollection
{
    private readonly EdgeCollection<Core.Node, Core.Node> _edgeCollection;

    private PageInfo? _pageInfo;

    public EdgeCollection(string edgeCollectionKey,
        EdgeCollection<Core.Node, Core.Node> edgeCollection)
    {
        Key = edgeCollectionKey;
        _edgeCollection = edgeCollection;
        Tag = _edgeCollection.Tag;
    }

    public string Key { get; set; }

    public string Tag { get; set; }

    public int GetCount()
    {
        return _edgeCollection.Count;
    }

    [GraphQLIgnore]
    public List<Guid> GetNodeGuids(string? after, int? first)
    {
        var guids = new List<Guid>();

        if (first != null)
            try
            {
                guids = _edgeCollection.GetGuidsForRange(after == null ? null : new Guid(after), (int)first);
            }
            catch (Exception)
            {
                throw new GraphQLException($"Invalid pagination arguments provided for edge collection key: '{Key}'");
            }

        _SetPageInfo(guids);

        return guids;
    }

    public async Task<PageInfo> GetPageInfoAsync()
    {
        return await Task.Run(() =>
        {
            var attempts = 0;
            while (_pageInfo == null)
            {
                if (attempts++ > 1000)
                    return new PageInfo(false, false, null, null);

                Thread.Sleep(1);
            }

            Console.WriteLine(attempts);
            return _pageInfo!;
        });
    }

    private void _SetPageInfo(List<Guid> guids)
    {
        Guid? startCursor = guids.Any() ? guids.First() : null;
        Guid? endCursor = guids.Any() ? guids.Last() : null;
        var hasNextPage = _edgeCollection.GetNext(endCursor) != null;
        var hasPreviousPage = _edgeCollection.GetPrevious(startCursor) != null;

        _pageInfo = new PageInfo
        (
            hasNextPage,
            hasPreviousPage,
            startCursor != null ? startCursor.ToString() : null,
            endCursor != null ? endCursor.ToString() : null
        );
    }
}

public class EdgeCollectionType : ObjectType<EdgeCollection>
{
    protected override void Configure(IObjectTypeDescriptor<EdgeCollection> descriptor)
    {
        descriptor
            .Field("count")
            .Type<IntType>()
            .Resolve(context => context.Parent<EdgeCollection>().GetCount());

        descriptor
            .Field("ids")
            .Argument("paginationArguments", a => a.Type<ListType<EdgeCollectionArgsType>>())
            .Type<ListType<UuidType>>()
            .Resolve(context =>
            {
                var edgeCollection = context.Parent<EdgeCollection>();

                string? after = null;
                int? first = null;

                var paginationArguments = context.ArgumentValue<List<EdgeCollectionArgs>>("paginationArguments");
                if (paginationArguments != null)
                    foreach (var edgeCollectionArgs in paginationArguments)
                        if (edgeCollectionArgs?.Key == edgeCollection.Key)
                        {
                            after = edgeCollectionArgs.After;
                            first = edgeCollectionArgs.First;
                            break;
                        }


                return edgeCollection.GetNodeGuids(after, first);
            });

        descriptor
            .Field("pageInfo")
            .Type<PageInfoType>()
            .Resolve(async context => await context.Parent<EdgeCollection>().GetPageInfoAsync());
    }
}

public class EdgeCollectionArgs
{
    public string Key { get; set; }

    public string? After { get; set; }

    public int? First { get; set; }
}

public class EdgeCollectionArgsType : InputObjectType<EdgeCollectionArgs>
{
}

public class PageInfo
{
    public PageInfo(bool hasNextPage, bool hasPreviousPage, string? firstId, string? lastId)
    {
        HasNextPage = hasNextPage;
        HasPreviousPage = hasPreviousPage;
        FirstId = firstId;
        LastId = lastId;
    }

    public bool HasNextPage { get; set; }

    public bool HasPreviousPage { get; set; }

    public string? FirstId { get; set; }

    public string? LastId { get; set; }
}

public class PageInfoType : ObjectType<PageInfo>
{
}