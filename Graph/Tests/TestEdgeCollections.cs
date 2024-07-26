using Graph.Core;

namespace Graph.Tests;

[TestFixture]
public class TestEdgeCollections
{
    [SetUp]
    public async Task Init()
    {
        _cursor = Fixtures.GetCursor();
        _rocketship = Fixtures.GetRocketship();

        _rocketship.Cursor = _cursor;
        _boosters = _cursor.Nodes.Select(n => n).Where(n => n.Tag == "Booster").ToList();

        _edgeCollection = (EdgeCollection<Node, Node>?)await _rocketship.GetValueAsync("Boosters");

        _initialData = (await _rocketship.AsDictionaryAsync())!;

        await _cursor.TogetherAsync(ModeEnum.Data, operation: async Task () =>
        {
            var tasks = new List<Task> { _rocketship.SaveAsync() };
            foreach (var booster in _boosters)
                tasks.Add(booster.SaveAsync());

            await Task.WhenAll(tasks);
        });
    }

    private Cursor _cursor;
    private Rocketship _rocketship;
    private List<Node> _boosters;
    private EdgeCollection<Node, Node>? _edgeCollection;
    private NestedDictionary _initialData;

    [Test]
    public async Task TestAttachEdgeCollectionToNode()
    {
        Assert.That(_boosters.Count, Is.EqualTo(_edgeCollection?.Count));

        var nodes = await _edgeCollection!.GetAllNodesAsync();
        foreach (var node in nodes)
        {
            Assert.That(node, Is.InstanceOf(typeof(Booster)));
            Assert.That(_rocketship.Cursor, Is.SameAs(node!.Cursor));
            Assert.That(_cursor.NodeGuids.ContainsKey(node!.Guid));
        }
    }

    [Test]
    public void TestAsDictionary()
    {
        CollectionAssert.AreEquivalent(new FlatDictionary
        {
            {
                "Meta", new FlatDictionary
                {
                    { "Guid", _rocketship.Guid },
                    { "Tag", "Rocketship" },
                    { "Deleted", false }
                }
            },
            {
                "Data", new FlatDictionary
                {
                    {
                        "Fields", new FlatDictionary
                        {
                            { "Name", "Timeless" },
                            { "SerialNumber", "R_1" }
                        }
                    },
                    {
                        "Edges", new FlatDictionary()
                    },
                    {
                        "EdgeCollections", new FlatDictionary
                        {
                            {
                                "Boosters", new Dictionary<Guid, Guid?>
                                {
                                    { _boosters[0].Guid, _boosters[1].Guid },
                                    { _boosters[1].Guid, _boosters[2].Guid },
                                    { _boosters[2].Guid, null }
                                }
                            }
                        }
                    }
                }
            }
        }, _initialData);
    }

