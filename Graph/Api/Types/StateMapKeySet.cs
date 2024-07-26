using Graph.Mongo;

namespace Graph.Api;

public class StateMapKeySet
{
    public Guid StateMapId { get; set; }

    public List<StateMapKeyPair> PartyKeys { get; set; }

    public StateMapKeySet(StateMapExecutor stateMap)
    {
        StateMapId = (Guid)stateMap.Guid!;
        PartyKeys = new List<StateMapKeyPair>();

        foreach (var partyGuid in stateMap.PartyGuids)
        {
            PartyKeys.Add(new StateMapKeyPair
            {
                PartyId = partyGuid,
                PrivateKey = "private-key",
                PublicKey = "public-key",
            });
        }
    }
}

public class StateMapKeySetType : ObjectType<StateMapKeySet>
{
}

public class StateMapKeyPair
{
    public decimal TemplatePartyId { get; set; }

    public Guid PartyId { get; set; }

    public string PrivateKey { get; set; }

    public string PublicKey { get; set; }

}

public class StateMapKeyPairType : ObjectType<StateMapKeyPair>
{
}