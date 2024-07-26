using Graph.Mongo;
using Graph.Core;
using Graph.Templates;

namespace Graph.Tests;

public static class MongoFixtures
{
    public static async Task<(StateMapExecutor, Guid)> GetLoadedStateMapAsync(
        string oisSchemaPath = "schemas/test/small_example_schema.json",
        string asPartyName = "Project"
    )
    {
        var stateMap = await StateMapFromTemplateAsync(oisSchemaPath);
        var stateMapGuid = (Guid)stateMap.Guid!;

        var partyId = await Mongo.Utils.GetPartyGuidAsync(stateMap, partyName: asPartyName);

        return (await StateMapExecutor.LoadAsync(stateMapGuid, partyId, stateMap.Cursor), partyId);
    }

    public static async Task<StateMapExecutor> StateMapFromTemplateAsync(
        string oisSchemaPath = "schemas/test/small_example_schema.json"
    )
    {
        var ds = new MongoDataStore();
        var cursor = new Cursor(new Session(ds));
        var ignoreExistingTemplates = false;
        Guid? templateGuid = ignoreExistingTemplates ? null : await ds.GetOisSchemaAsync(oisSchemaPath);

        if (templateGuid == null)
            templateGuid = await Templates.Utils.SaveTemplateAsync(oisSchemaPath, cursor, ignoreExistingTemplates);

        var stateMap = new StateMapExecutor((Guid)templateGuid);
        await stateMap.FromTemplateAsync((Guid)templateGuid, cursor);

        return stateMap;
    }
}