    [Test]
    public async Task TestAdd()
    {
        var lastBoosterGuid = _boosters[_boosters.Count - 1].Guid;
        var additionalBoosters = Fixtures.GetBoosters(2);

        _edgeCollection?.Append(additionalBoosters);

        Assert.That(_edgeCollection?.Count, Is.EqualTo(_boosters.Count + additionalBoosters.Count));
        CollectionAssert.AreEquivalent(_initialData, await _rocketship.AsDictionaryAsync(ModeEnum.Data, true));
        CollectionAssert.AreEquivalent(new FlatDictionary
        {
            {
                "Meta", new FlatDictionary
                {
                    { "Guid", _rocketship.Guid },
                    { "Tag", "Rocketship" },
                    { "Deleted", false }
                }
            },
            {
                "Delta", new FlatDictionary
                {
                    {
                        "Fields", new FlatDictionary()
                    },
                    {
                        "Edges", new FlatDictionary()
                    },
                    {
                        "EdgeCollections", new FlatDictionary
                        {
                            {
                                "Boosters", new FlatDictionary
                                {
                                    {
                                        "Added", new Dictionary<Guid, Guid?>
                                        {
                                            { lastBoosterGuid, additionalBoosters[0].Guid },
                                            { additionalBoosters[0].Guid, additionalBoosters[1].Guid },
                                            { additionalBoosters[1].Guid, null }
                                        }
                                    },
                                    {
                                        "Removed", new List<Guid>
                                        {
                                            lastBoosterGuid
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }, await _rocketship.AsDictionaryAsync(ModeEnum.Delta));
    }

    [Test]
    public void TestAddInvalidNodeType()
    {
        // Should not be able to add a different node type to the edge collection.
        var fuelTank = Fixtures.GetFuelTank("F_10");
        Assert.Throws<ArgumentException>(() => { _edgeCollection?.Append(fuelTank); });
    }

    [Test]
    public async Task TestAddAtIndex()
    {
        var additionalBoosters = Fixtures.GetBoosters(2, _boosters.Count + 1);

        // Add single
        _edgeCollection!.AddAtIndex(additionalBoosters[0], 2);

        Assert.That(_edgeCollection.Count, Is.EqualTo(_boosters.Count + 1));
        CollectionAssert.AreEquivalent(new FlatDictionary
        {
            {
                "Added", new Dictionary<Guid, Guid?>
                {
                    { _boosters[1].Guid, additionalBoosters[0].Guid },
                    { additionalBoosters[0].Guid, _boosters[2].Guid }
                }
            },
            {
                "Removed", new List<Guid>
                {
                    _boosters[1].Guid
                }
            }
        }, (FlatDictionary)_edgeCollection.AsDictionary(ModeEnum.Delta));

        // reset
        await _edgeCollection.ClearDeltaAsync();
        Assert.That(_edgeCollection.Count, Is.EqualTo(_boosters.Count));

        // Add multiple
        _edgeCollection!.AddAtIndex(additionalBoosters, 2);

        Assert.That(_edgeCollection.Count, Is.EqualTo(_boosters.Count + additionalBoosters.Count));
        CollectionAssert.AreEquivalent(new FlatDictionary
        {
            {
                "Added", new Dictionary<Guid, Guid?>
                {
                    { _boosters[1].Guid, additionalBoosters[0].Guid },
                    { additionalBoosters[0].Guid, additionalBoosters[1].Guid },
                    { additionalBoosters[1].Guid, _boosters[2].Guid }
                }
            },
            {
                "Removed", new List<Guid>
                {
                    _boosters[1].Guid
                }
            }
        }, (FlatDictionary)_edgeCollection.AsDictionary(ModeEnum.Delta));

        // Invalid indices should throw exceptions
        Assert.Throws<IndexOutOfRangeException>(() => { _edgeCollection.AddAtIndex(Fixtures.GetBooster(), -1); });
        Assert.Throws<IndexOutOfRangeException>(() =>
        {
            _edgeCollection.AddAtIndex(Fixtures.GetBooster(), _edgeCollection.Count + 1);
        });
    }

    [Test]
    public async Task TestPrepend()
    {
        var additionalBoosters = Fixtures.GetBoosters(2, _boosters.Count + 1);

        // Prepend single
        _edgeCollection!.Prepend(additionalBoosters[0]);

        Assert.That(_edgeCollection.Count, Is.EqualTo(_boosters.Count + 1));
        CollectionAssert.AreEquivalent(new FlatDictionary
        {
            {
                "Added", new Dictionary<Guid, Guid?>
                {
                    { additionalBoosters[0].Guid, _boosters[0].Guid }
                }
            },
            {
                "Removed", new List<Guid>()
            }
        }, (FlatDictionary)_edgeCollection.AsDictionary(ModeEnum.Delta));

        // reset
        await _edgeCollection.ClearDeltaAsync();
        Assert.That(_edgeCollection.Count, Is.EqualTo(_boosters.Count));

        // Prepend multiple
        _edgeCollection.Prepend(additionalBoosters);

        Assert.That(_edgeCollection.Count, Is.EqualTo(_boosters.Count + additionalBoosters.Count));
        CollectionAssert.AreEquivalent(new FlatDictionary
        {
            {
                "Added", new Dictionary<Guid, Guid?>
                {
                    { additionalBoosters[0].Guid, additionalBoosters[1].Guid },
                    { additionalBoosters[1].Guid, _boosters[0].Guid }
                }
            },
            {
                "Removed", new List<Guid>()
            }
        }, (FlatDictionary)_edgeCollection.AsDictionary(ModeEnum.Delta));
    }

    [Test]
    public void TestModifyDelta()
    {
        var appendedBooster = Fixtures.GetBooster("B_4");
        _edgeCollection!.Append(appendedBooster);

        var prependedBoosters = Fixtures.GetBoosters(2, 5);
        _edgeCollection.Prepend(prependedBoosters);

        var insertedBoosters = Fixtures.GetBoosters(3, 7);
        _edgeCollection.AddAtIndex(insertedBoosters, 1);

        CollectionAssert.AreEquivalent(new FlatDictionary
        {
            {
                "Added", new Dictionary<Guid, Guid?>
                {
                    { prependedBoosters[0].Guid, insertedBoosters[0].Guid },
                    { insertedBoosters[0].Guid, insertedBoosters[1].Guid },
                    { insertedBoosters[1].Guid, insertedBoosters[2].Guid },
                    { insertedBoosters[2].Guid, prependedBoosters[1].Guid },
                    { prependedBoosters[1].Guid, _boosters[0].Guid },
                    { _boosters[2].Guid, appendedBooster.Guid },
                    { appendedBooster.Guid, null }
                }
            },
            {
                "Removed", new List<Guid>
                {
                    _boosters[2].Guid
                }
            }
        }, (FlatDictionary)_edgeCollection.AsDictionary(ModeEnum.Delta));
    }

    [Test]
    public async Task TestRemove()
    {
        // Remove single
        _edgeCollection?.Remove(_boosters[0].Guid);

        Assert.That(_edgeCollection?.Count, Is.EqualTo(_boosters.Count - 1));
        CollectionAssert.AreEquivalent(_initialData, await _rocketship.AsDictionaryAsync(ModeEnum.Data, true));
        CollectionAssert.AreEquivalent(new FlatDictionary
        {
            {
                "Meta", new FlatDictionary
                {
                    { "Guid", _rocketship.Guid },
                    { "Tag", "Rocketship" },
                    { "Deleted", false }
                }
            },
            {
                "Delta", new FlatDictionary
                {
                    {
                        "Fields", new FlatDictionary()
                    },
                    {
                        "Edges", new FlatDictionary()
                    },
                    {
                        "EdgeCollections", new FlatDictionary
                        {
                            {
                                "Boosters", new FlatDictionary
                                {
                                    {
                                        "Added", new Dictionary<Guid, Guid?>()
                                    },
                                    {
                                        "Removed", new List<Guid>
                                        {
                                            _boosters[0].Guid
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }, await _rocketship.AsDictionaryAsync(ModeEnum.Delta));

        _edgeCollection?.ClearDeltaAsync();

        // Remove multiple
        var guidsToRemove = new List<Guid>
        {
            _boosters[1].Guid,
            _boosters[2].Guid
        };
        _edgeCollection?.Remove(guidsToRemove);

        Assert.That(_edgeCollection?.Count, Is.EqualTo(_boosters.Count - guidsToRemove.Count));
        CollectionAssert.AreEquivalent(_initialData, await _rocketship.AsDictionaryAsync(ModeEnum.Data, true));
        CollectionAssert.AreEquivalent(new FlatDictionary
        {
            {
                "Meta", new FlatDictionary
                {
                    { "Guid", _rocketship.Guid },
                    { "Tag", "Rocketship" },
                    { "Deleted", false }
                }
            },
            {
                "Delta", new FlatDictionary
                {
                    {
                        "Fields", new FlatDictionary()
                    },
                    {
                        "Edges", new FlatDictionary()
                    },
                    {
                        "EdgeCollections", new FlatDictionary
                        {
                            {
                                "Boosters", new FlatDictionary
                                {
                                    {
                                        "Added", new Dictionary<Guid, Guid?>()
                                    },
                                    {
                                        "Removed", guidsToRemove
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }, await _rocketship.AsDictionaryAsync(ModeEnum.Delta));
    }

    [Test]
    public async Task TestRemoveJustAdded()
    {
        var lastBoosterGuid = _boosters[_boosters.Count - 1].Guid;
        var additionalBoosters = Fixtures.GetBoosters(2);

        _edgeCollection?.Append(additionalBoosters);
        _edgeCollection?.Remove(additionalBoosters[0].Guid);

        Assert.That(_edgeCollection?.Count, Is.EqualTo(_boosters.Count + additionalBoosters.Count - 1));
        CollectionAssert.AreEquivalent(new FlatDictionary
        {
            {
                "Meta", new FlatDictionary
                {
                    { "Guid", _rocketship.Guid },
                    { "Tag", "Rocketship" },
                    { "Deleted", false }
                }
            },
            {
                "Delta", new FlatDictionary
                {
                    {
                        "Fields", new FlatDictionary()
                    },
                    {
                        "Edges", new FlatDictionary()
                    },
                    {
                        "EdgeCollections", new FlatDictionary
                        {
                            {
                                "Boosters", new FlatDictionary
                                {
                                    {
                                        "Added", new Dictionary<Guid, Guid?>
                                        {
                                            { lastBoosterGuid, additionalBoosters[1].Guid },
                                            { additionalBoosters[1].Guid, null }
                                        }
                                    },
                                    {
                                        "Removed", new List<Guid>
                                        {
                                            lastBoosterGuid
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }, await _rocketship.AsDictionaryAsync(ModeEnum.Delta));

        _edgeCollection?.ClearDeltaAsync();

        // Test removing 2nd added
        _edgeCollection?.Append(additionalBoosters);
        _edgeCollection?.Remove(additionalBoosters[1].Guid);

        Assert.That(_edgeCollection?.Count, Is.EqualTo(_boosters.Count + additionalBoosters.Count - 1));
        CollectionAssert.AreEquivalent(new FlatDictionary
        {
            {
                "Meta", new FlatDictionary
                {
                    { "Guid", _rocketship.Guid },
                    { "Tag", "Rocketship" },
                    { "Deleted", false }
                }
            },
            {
                "Delta", new FlatDictionary
                {
                    {
                        "Fields", new FlatDictionary()
                    },
                    {
                        "Edges", new FlatDictionary()
                    },
                    {
                        "EdgeCollections", new FlatDictionary
                        {
                            {
                                "Boosters", new FlatDictionary
                                {
                                    {
                                        "Added", new Dictionary<Guid, Guid?>
                                        {
                                            { lastBoosterGuid, additionalBoosters[0].Guid },
                                            { additionalBoosters[0].Guid, null }
                                        }
                                    },
                                    {
                                        "Removed", new List<Guid>
                                        {
                                            lastBoosterGuid
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }, await _rocketship.AsDictionaryAsync(ModeEnum.Delta));

        _edgeCollection?.ClearDeltaAsync();

        // Test add single, then remove
        var singleBooster = Fixtures.GetBooster();
        _edgeCollection?.Append(singleBooster);
        _edgeCollection?.Remove(singleBooster.Guid);

        Assert.That(_edgeCollection?.Count, Is.EqualTo(_boosters.Count));
        Assert.That(await _rocketship.AsDictionaryAsync(ModeEnum.Delta), Is.Null);

        // _edgeCollection._hasDelta should be false, so the following should not throw an exception.
        Assert.That(await _rocketship.AsDictionaryAsync(), Is.InstanceOf(typeof(NestedDictionary)));
    }

    [Test]
    public async Task TestDeltaToData()
    {
        // Test after adding
        var additionalBoosters = Fixtures.GetBoosters(2);
        _edgeCollection!.Append(additionalBoosters);

        await _edgeCollection.DeltaToDataAsync();

        Assert.That(_edgeCollection.Count, Is.EqualTo(_boosters.Count + additionalBoosters.Count));
        Assert.That(await _rocketship.AsDictionaryAsync(ModeEnum.Delta), Is.Null);
        CollectionAssert.AreEquivalent(new FlatDictionary
        {
            {
                "Meta", new FlatDictionary
                {
                    { "Guid", _rocketship.Guid },
                    { "Tag", "Rocketship" },
                    { "Deleted", false }
                }
            },
            {
                "Data", new FlatDictionary
                {
                    {
                        "Fields", new FlatDictionary
                        {
                            { "Name", "Timeless" },
                            { "SerialNumber", "R_1" }
                        }
                    },
                    {
                        "Edges", new FlatDictionary()
                    },
                    {
                        "EdgeCollections", new FlatDictionary
                        {
                            {
                                "Boosters", new Dictionary<Guid, Guid?>
                                {
                                    { _boosters[0].Guid, _boosters[1].Guid },
                                    { _boosters[1].Guid, _boosters[2].Guid },
                                    { _boosters[2].Guid, additionalBoosters[0].Guid },
                                    { additionalBoosters[0].Guid, additionalBoosters[1].Guid },
                                    { additionalBoosters[1].Guid, null }
                                }
                            }
                        }
                    }
                }
            }
        }, await _rocketship.AsDictionaryAsync());

        // Test after removing
        _edgeCollection.Remove(additionalBoosters.Select(n => n.Guid).ToList());

        CollectionAssert.IsSupersetOf(
            (await _edgeCollection.GetAllNodesAsync()).Select(n => n.Guid).ToList(),
            additionalBoosters.Select(n => n.Guid).ToList()
        );

        await _edgeCollection.DeltaToDataAsync();

        Assert.That(_edgeCollection.Count, Is.EqualTo(_boosters.Count));
        Assert.That(await _rocketship.AsDictionaryAsync(ModeEnum.Delta), Is.Null);
        CollectionAssert.AreEquivalent(_initialData, await _rocketship.AsDictionaryAsync());

        CollectionAssert.IsNotSupersetOf(
            (await _edgeCollection.GetAllNodesAsync()).Select(n => n.Guid).ToList(),
            additionalBoosters.Select(n => n.Guid).ToList()
        );

        // Test after adding and removing
        _edgeCollection.Append(additionalBoosters[0]);
        _edgeCollection.Remove(_boosters[1].Guid);

        await _edgeCollection.DeltaToDataAsync();

        Assert.That(_edgeCollection.Count, Is.EqualTo(_boosters.Count));
        Assert.That(await _rocketship.AsDictionaryAsync(ModeEnum.Delta), Is.Null);
        CollectionAssert.AreEquivalent(new FlatDictionary
        {
            {
                "Meta", new FlatDictionary
                {
                    { "Guid", _rocketship.Guid },
                    { "Tag", "Rocketship" },
                    { "Deleted", false }
                }
            },
            {
                "Data", new FlatDictionary
                {
                    {
                        "Fields", new FlatDictionary
                        {
                            { "Name", "Timeless" },
                            { "SerialNumber", "R_1" }
                        }
                    },
                    {
                        "Edges", new FlatDictionary()
                    },
                    {
                        "EdgeCollections", new FlatDictionary
                        {
                            {
                                "Boosters", new Dictionary<Guid, Guid?>
                                {
                                    { _boosters[0].Guid, _boosters[2].Guid },
                                    { _boosters[2].Guid, additionalBoosters[0].Guid },
                                    { additionalBoosters[0].Guid, null }
                                }
                            }
                        }
                    }
                }
            }
        }, await _rocketship.AsDictionaryAsync());

        // Test after removing all and adding multiple
        _edgeCollection.Remove(new List<Guid>
        {
            _boosters[0].Guid,
            _boosters[2].Guid,
            additionalBoosters[0].Guid
        });
        _edgeCollection.Append(new List<Node> { _boosters[1], additionalBoosters[1] });

        await _edgeCollection.DeltaToDataAsync();

        Assert.That(_edgeCollection.Count, Is.EqualTo(2));
        Assert.That(await _rocketship.AsDictionaryAsync(ModeEnum.Delta), Is.Null);
        CollectionAssert.AreEquivalent(new FlatDictionary
        {
            {
                "Meta", new FlatDictionary
                {
                    { "Guid", _rocketship.Guid },
                    { "Tag", "Rocketship" },
                    { "Deleted", false }
                }
            },
            {
                "Data", new FlatDictionary
                {
                    {
                        "Fields", new FlatDictionary
                        {
                            { "Name", "Timeless" },
                            { "SerialNumber", "R_1" }
                        }
                    },
                    {
                        "Edges", new FlatDictionary()
                    },
                    {
                        "EdgeCollections", new FlatDictionary
                        {
                            {
                                "Boosters", new Dictionary<Guid, Guid?>
                                {
                                    { _boosters[1].Guid, additionalBoosters[1].Guid },
                                    { additionalBoosters[1].Guid, null }
                                }
                            }
                        }
                    }
                }
            }
        }, await _rocketship.AsDictionaryAsync());
    }

    [Test]
    public async Task TestGetNodesByMode()
    {
        var deltaNodes = await _edgeCollection!.GetAllNodesAsync(ModeEnum.Delta);

        // No delta yet
        Assert.That(deltaNodes, Is.Empty);

        var dataAssertions = async Task (bool silent) =>
        {
            var initialNodes = await _edgeCollection!.GetAllNodesAsync(ModeEnum.Data, silent);

            Assert.That(initialNodes.Count, Is.EqualTo(_boosters.Count));
            for (var i = 0; i < initialNodes.Count; i++)
                Assert.That(initialNodes[i].Guid, Is.EqualTo(_boosters[i].Guid));
        };

        await dataAssertions(false);

        var additionalNodes = Fixtures.GetBoosters(3, _boosters.Count + 1);
        _edgeCollection?.Append(additionalNodes);

        deltaNodes = await _edgeCollection!.GetAllNodesAsync(ModeEnum.Delta);

        // Assertions for delta
        Assert.That(deltaNodes.Count, Is.EqualTo(additionalNodes.Count));
        for (var i = 0; i < additionalNodes.Count; i++)
            Assert.That(deltaNodes[i].Guid, Is.EqualTo(additionalNodes[i].Guid));

        await dataAssertions(true);
    }

    [Test]
    public async Task TestLazyLoad()
    {
        // use cursor that has not yet loaded the nodes in the edge collection.
        var cursor = await Fixtures.GetInMemoryTestCursorAsync();
        var expectedNodeCount = 1;

        Assert.That(cursor.Nodes.Count, Is.EqualTo(expectedNodeCount));

        var rocketship = cursor.Nodes[0];
        var edgeCollection = (EdgeCollection<Node, Node>?)await rocketship.GetValueAsync("Boosters");

        Assert.That(cursor.Nodes.Count,
            Is.EqualTo(expectedNodeCount)); // none of the edges in the edge collection should be loaded yet
        Assert.That(edgeCollection, Is.InstanceOf(typeof(EdgeCollection<Node, Node>)));
        Assert.That(cursor.Nodes.Count, Is.EqualTo(1)); // just the rocketship

        // Test by index
        var node = await edgeCollection!.GetNodeAtIndexAsync(1);
        expectedNodeCount++;

        Assert.That(cursor.Nodes.Count,
            Is.EqualTo(expectedNodeCount)); // the rocketship and the single requested booster
        Assert.That(node, Is.InstanceOf(typeof(Booster)));
        Assert.That(node!.IsLoaded, Is.True);
        Assert.That(await node!.GetValueAsync("SerialNumber"), Is.EqualTo("B_2"));

        // Test range
        var requestedRangeSize = 2;
        var subset = await edgeCollection.GetRange(2, requestedRangeSize);
        expectedNodeCount += requestedRangeSize;

        Assert.That(cursor.Nodes.Count,
            Is.EqualTo(expectedNodeCount)); // the rocketship and the 3 boosters that have been requested
        var expectedSerialNumbers = new List<string> { "B_4", "B_3" };
        for (var i = 0; i < subset!.Count; i++)
        {
            node = subset[i];
            Assert.That(node, Is.InstanceOf(typeof(Booster)));
            Assert.That(node!.IsLoaded, Is.True);
            Assert.That(await node.GetValueAsync("SerialNumber"), Is.EqualTo(expectedSerialNumbers[i]));

            var fuelTank = (Node?)await node.GetValueAsync("FuelTank");
            if (fuelTank != null)
                Assert.That(fuelTank?.IsLoaded, Is.False);
        }

        Assert.That(cursor.Nodes.Count, Is.EqualTo(expectedNodeCount)); // should include a fuel tank now
    }

    [Test]
    public void TestGetNext()
    {
        var next = _edgeCollection!.GetNext(_boosters[0].Guid);
        Assert.That(next, Is.EqualTo(_boosters[1].Guid));

        // after last
        next = _edgeCollection!.GetNext(_boosters[2].Guid);
        Assert.That(next, Is.Null);

        // Guid does not exist in list
        Assert.Throws<ArgumentException>(() => { _edgeCollection!.GetNext(Guid.NewGuid()); });
    }

    [Test]
    public void TestGetPrevious()
    {
        var previous = _edgeCollection!.GetPrevious(_boosters[2].Guid);
        Assert.That(previous, Is.EqualTo(_boosters[1].Guid));

        // before first
        previous = _edgeCollection!.GetPrevious(_boosters[0].Guid);
        Assert.That(previous, Is.Null);

        // Guid does not exist in list
        Assert.Throws<ArgumentException>(() => { _edgeCollection!.GetPrevious(Guid.NewGuid()); });
    }

    [Test]
    public void TestGetGuidByIndex()
    {
        for (var i = 0; i < _boosters.Count; i++)
            Assert.That(_edgeCollection!.GetGuidAtIndex(i), Is.EqualTo(_boosters[i].Guid));

        Assert.That(_edgeCollection!.GetGuidAtIndex(_boosters.Count + 1), Is.Null);

        Assert.Throws<ArgumentException>(() => { _edgeCollection!.GetGuidAtIndex(-1); });
    }

    [Test]
    public async Task TestGetRange()
    {
        _edgeCollection!.Append(Fixtures.GetBoosters(7, _boosters.Count + 1));
        await _edgeCollection.DeltaToDataAsync();
        var allNodes = await _edgeCollection.GetAllNodesAsync();

        // full range
        CollectionAssert.AreEqual(allNodes.Select(n => n.Guid).ToList(),
            _edgeCollection.GetGuidsForRange(0, allNodes.Count));

        // partial range at start
        CollectionAssert.AreEqual(allNodes.Select(n => n.Guid).ToList().GetRange(0, 4),
            _edgeCollection.GetGuidsForRange(0, 4));

        // partial range in middle
        var actualMiddle = allNodes.Select(n => n.Guid).ToList().GetRange(5, 2);
        var middle = _edgeCollection.GetGuidsForRange(5, 2);
        CollectionAssert.AreEqual(actualMiddle, middle);

        // partial range at end
        CollectionAssert.AreEqual(allNodes.Select(n => n.Guid).ToList().GetRange(6, 4),
            _edgeCollection.GetGuidsForRange(6, 4));

        // partial count at end
        CollectionAssert.AreEqual(allNodes.Select(n => n.Guid).ToList().GetRange(8, 2),
            _edgeCollection.GetGuidsForRange(8, 25));

        // invalid startIndex
        Assert.Throws<ArgumentException>(() => { _edgeCollection.GetGuidsForRange(-1, 1); });
    }

    [Test]
    public async Task TestGetRangeAfterGuid()
    {
        _edgeCollection!.Append(Fixtures.GetBoosters(7, _boosters.Count + 1));
        await _edgeCollection.DeltaToDataAsync();
        var allNodes = await _edgeCollection.GetAllNodesAsync();

        // full range
        CollectionAssert.AreEqual(allNodes.Select(n => n.Guid).ToList(),
            (await _edgeCollection.GetRange(null, allNodes.Count)).Select(n => n.Guid).ToList());

        // partial range at start
        CollectionAssert.AreEqual(allNodes.Select(n => n.Guid).ToList().GetRange(0, 4),
            (await _edgeCollection.GetRange(null, 4)).Select(n => n.Guid).ToList());

        // partial range in middle
        CollectionAssert.AreEqual(allNodes.Select(n => n.Guid).ToList().GetRange(5, 2),
            (await _edgeCollection.GetRange(allNodes[4].Guid, 2)).Select(n => n.Guid).ToList());

        // partial range at end
        CollectionAssert.AreEqual(allNodes.Select(n => n.Guid).ToList().GetRange(6, 4),
            (await _edgeCollection.GetRange(allNodes[5].Guid, 4)).Select(n => n.Guid).ToList());

        // partial count at end
        CollectionAssert.AreEqual(allNodes.Select(n => n.Guid).ToList().GetRange(8, 2),
            (await _edgeCollection.GetRange(allNodes[7].Guid, 25)).Select(n => n.Guid).ToList());

        // invalid argument
        Assert.ThrowsAsync<ArgumentException>(async () => { await _edgeCollection.GetRange(Guid.NewGuid(), 1); });
    }

    [Test]
    public async Task TestChangeNodeIndex()
    {
        // Test reorder
        _edgeCollection!.MoveAfter(_boosters[0], _boosters[1]);

        CollectionAssert.AreEquivalent(new FlatDictionary
        {
            {
                "Added", new Dictionary<Guid, Guid?>
                {
                    { _boosters[1].Guid, _boosters[0].Guid },
                    { _boosters[0].Guid, _boosters[2].Guid }
                }
            },
            {
                "Removed", new List<Guid>
                {
                    _boosters[1].Guid,
                    _boosters[0].Guid
                }
            }
        }, (FlatDictionary)_edgeCollection.AsDictionary(ModeEnum.Delta));

        await _edgeCollection.ClearDeltaAsync();

        // Test after last
        _edgeCollection!.MoveAfter(_boosters[0], _boosters[2]);

        CollectionAssert.AreEquivalent(new FlatDictionary
        {
            {
                "Added", new Dictionary<Guid, Guid?>
                {
                    { _boosters[2].Guid, _boosters[0].Guid },
                    { _boosters[0].Guid, null }
                }
            },
            {
                "Removed", new List<Guid>
                {
                    _boosters[2].Guid,
                    _boosters[0].Guid
                }
            }
        }, (FlatDictionary)_edgeCollection.AsDictionary(ModeEnum.Delta));

        await _edgeCollection.ClearDeltaAsync();

        // Edge case
        _edgeCollection.MoveToFirst(_boosters[1]);

        CollectionAssert.AreEquivalent(new FlatDictionary
        {
            {
                "Added", new Dictionary<Guid, Guid?>
                {
                    { _boosters[1].Guid, _boosters[0].Guid },
                    { _boosters[0].Guid, _boosters[2].Guid }
                }
            },
            {
                "Removed", new List<Guid>
                {
                    _boosters[1].Guid,
                    _boosters[0].Guid
                }
            }
        }, (FlatDictionary)_edgeCollection.AsDictionary(ModeEnum.Delta));
    }

    [Test]
    public async Task TestRetrieveDeltaFromDataStore()
    {
        var expectedDataA = await _rocketship.AsDictionaryAsync();
        Assert.That(expectedDataA, Is.Not.Null);
        ((FlatDictionary)expectedDataA!["Data"]["EdgeCollections"]!)["Boosters"] = new Dictionary<Guid, Guid?>
        {
            { _boosters[1].Guid, _boosters[2].Guid },
            { _boosters[2].Guid, null }
        };

        var expectedDataB = await _rocketship.AsDictionaryAsync();
        Assert.That(expectedDataB, Is.Not.Null);
        ((FlatDictionary)expectedDataB!["Data"]["EdgeCollections"]!)["Boosters"] = new Dictionary<Guid, Guid?>
        {
            { _boosters[1].Guid, _boosters[2].Guid },
            { _boosters[2].Guid, _boosters[0].Guid },
            { _boosters[0].Guid, null }
        };

        // Remove single
        _edgeCollection?.Remove(_boosters[0].Guid);
        await _rocketship.SaveAsync();
        await _RefreshEdgeCollection();

        Assert.That(_edgeCollection?.Count, Is.EqualTo(_boosters.Count - 1));
        CollectionAssert.AreEquivalent(expectedDataA, await _rocketship.AsDictionaryAsync());

        // Append single
        _edgeCollection?.Append(_boosters[0]);
        await _rocketship.SaveAsync();
        await _RefreshEdgeCollection();

        Assert.That(_edgeCollection?.Count, Is.EqualTo(_boosters.Count));
        CollectionAssert.AreEquivalent(expectedDataB, await _rocketship.AsDictionaryAsync());
    }

    [Test]
    public async Task TestContains()
    {
        // data contains
        Assert.That(_edgeCollection!.Contains(_boosters[0].Guid), Is.True);
        Assert.That(_edgeCollection.Contains(Guid.NewGuid()), Is.False);

        // _deltaRemoved contains
        _edgeCollection.Remove(_boosters[0].Guid);
        Assert.That(_edgeCollection.Contains(_boosters[0].Guid), Is.False);

        // _deltaAdded contains
        var newBooster = Fixtures.GetBooster("B_10");
        _edgeCollection.Append(newBooster);
        Assert.That(_edgeCollection.Contains(newBooster.Guid), Is.True);

        // after delta to data, data contains
        await _edgeCollection.DeltaToDataAsync();
        Assert.That(_edgeCollection.Contains(newBooster.Guid), Is.True);
        Assert.That(_edgeCollection.Contains(_boosters[0].Guid), Is.False);
    }

    [Test]
    public async Task TestClear()
    {
        // Make sure there are some nodes in the edgecollection
        CollectionAssert.AreEquivalent(_initialData, await _rocketship.AsDictionaryAsync(ModeEnum.Data, true));

        // Make some changes to populate the delta lists
        var additionalBoosters = Fixtures.GetBoosters(2);
        _edgeCollection?.Append(additionalBoosters);
        _edgeCollection!.Remove(_boosters[0].Guid);

        // Do the deed
        _edgeCollection.Clear();
        await _edgeCollection.DeltaToDataAsync();

        // Check the results
        Assert.That(_edgeCollection.Count, Is.EqualTo(0));
        Assert.That(_edgeCollection.AsDictionary(ModeEnum.Data), Is.Empty);
        Assert.That(_edgeCollection.AsDictionary(ModeEnum.Delta), Is.InstanceOf(typeof(Ignorable)));
    }

    private async Task _RefreshEdgeCollection()
    {
        await _cursor.SearchAsync();

        var rocketship = _cursor.Nodes.Select(n => n).Where(n => n.Tag == "Rocketship").ToList().First();

        if (rocketship == null) throw new Exception("Failed to refresh rocketship.");

        _rocketship = (Rocketship)rocketship;
        _edgeCollection = (EdgeCollection<Node, Node>?)await _rocketship.GetValueAsync("Boosters");
    }
}