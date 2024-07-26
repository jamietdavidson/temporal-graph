using Graph.Core;

namespace Graph.Tests;

public static class Fixtures
{
    public static FuelTank GetFuelTank(string SerialNumber = "F_1", int FuelRemaining = 100, Guid? guid = null)
    {
        var fuelTank = new FuelTank(new NestedDictionary
        {
            {
                "Fields",
                new FlatDictionary
                {
                    { "SerialNumber", SerialNumber },
                    { "FuelRemaining", FuelRemaining }
                }
            }
        }, guid);
        return fuelTank;
    }

    public static List<FuelTank> GetFuelTanks(Guid[] guids)
    {
        List<FuelTank> fuelTanks = new();
        for (var i = 1; i <= guids.Count(); i++)
            fuelTanks.Add(GetFuelTank($"F_{i}", 100, guids[i - 1]));

        return fuelTanks;
    }

    public static Booster GetBooster(string SerialNumber = "B_1", string State = "Unready", Guid? guid = null)
    {
        var booster = new Booster(new NestedDictionary
        {
            {
                "Fields", new FlatDictionary
                {
                    { "SerialNumber", SerialNumber },
                    { "State", State }
                }
            }
        }, guid);
        return booster;
    }

    public static List<Node> GetBoosters(int numBoosters, int startingIdx = 1)
    {
        var i = startingIdx;
        List<Node> boosters = new();
        while (boosters.Count < numBoosters)
            boosters.Add(GetBooster($"B_{i++}"));

        return boosters;
    }

    public static List<Node> GetBoosters(Guid[] guids, int startingIdx = 1)
    {
        List<Node> boosters = new();
        var idx = startingIdx;
        for (var i = 1; i <= guids.Count(); i++)
            boosters.Add(GetBooster($"B_{startingIdx++}", "Unready", guids[i - 1]));

        return boosters;
    }

    public static Rocketship GetRocketship(string name = "Timeless", string serialNumber = "R_1", int numBoosters = 3)
    {
        var boosters = GetBoosters(numBoosters);

        var rocketship = new Rocketship(
            new NestedDictionary
            {
                {
                    "Fields", new FlatDictionary
                    {
                        { "Name", name },
                        { "SerialNumber", serialNumber }
                    }
                },
                {
                    "EdgeCollections", new FlatDictionary
                    {
                        { "Boosters", boosters }
                    }
                }
            }
        );
        return rocketship;
    }

    public static Cursor GetCursor()
    {
        var cursor = new Cursor(GetSession());
        // TODO: update branch with default branch info
        return cursor;
    }

    public static async Task<Cursor> GetCursorWithHistoryAsync()
    {
        var cursor = new Cursor(GetSession());
        var rocketship = GetRocketship(numBoosters: 3);

        rocketship.Cursor = cursor;

        // Inception
        await cursor.KeyframeAsync();

        Thread.Sleep(500);

        // First operation
        await cursor.TogetherAsync(ModeEnum.Data, operation: async Task () =>
        {
            var boosters = (EdgeCollection<Node, Node>?)await rocketship.GetValueAsync("Boosters");
            if (boosters == null) throw new Exception("Failed to get EdgeCollection.");

            var extraBoosters = GetBoosters(3, boosters.Count + 1);

            boosters.Append(extraBoosters);

            var tasks = new List<Task>();
            var i = 1;
            foreach (var booster in extraBoosters)
            {
                var fuelTank = GetFuelTank("F_" + i++);
                tasks.Add(booster.SetValueAsync("FuelTank", fuelTank));
                tasks.Add(booster.SaveAsync());
                tasks.Add(fuelTank.SaveAsync());
            }

            tasks.Add(rocketship.SaveAsync());

            await Task.WhenAll(tasks);
        });

        Thread.Sleep(500);

        // Second operation
        await cursor.TogetherAsync(operation: async Task () =>
        {
            var boosters = (EdgeCollection<Node, Node>?)await rocketship.GetValueAsync("Boosters");
            if (boosters == null) return;

            var tasks = new List<Task>();
            foreach (var booster in await boosters.GetAllNodesAsync())
            {
                tasks.Add(booster.SetValueAsync("State", "Ready"));
                tasks.Add(booster.SaveAsync());
            }

            await Task.WhenAll(tasks);
        });

        return cursor;
    }

    public static Session GetSession(Guid? sampleDataGuid = null)
    {
        return new Session(new InMemoryDataStore(sampleDataGuid));
    }

    public static async Task<Cursor> GetInMemoryTestCursorAsync()
    {
        var cursor = new Cursor(GetSession(Guid.NewGuid()));
        await cursor.SearchAsync(Constants.RocketshipGuid);

        return cursor;
    }

    public static Cursor GetApiTestCursor(Guid sampleDataGuid)
    {
        return new Cursor(GetSession(sampleDataGuid));
    }

