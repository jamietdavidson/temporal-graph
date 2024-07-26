using Graph.Core;

namespace Graph.Tests;

public class TestDataStores
{
    [Test]
    public async Task TestMongoDataStore()
    {
        MongoDataStore ds = new();
        var timestamp = Utils.TimestampMillis();
        var boosters = Fixtures.GetBoosters(3);
        Query query = new();

        await ds.SaveAsync(boosters, timestamp, ModeEnum.Data);

        // Test with Query.At
        var result = await ds.SearchAsync(query.At(timestamp).Include(boosters));

        Assert.That(result.timestamp, Is.EqualTo(timestamp));

        for (var i = 0; i < boosters.Count(); i++)
            foreach (var node in result.nodes)
                if (node.Guid == boosters[i].Guid)
                    CollectionAssert.AreEqual(
                        boosters[i].AsDictionaryAsync().Result,
                        node.AsDictionaryAsync().Result
                    );

        // Test with Query.Include
        boosters.RemoveAt(0);
        var guidSubset = boosters.Select(b => b.Guid).ToList();
        result = await ds.SearchAsync(new Query().At(timestamp).Include(guidSubset));

        Assert.That(result.timestamp, Is.EqualTo(timestamp));

        for (var i = 0; i < boosters.Count(); i++)
            CollectionAssert.AreEqual(boosters[i].AsDictionaryAsync().Result,
                result.nodes[i].AsDictionaryAsync().Result);

        // TODO: test with Query.After

        // Test At arbitrary timestamps
        var timestamps = new List<ulong>
        {
            Utils.TimestampMillis(), // now
            timestamp + 5000
        };

        foreach (var ts in timestamps)
        {
            result = await ds.SearchAsync(new Query().At(ts).Include(boosters));

            foreach (var booster in boosters)
                CollectionAssert.AreEquivalent(booster.AsDictionaryAsync().Result,
                    result.nodes.Select(n => n)
                        .Where(n => n.Guid == booster.Guid)
                        .First().AsDictionaryAsync().Result);
        }

        // Test empty results
        var resultlessQueries = new List<Query>
        {
            new Query().At(timestamp - 1000).Include(boosters), // too early
            new Query().At(timestamp)
                .Include(Fixtures.GetBoosters(1)) // Include a Guid that doesn't exist in the datastore
        };

        foreach (var q in resultlessQueries)
        {
            result = await ds.SearchAsync(q);
            Assert.That(result.timestamp, Is.Null);
            Assert.That(result.nodes, Is.Empty);
        }
    }

    [Test]
    public async Task TestInMemoryDataStore()
    {
        InMemoryDataStore ds = new();
        var timestamp = Utils.TimestampMillis();
        var boosters = Fixtures.GetBoosters(3);
        Query query = new();

        await ds.SaveAsync(boosters, timestamp, ModeEnum.Data);

        // Test with Query.At
        var result = await ds.SearchAsync(query.At(timestamp).Include(boosters));

        Assert.That(result.timestamp, Is.EqualTo(timestamp));

        for (var i = 0; i < boosters.Count(); i++)
            CollectionAssert.AreEqual(boosters[i].AsDictionaryAsync().Result,
                result.nodes[i].AsDictionaryAsync().Result);

        // Test with Query.Include
        boosters.RemoveAt(0);
        var guidSubset = boosters.Select(b => b.Guid).ToList();
        result = await ds.SearchAsync(new Query().At(timestamp).Include(guidSubset));

        Assert.That(result.timestamp, Is.EqualTo(timestamp));

        for (var i = 0; i < boosters.Count(); i++)
            CollectionAssert.AreEqual(boosters[i].AsDictionaryAsync().Result,
                result.nodes[i].AsDictionaryAsync().Result);

        // TODO: test with Query.After

        // Test At arbitrary timestamps
        var timestamps = new List<ulong>
        {
            Utils.TimestampMillis(), // now
            timestamp + 5000
        };

        foreach (var ts in timestamps)
        {
            result = await ds.SearchAsync(new Query().At(ts).Include(boosters));

            foreach (var booster in boosters)
                CollectionAssert.AreEquivalent(booster.AsDictionaryAsync().Result,
                    result.nodes.Select(n => n)
                        .Where(n => n.Guid == booster.Guid)
                        .First().AsDictionaryAsync().Result);
        }

        // Test empty results
        var resultlessQueries = new List<Query>
        {
            new Query().At(timestamp), // nothing included
            new Query().Include(boosters).At(timestamp - 1000), // too early
            new Query().At(timestamp)
                .Include(Fixtures.GetBoosters(1)), // Include a Guid that doesn't exist in the datastore
            new() // Blank query
        };

        foreach (var q in resultlessQueries)
        {
            result = await ds.SearchAsync(q);
            Assert.That(result.timestamp, Is.Null);
            Assert.That(result.nodes, Is.Empty);
        }
    }

    [Test]
    public async Task TestGetInMemoryDataStoreTimestamps()
    {
        var ds = new InMemoryDataStore(Guid.NewGuid());

        // rocketship should be present at the first 2 timestamps
        CollectionAssert.AreEquivalent(new List<ulong> { Constants.Timestamps[0], Constants.Timestamps[1] },
            await ds.GetNodeTimestampsAsync(Constants.RocketshipGuid));

        // B_1 booster should be present at the first and third
        CollectionAssert.AreEquivalent(new List<ulong> { Constants.Timestamps[0], Constants.Timestamps[2] },
            await ds.GetNodeTimestampsAsync(Constants.BoosterGuids[0]));

        // additional boosters should be present at the second and third
        CollectionAssert.AreEquivalent(new List<ulong> { Constants.Timestamps[1], Constants.Timestamps[2] },
            await ds.GetNodeTimestampsAsync(Constants.AdditionalBoosterGuids[0]));

        // fuel tanks should only be present at the second timestamp
        CollectionAssert.AreEquivalent(new List<ulong> { Constants.Timestamps[1] },
            await ds.GetNodeTimestampsAsync(Constants.FuelTankGuids[0]));

        // new guid should not be present at any
        CollectionAssert.AreEquivalent(new List<ulong>(),
            await ds.GetNodeTimestampsAsync(Guid.NewGuid()));
    }

}