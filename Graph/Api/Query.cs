namespace Graph.Api;

public class Query
{
}

public class QueryType : ObjectType<Query>
{
    protected override void Configure(IObjectTypeDescriptor<Query> descriptor)
    {
        descriptor
            .Field("getLive")
            .Type<OracleType>()
            .Argument("ids", a => a.Type<NonNullType<ListType<UuidType>>>())
            .Resolve(async context =>
            {
                return await Oracle.NewOracleAsync(
                    context.ArgumentValue<List<Guid>>("ids"),
                    oracleAction: OracleActionEnum.Now
                );
            });

        descriptor
            .Field("getInception")
            .Type<OracleType>()
            .Argument("ids", a => a.Type<NonNullType<ListType<UuidType>>>())
            .Resolve(async context =>
            {
                return await Oracle.NewOracleAsync(
                    context.ArgumentValue<List<Guid>>("ids"),
                    oracleAction: OracleActionEnum.Inception
                );
            });

        descriptor
            .Field("getNext")
            .Type<OracleType>()
            .Argument("ids", a => a.Type<NonNullType<ListType<UuidType>>>())
            .Argument("timestamp", a => a.Type<NonNullType<UnsignedLongType>>())
            .Resolve(async context =>
            {
                return await Oracle.NewOracleAsync(
                    context.ArgumentValue<List<Guid>>("ids"),
                    context.ArgumentValue<ulong>("timestamp"),
                    OracleActionEnum.Next
                );
            });

        descriptor
            .Field("getPrevious")
            .Type<OracleType>()
            .Argument("ids", a => a.Type<NonNullType<ListType<UuidType>>>())
            .Argument("timestamp", a => a.Type<NonNullType<UnsignedLongType>>())
            .Resolve(async context =>
            {
                return await Oracle.NewOracleAsync(
                    context.ArgumentValue<List<Guid>>("ids"),
                    context.ArgumentValue<ulong>("timestamp"),
                    OracleActionEnum.Previous
                );
            });

        descriptor
            .Field("getAtTimestamp")
            .Type<OracleType>()
            .Argument("ids", a => a.Type<NonNullType<ListType<UuidType>>>())
            .Argument("timestamp", a => a.Type<NonNullType<UnsignedLongType>>())
            .Resolve(async context =>
            {
                return await Oracle.NewOracleAsync(
                    context.ArgumentValue<List<Guid>>("ids"),
                    context.ArgumentValue<ulong>("timestamp")
                );
            });
    }
}