    public static Node NewSchemalessNode(
        string tag,
        Cursor? cursor = null,
        NodeRep? nodeRep = null
    )
    {
        return new Node(
            null,
            cursor,
            nodeRep ?? new NodeRep(),
            Utils.TimestampMillis(),
            tag
        );
    }

    public static (SchemaDefinition, List<Node>) GetBicycleSchema(Cursor cursor)
    {
        var seat = NewSchemalessNode("Saddle", cursor);
        var chain = NewSchemalessNode("Chain", cursor);
        var frontWheel = NewSchemalessNode("Wheel", cursor);
        var rearWheel = NewSchemalessNode("Wheel", cursor);
        var leftPedal = NewSchemalessNode("Pedal", cursor);
        var rightPedal = NewSchemalessNode("Pedal", cursor);

        var bellRep = new NodeRep();
        bellRep.Meta.Tag = "Bell";
        bellRep.Meta.Guid = Guid.NewGuid();
        bellRep.Fields.StringFields = new Dictionary<string, string?>
        { { "Sound", "Ding" } };
        var bell = new Node(
            bellRep.Meta.Guid,
            cursor,
            bellRep,
            Utils.TimestampMillis()
        );

        var bicycleRep = new NodeRep();
        bicycleRep.Meta.Tag = "Bicycle";
        bicycleRep.Meta.Guid = Guid.NewGuid();
        bicycleRep.Fields.BooleanFields = new Dictionary<string, bool?>
        {
            { "Preowned", false },
            { "IsCool", true }
        };
        bicycleRep.Fields.StringFields = new Dictionary<string, string?>
        {
            { "Name", "Speedster" },
            { "Color", "Red" },
            { "SomeNullField", null }
        };
        bicycleRep.Fields.NumericFields = new Dictionary<string, decimal?>
        {
            { "Price", 999.99m },
            { "SomeNullDecimal", null }
        };
        bicycleRep.Fields.StringListFields = new Dictionary<string, List<string?>?>
        {
            { "SomeStringList", new List<string?> { "A", "B", null } },
            { "SomeNullStringList", null }
        };
        bicycleRep.Fields.NumericListFields = new Dictionary<string, List<decimal?>?>
        {
            { "SomeNumericList", new List<decimal?> { 1.1m, null, 3.3m } },
            { "SomeNullNumericList", null }
        };
        bicycleRep.Edges.Tags.Add("Seat", seat.Tag);
        bicycleRep.Edges.Values.Add("Seat", seat.Guid);
        bicycleRep.Edges.Tags.Add("Bell", bell.Tag);
        bicycleRep.Edges.Values.Add("Bell", bell.Guid);
        bicycleRep.Edges.Tags.Add("Chain", chain.Tag);
        bicycleRep.Edges.Values.Add("Chain", chain.Guid);
        bicycleRep.EdgeCollections.Tags.Add("Wheels", "Wheel");
        bicycleRep.EdgeCollections.Values.Add("Wheels", new List<Guid> { frontWheel.Guid, rearWheel.Guid });
        bicycleRep.EdgeCollections.Tags.Add("Pedals", "Pedal");
        bicycleRep.EdgeCollections.Values.Add("Pedals", new List<Guid> { leftPedal.Guid, rightPedal.Guid });

        var bicycle = new Node(
            bicycleRep.Meta.Guid,
            cursor,
            bicycleRep,
            Utils.TimestampMillis()
        );

        return (
            new SchemaDefinition(new List<NodeSchema> {
                NodeSchema.FromNodeRep(new NodeRep("Saddle")),
                NodeSchema.FromNodeRep(new NodeRep("Chain")),
                NodeSchema.FromNodeRep(bellRep),
                NodeSchema.FromNodeRep(new NodeRep("Wheel")),
                NodeSchema.FromNodeRep(new NodeRep("Pedal")),
                NodeSchema.FromNodeRep(bicycleRep)
            }),
            new List<Node> {
                bicycle,
                seat,
                bell,
                chain,
                frontWheel,
                rearWheel,
                leftPedal,
                rightPedal
            }
        );
    }

    public static Node GetBicycleNode(Cursor cursor)
    {
        var bicycleRep = new NodeRep();
        bicycleRep.Meta.Tag = "Bicycle";
        bicycleRep.Meta.Guid = Guid.NewGuid();
        bicycleRep.Fields.StringFields = new Dictionary<string, string?>
        {
            { "Name", "Speedster" },
            { "Color", "Red" },
            { "SomeNullField", null }
        };
        bicycleRep.Fields.NumericFields = new Dictionary<string, decimal?>
        {
            { "Price", 999.99m },
            { "SomeNullDecimal", null }
        };

        bicycleRep.Edges.Tags.Add("Seat", "Saddle");
        bicycleRep.Edges.Tags.Add("Bell", "Bell");
        bicycleRep.Edges.Tags.Add("Chain", "Chain");
        bicycleRep.EdgeCollections.Tags.Add("Wheels", "Wheel");

        return new Node(
            bicycleRep.Meta.Guid,
            cursor,
            bicycleRep,
            Utils.TimestampMillis()
        );
    }
}