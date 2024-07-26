using System.Reflection;
using Flow.Net.Sdk.Client.Http;
using Flow.Net.Sdk.Core;
using Flow.Net.Sdk.Core.Client;
using Flow.Net.Sdk.Core.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Newtonsoft.Json;
using Path = System.IO.Path;

namespace Graph.Core;

#pragma warning disable CS0618
public static class Environment
{
    private static IMongoDatabase _mongoDatabase;
    private static FlowHttpClient _flowHttpClient;

    private static string? _GetEnvironmentVariable(string name)
    {
        return System.Environment.GetEnvironmentVariable(name);
    }

    public static IMongoDatabase GetMongoDatabase(string? databaseName = null)
    {
        if (_mongoDatabase != null) return _mongoDatabase;

        BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
        BsonDefaults.GuidRepresentationMode = GuidRepresentationMode.V3;

        var connectionString = _GetEnvironmentVariable("MONGO_CONNECTION_STRING") ?? "mongodb://127.0.0.1:27017";
        var clientSettings = MongoClientSettings.FromConnectionString(connectionString);
        clientSettings.LinqProvider = LinqProvider.V3;
        var client = new MongoClient(clientSettings);
        var database = client.GetDatabase("default");

        _mongoDatabase = database;
        return _mongoDatabase;
    }

    public static string GetApplicationDirectory()
    {
        // Note that this doesn't work when the build
        // output is not a subdirectory of the project directory.
        var directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        directory = Path.GetFullPath(directory);
        directory = Path.TrimEndingDirectorySeparator(directory);

        while (!directory.EndsWith("Graph"))
        {
            directory = Path.GetDirectoryName(directory)!;
            directory = Path.TrimEndingDirectorySeparator(directory);
            if (directory == null || directory == "") throw new Exception("Could not find application directory.");
        }

        directory = Path.GetDirectoryName(directory);
        directory = directory! + Path.DirectorySeparatorChar;
        return directory!;
    }
}