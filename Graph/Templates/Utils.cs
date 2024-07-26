using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Graph.Core;

namespace Graph.Templates;

public static class Utils
{
    public static async Task<Guid> SaveTemplateAsync(
        string oisSchemaPath,
        Cursor cursor,
        bool ignoreExisting = false
    )
    {
        if (cursor.Session.Database is MongoDataStore && !ignoreExisting)
        {
            // Don't allow the same template to be saved twice
            var ds = (MongoDataStore)cursor.Session.Database;
            var existingTemplateGuid = await ds.GetOisSchemaAsync(oisSchemaPath);
            if (existingTemplateGuid != null)
                throw new Exception($"Template already exists on the graph with Guid: {existingTemplateGuid}");
        }

        using (var streamReader = await GetTemplateAsync(oisSchemaPath))
        {
            var template = await ParseTemplateAsync(streamReader.ReadToEnd(), oisSchemaPath, cursor);

            var templateNode = template.ToNode();
            templateNode.Cursor = cursor;
            await templateNode.SaveAsync();

            return templateNode.Guid;
        }
    }

    public static async Task<Guid> SaveTestTemplateAsync(string filePath, Cursor cursor)
    {
        using (StreamReader file = File.OpenText(Core.Environment.GetApplicationDirectory() + filePath))
        {
            var template = await ParseTemplateAsync(file.ReadToEnd(), oisSchemaPath: null, cursor);

            var templateNode = template.ToNode();
            templateNode.Cursor = cursor;
            await templateNode.SaveAsync();

            return templateNode.Guid;
        }
    }

    public static async Task<TemplateStateMap> ParseTemplateAsync(string json, string? oisSchemaPath, Cursor? cursor = null)
    {

        if (cursor == null)
            cursor = new Cursor(new Session(new MongoDataStore()));

        var template = new TemplateStateMap(oisSchemaPath);
        await cursor.TogetherAsync(operation: async () =>
        {
            var nodeIntermediaries = new List<INodeIntermediary>();

            JObject jsonObject = (JObject)JsonConvert.DeserializeObject(json)!;
            var properties = jsonObject.Properties();

            // Make sure it's actually a schema file
            var expectedPropertyNames = new List<string>
            {
                "standard",
                "terms",
                "parties",
                "object_types",
                "object_promises",
                "actions",
                "checkpoints",
                "pipelines"
            };
            var actualPropertyNames = properties.Select(p => p.Name).ToList();
            if (!expectedPropertyNames.All(name => actualPropertyNames.Contains(name)))
                throw new Exception("Invalid oisSchemaPath");

            foreach (var prop in properties)
            {
                var name = prop.Name;
                if (name == "object_types")
                {
                    (
                        template.ObjectTypes,
                        template.ObjectTypesSchemaDefinition
                    ) = await ParseObjectTypesAsync(prop.Value, cursor);
                }
                else if (name == "standard")
                    template.Standard = (string)prop.Value!;
                else if (name == "terms")
                    (_, template.Terms) = await ParseObjectsAsync<TemplateTerm>(prop.Value, cursor);
                else if (name == "parties")
                    (_, template.Parties) = await ParseObjectsAsync<TemplateParty>(prop.Value, cursor);
                else if (name == "object_promises")
                {
                    (var objectPromiseIntermediaries, template.ObjectPromises) = await ParseObjectsAsync<TemplateObjectPromise>(prop.Value, cursor);
                    nodeIntermediaries.AddRange(objectPromiseIntermediaries);
                }
                else if (name == "actions")
                {
                    (var actionIntermediaries, template.Actions) = await ParseObjectsAsync<TemplateAction>(prop.Value, cursor);
                    nodeIntermediaries.AddRange(actionIntermediaries);
                }
                else if (name == "checkpoints")
                {
                    (var checkpointIntermediaries, template.Checkpoints) = await ParseObjectsAsync<TemplateCheckpoint>(prop.Value, cursor);
                    nodeIntermediaries.AddRange(checkpointIntermediaries);
                }
                else if (name == "thread_groups")
                {
                    (var threadGroupIntermediaries, template.ThreadGroups) = await ParseObjectsAsync<TemplateThreadGroup>(prop.Value, cursor);
                    nodeIntermediaries.AddRange(threadGroupIntermediaries);
                }
            }

            var tasks = new List<Task>();

            foreach (var node in cursor.Nodes)
                tasks.Add(node.SaveAsync());

            foreach (var nodeIntermediary in nodeIntermediaries)
                if (nodeIntermediary is TemplateObjectPromise)
                    tasks.Add(((TemplateObjectPromise)nodeIntermediary).ResolveReferencesAsync(template, cursor));
                else if (nodeIntermediary is TemplateAction)
                    tasks.Add(((TemplateAction)nodeIntermediary).ResolveReferencesAsync(template, cursor));
                else if (nodeIntermediary is TemplateCheckpoint)
                    tasks.Add(((TemplateCheckpoint)nodeIntermediary).ResolveReferencesAsync(template, cursor));
                else if (nodeIntermediary is TemplateThreadGroup)
                    tasks.Add(((TemplateThreadGroup)nodeIntermediary).ResolveReferencesAsync(template, cursor));

            await Task.WhenAll(tasks);
            await template.SetEvergreenActionsAsync(cursor);
        });

        template.AllGuids = cursor.NodeGuids.Keys.ToList();
        return template;
    }

