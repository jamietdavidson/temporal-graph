using Graph.Core;

namespace Graph.Tests;

public class TestUtils
{
    [Test]
    public void TestEnumAsList()
    {
        var actual = Utils.EnumAsList(typeof(ModeEnum));
        var expected = new List<string> { "Data", "Delta", "Either" };

        CollectionAssert.AreEqual(expected, actual);
    }

    [Test]
    public void TestTimestampMillis()
    {
        var now = Utils.TimestampMillis();
        var inception = Utils.TimestampMillis(new DateTime(2020, 1, 1));
        Assert.That(now, Is.GreaterThan(inception));
    }

    [Test]
    public void TestAsLinkedList()
    {
        var guids = new List<Guid>
        {
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid()
        };

        var expectedLinkedList = new LinkedList<Guid>(guids);

        var linkedDict = new Dictionary<Guid, Guid?>
        {
            { guids[0], guids[1] },
            { guids[1], guids[2] },
            { guids[2], null }
        };

        // Convert Dictionary to LinkedList
        var actualLinkedList = Utils.AsLinkedList(linkedDict);

        Assert.That(actualLinkedList.Count, Is.EqualTo(guids.Count));
        CollectionAssert.AreEquivalent(expectedLinkedList, actualLinkedList);

        // Already a LinkedList
        CollectionAssert.AreEquivalent(actualLinkedList,
            Utils.AsLinkedList(actualLinkedList));
    }
}