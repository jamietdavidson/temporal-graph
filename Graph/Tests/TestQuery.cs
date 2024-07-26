using Graph.Core;

namespace Graph.Tests;

public class TestQuery
{
    [Test]
    [TestCase(0, 0, 0)]
    [TestCase(0, 1, 1)]
    [TestCase(1, 0, 1)]
    [TestCase(4, 5, 9)]
    public void TestInclude(int countA, int countB, int expectedCount)
    {
        var query = new Query();
        query.Include(Fixtures.GetBoosters(countA));
        query.Include(Fixtures.GetBoosters(countB));

        Assert.That(query.IncludeGuids.Count(), Is.EqualTo(expectedCount));
    }

    [Test]
    public void TestIncludeDuplicates()
    {
        var query = new Query();
        var boosterCount = 2;
        var boosters = Fixtures.GetBoosters(boosterCount);

        // Include some boosters
        query.Include(boosters);
        Assert.That(query.IncludeGuids.Count(), Is.EqualTo(boosterCount));

        // Attempt to include duplicates
        query.Include(boosters);
        Assert.That(query.IncludeGuids.Count(), Is.EqualTo(boosterCount));
    }
}