    public static async Task<(List<Guid>, SchemaDefinition)> ParseObjectTypesAsync(
        JToken objectsToken, Cursor cursor
    )
    {
        var intermediaries = new List<TemplateObjectType>();
        var toNodeTasks = new List<Task<Node>>();
        foreach (JObject objectToken in objectsToken.Children())
        {
            INodeIntermediary intermediary = JsonConvert.DeserializeObject<TemplateObjectType>(objectToken.ToString())!;
            intermediaries.Add((TemplateObjectType)intermediary);
            toNodeTasks.Add(intermediary.ToNodeAsync(cursor));
        }

        // Create node schemas without edge types
        foreach (var intermediary in intermediaries)
            intermediary.ToNodeSchema();

        var nodes = await Task.WhenAll(toNodeTasks);
        var tasks = nodes.Select(node => node.SaveAsync());

        // Set edge types for node schemas (now that all types exist)
        foreach (var intermediary in intermediaries)
            tasks.Append(intermediary.SetNodeSchemaEdgeTypesAsync(nodes.ToList()));

        await Task.WhenAll(tasks);

        return (
            nodes.Select(node => node.Guid).ToList(),
            new SchemaDefinition(
                intermediaries.Select(i => i.NodeSchema).ToList()
            )
        );
    }

    public static async Task<(List<INodeIntermediary>, List<Guid>)> ParseObjectsAsync<T>(
        JToken objectsToken, Cursor cursor
    ) where T : INodeIntermediary
    {
        var intermediaries = new List<INodeIntermediary>();
        var toNodeTasks = new List<Task<Node>>();
        foreach (JObject objectToken in objectsToken.Children())
        {
            INodeIntermediary intermediary = JsonConvert.DeserializeObject<T>(objectToken.ToString())!;

            if (intermediary is TemplateAction)
                ((TemplateAction)intermediary).SetOperationType(objectToken);

            intermediaries.Add(intermediary);
            toNodeTasks.Add(intermediary.ToNodeAsync(cursor));
        }
        var nodes = await Task.WhenAll(toNodeTasks);
        return (intermediaries, nodes.Select(node => node.Guid).ToList());
    }

    public static string DetermineFieldType(object? value)
    {
        if (value is JArray)
        {
            if (((JArray)value).Count == 0)
                return "null";

            foreach (var item in ((JArray)value).Children())
            {
                var jtokenType = ((JToken?)item.Value<object>())?.Type;

                switch (jtokenType)
                {
                    case null:
                        continue;
                    case JTokenType.String:
                        return "string_list";
                    case JTokenType.Boolean:
                        return "boolean_list";
                    case JTokenType.Float:
                    case JTokenType.Integer:
                        return "numeric_list";
                    default:
                        throw new Exception("Cannot determine list type");
                }
            }
        }

        if (value is bool)
            return "boolean";

        if (value is string)
            return "string";

        if (decimal.TryParse(value?.ToString() ?? "", out _))
            return "numeric";

        if (value is List<string?>)
            return "string_list";

        if (value is List<bool?>)
            return "boolean_list";

        if (value is List<decimal?>)
            return "numeric_list";

        return "string";
    }

    public static async Task<StreamReader> GetTemplateAsync(string path)
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "test");
        client.DefaultRequestHeaders.Add("Accept", "application/vnd.github.raw+json");
        HttpResponseMessage response = await client.GetAsync(
            "https://api.github.com/repos/natureblocks/open-impact-standards/contents/" + path
        );
        HttpContent responseContent = response.Content;
        return new StreamReader(await responseContent.ReadAsStreamAsync());
    }
}