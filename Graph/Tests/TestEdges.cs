using Graph.Core;

namespace Graph.Tests;

public class TestEdges
{
    [Test]
    public async Task TestAttachEdgeToNode()
    {
        var cursor = Fixtures.GetCursor();
        var booster = Fixtures.GetBooster();
        var fuelTank = Fixtures.GetFuelTank();

        booster.Cursor = cursor;

        await booster.SetValueAsync("FuelTank", fuelTank);

        var retrievedFuelTank = (FuelTank?)await booster.GetValueAsync("FuelTank", ModeEnum.Delta);

        Assert.That(retrievedFuelTank?.Guid, Is.EqualTo(fuelTank.Guid));
        Assert.That(retrievedFuelTank?.Cursor, Is.SameAs(booster.Cursor));
        Assert.That(cursor.NodeGuids.ContainsKey(fuelTank.Guid));
    }

    [Test]
    public async Task TestDetachEdgeNode()
    {
        var cursor = Fixtures.GetApiTestCursor(Guid.NewGuid());
        var boosterGuid = Constants.AdditionalBoosterGuids[0];
        var boosterWithFuelTank = await cursor.SearchAsync(boosterGuid);

        Assert.That(boosterWithFuelTank, Is.InstanceOf(typeof(Booster)));

        var fuelTank = await boosterWithFuelTank!.GetValueAsync("FuelTank");

        Assert.That(fuelTank, Is.InstanceOf(typeof(FuelTank)));

        await boosterWithFuelTank.SetValueAsync("FuelTank", null);
        await boosterWithFuelTank.SaveAsync();

        boosterWithFuelTank = await cursor.SearchAsync(boosterGuid);

        Assert.That(boosterWithFuelTank, Is.InstanceOf(typeof(Booster)));

        fuelTank = await boosterWithFuelTank!.GetValueAsync("FuelTank");

        Assert.That(fuelTank, Is.Null);
    }

    [Test]
    public async Task TestLazyLoad()
    {
        // use cursor that has not yet loaded the edge nodes.
        var cursor = await Fixtures.GetInMemoryTestCursorAsync();
        var expectedNodeCount = 1;

        Assert.That(cursor.Nodes.Count, Is.EqualTo(expectedNodeCount));

        var rocketship = cursor.Nodes[0];
        var boosters = (EdgeCollection<Node, Node>?)await rocketship.GetValueAsync("Boosters");

        Assert.That(cursor.Nodes.Count, Is.EqualTo(expectedNodeCount));

        // Load one booster
        var boosterWithFuelTank = await boosters!.GetNodeAtIndexAsync(3);
        expectedNodeCount++;

        Assert.That(cursor.Nodes.Count, Is.EqualTo(expectedNodeCount));
    }

    [Test]
    public async Task TestGetEitherValue()
    {
        var cursor = Fixtures.GetCursor();
        var booster = Fixtures.GetBooster();
        var fuelTank = Fixtures.GetFuelTank();

        booster.Cursor = cursor;

        // should get data
        var edgeNode = (Node?)await booster.GetValueAsync("FuelTank", ModeEnum.Either);
        Assert.That(edgeNode, Is.Null);

        await booster.SetValueAsync("FuelTank", fuelTank);

        // should get delta
        edgeNode = (Node?)await booster.GetValueAsync("FuelTank", ModeEnum.Either);
        Assert.That(edgeNode, Is.InstanceOf(typeof(FuelTank)));
        Assert.That(edgeNode!.Guid, Is.EqualTo(fuelTank.Guid));

        await booster.DeltaToDataAsync();
        Assert.That(await booster.GetValueAsync("FuelTank", ModeEnum.Delta), Is.InstanceOf(typeof(Ignorable)));
        edgeNode = (Node?)await booster.GetValueAsync("FuelTank", ModeEnum.Either);
        Assert.That(edgeNode, Is.InstanceOf(typeof(FuelTank)));
        Assert.That(edgeNode!.Guid, Is.EqualTo(fuelTank.Guid));
    }
}