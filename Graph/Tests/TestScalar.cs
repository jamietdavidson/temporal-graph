using System.Reflection;
using Graph.Core;

namespace Graph.Tests;

public class TestScalar
{
    [Test]
    public async Task TestBooster()
    {
        var booster = new Booster(new NestedDictionary());
        // Usually we would not be calling GetValueAsync on a node that has yet to be saved.
        // For this test, it is necessary to manually specify that the node IsLoaded.
        booster.IsLoaded = true;

        var cursor = Fixtures.GetCursor();
        booster.Cursor = cursor;

        await booster.SetValueAsync("State", "Unready");

        Assert.That(await booster.GetValueAsync("State", ModeEnum.Data, true), Is.Null);
        Assert.That(await booster.GetValueAsync("State", ModeEnum.Delta), Is.EqualTo("Unready"));

        await booster.SaveAsync();

        Assert.That(await booster.GetValueAsync("State"), Is.EqualTo("Unready"));
        Assert.That(await booster.GetValueAsync("State", ModeEnum.Delta), Is.InstanceOf(typeof(Ignorable)));
    }

    [Test]
    public async Task TestValuesInitialized()
    {
        // Initialize single Scalar.Value
        var booster1 = new Booster(new NestedDictionary
            {
                { "Fields", new FlatDictionary { { "State", "Unready" } } }
            }
        );

        var actualState = await booster1.GetValueAsync("State");
        var actualSerialNumber = await booster1.GetValueAsync("SerialNumber");

        Assert.That(actualState, Is.EqualTo("Unready"));
        Assert.That(actualSerialNumber, Is.Null);

        // Initialize more than one Scalar.Value
        var booster2 = new Booster(new NestedDictionary
            {
                { "Fields", new FlatDictionary { { "State", "Unready" }, { "SerialNumber", "B_1" } } }
            }
        );

        actualState = await booster2.GetValueAsync("State");
        actualSerialNumber = await booster2.GetValueAsync("SerialNumber");

        Assert.That(actualSerialNumber, Is.EqualTo("B_1"));
        Assert.That(actualState, Is.EqualTo("Unready"));
    }

    [Test]
    public void TestSetValueOnInvalidCursorState()
    {
        // Values cannot be set on a node with a null cursor.
        var cursorlessBooster = new Booster(new NestedDictionary());
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await cursorlessBooster.SetValueAsync("State", "Ready");
        });

        var booster = new Booster(new NestedDictionary());
        var cursor = new Cursor(Fixtures.GetSession(), CursorStateEnum.Rewind);
        booster.Cursor = cursor;

        // Values can only be set on nodes when the cursor state is Live
        Assert.ThrowsAsync<InvalidOperationException>(async () => { await booster.SetValueAsync("State", "Ready"); });
    }

    [Test]
    public void TestScalarHasNodeReference()
    {
        var booster = new Booster(new NestedDictionary());

        var mi = typeof(Node).GetMethod("_GetGraphObject",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var stateField = (IGraphObject?)mi?.Invoke(booster, new object[] { "State" });

        var nodeReferenceField = stateField?.GetType()
            .GetField("_node", BindingFlags.NonPublic | BindingFlags.Instance);
        var boosterReference = (Booster?)nodeReferenceField?.GetValue(stateField);

        Assert.That(boosterReference, Is.InstanceOf(typeof(Booster)));
    }

    [Test]
    public async Task TestGetEitherValue()
    {
        var booster = Fixtures.GetBooster();
        var cursor = Fixtures.GetCursor();
        booster.Cursor = cursor;

        // Test with only data
        Assert.That(await booster.GetValueAsync("State", ModeEnum.Either), Is.EqualTo("Unready"));

        // Test with delta
        await booster.SetValueAsync("State", "Ready");
        Assert.That(await booster.GetValueAsync("State", ModeEnum.Either), Is.EqualTo("Ready"));

        await booster.DeltaToDataAsync();
        Assert.That(await booster.GetValueAsync("State", ModeEnum.Delta), Is.InstanceOf(typeof(Ignorable)));
        Assert.That(await booster.GetValueAsync("State", ModeEnum.Either), Is.EqualTo("Ready"));
    }

    [Test]
    public async Task TestList()
    {
        var nodeRep = new NodeRep();
        nodeRep.Meta.Tag = "Chain";
        nodeRep.Meta.Guid = Guid.NewGuid();
        nodeRep.Fields.BooleanListFields = new Dictionary<string, List<bool?>?> { { "SomeBooleanList", null } };
        nodeRep.Fields.StringListFields = new Dictionary<string, List<string?>?> { { "SomeStringList", null } };
        nodeRep.Fields.NumericListFields = new Dictionary<string, List<decimal?>?> { { "SomeNumericList", null } };

        var cursor = Fixtures.GetCursor();
        var node = new Node(
            nodeRep.Meta.Guid,
            cursor,
            nodeRep,
            Utils.TimestampMillis()
        );

        var validBooleanLists = new List<List<bool?>?> {
            new List<bool?> { true, false },
            new List<bool?> { false, null, true },
            null
        };

        foreach (var list in validBooleanLists)
        {
            await node.SetValueAsync("SomeBooleanList", list);
            await node.DeltaToDataAsync();

            var val = await node.GetValueAsync("SomeBooleanList");
            if (list == null)
                Assert.That(val, Is.Null);
            else
                CollectionAssert.AreEquivalent((List<bool?>?)val, list);
        }

        var validStringLists = new List<List<string?>?> {
            new List<string?> { "A", "B", "C" },
            new List<string?> { "A", null, "C" },
            null
        };

        foreach (var list in validStringLists)
        {
            await node.SetValueAsync("SomeStringList", list);
            await node.DeltaToDataAsync();

            var val = await node.GetValueAsync("SomeStringList");
            if (list == null)
                Assert.That(val, Is.Null);
            else
                CollectionAssert.AreEquivalent((List<string>?)val, list);
        }

        var validNumericLists = new List<List<decimal?>?> {
            new List<decimal?> { 1, 2, 3 },
            new List<decimal?> { 1, null, 3 },
            new List<decimal?> { 1.1m, 2.2m, 3.3m },
            new List<decimal?> { 1, 2.2m, null },
            null
        };

        foreach (var list in validNumericLists)
        {
            await node.SetValueAsync("SomeNumericList", list);
            await node.DeltaToDataAsync();

            var val = await node.GetValueAsync("SomeNumericList");
            if (list == null)
                Assert.That(val, Is.Null);
            else
                CollectionAssert.AreEquivalent((List<decimal?>?)val, list);
        }
    }
}