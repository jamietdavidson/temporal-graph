namespace Graph.Api;

public enum OracleActionEnum
{
    Inception,
    Previous,
    Next,
    Now
}

public class OracleActionEnumType : EnumType<OracleActionEnum>
{
}