using Graph.Core;

namespace Graph.Tests;

public class TestCursors
{
    [Test]
    public async Task TestReplay()
    {
        var cursor = await Fixtures.GetCursorWithHistoryAsync();
        var rocketship = cursor.Nodes.Select(n => n).Where(n => n.Tag == "Rocketship").ToList()[0];
        var rocketshipGuid = rocketship.Guid;
        var numBoosters = cursor.Nodes.Select(n => n).Where(n => n.Tag == "Booster").ToList().Count;
        var numFuelTanks = cursor.Nodes.Select(n => n).Where(n => n.Tag == "FuelTank").ToList().Count;

        Assert.That(numBoosters, Is.EqualTo(6));
        Assert.That(numFuelTanks, Is.EqualTo(3));

        rocketship = await cursor.InceptionAsync(rocketshipGuid);

        // Assertions for cursor at inception
        Assert.That(rocketship, Is.InstanceOf(typeof(Rocketship)));
        Assert.That(cursor.State, Is.EqualTo(CursorStateEnum.Rewind));
        Assert.That(cursor.Nodes.Count, Is.EqualTo(4));

        var boosters = (EdgeCollection<Node, Node>?)await rocketship!.GetValueAsync("Boosters");
        Assert.That(boosters, Is.InstanceOf(typeof(EdgeCollection<Node, Node>)));
        Assert.That(boosters!.Count, Is.EqualTo(3));

        foreach (var booster in await boosters.GetAllNodesAsync())
        {
            Assert.IsInstanceOf(typeof(Booster), booster);

            CollectionAssert.AreEquivalent(new NestedDictionary
            {
                {
                    "Meta", new FlatDictionary
                    {
                        { "Guid", booster.Guid },
                        { "Tag", "Booster" },
                        { "Deleted", false }
                    }
                },
                {
                    "Data", new FlatDictionary
                    {
                        {
                            "Fields", new FlatDictionary
                            {
                                { "SerialNumber", booster.GetValueAsync("SerialNumber").Result },
                                { "State", "Unready" }
                            }
                        },
                        {
                            "Edges", new FlatDictionary
                            {
                                { "FuelTank", null }
                            }
                        },
                        {
                            "EdgeCollections", new FlatDictionary()
                        }
                    }
                }
            }, await booster.AsDictionaryAsync());
        }

        await cursor.NextAsync();
        rocketship = cursor.NodeGuids[rocketshipGuid];

        // Assertions for cursor after adding extra boosters
        Func<Task> stepTwoAssertions = async () =>
        {
            Assert.That(cursor.State, Is.EqualTo(CursorStateEnum.Rewind));
            Assert.That(cursor.Nodes.Count, Is.EqualTo(numBoosters + numFuelTanks + 1));

            boosters = (EdgeCollection<Node, Node>?)await rocketship!.GetValueAsync("Boosters");
            Assert.That(boosters?.Count, Is.EqualTo(6));

            FlatDictionary? expectedFuelTank;
            var actualFuelTankCount = 0;
            foreach (var booster in await boosters!.GetAllNodesAsync())
            {
                Assert.IsInstanceOf(typeof(Booster), booster);

                var fuelTank = (Node?)await booster.GetValueAsync("FuelTank");

                if (fuelTank is FuelTank)
                {
                    fuelTank = await fuelTank.LoadAsync();
                    Assert.That(await fuelTank!.GetValueAsync("FuelRemaining"), Is.EqualTo(100));
                    Assert.That(await fuelTank.GetValueAsync("SerialNumber"), Is.Not.Null);

                    expectedFuelTank = new FlatDictionary
                    {
                        {
                            "Meta", new FlatDictionary
                            {
                                { "Guid", fuelTank.Guid },
                                { "Tag", "FuelTank" },
                                { "Deleted", false }
                            }
                        }
                    };
                    actualFuelTankCount++;
                }
                else
                {
                    expectedFuelTank = null;
                }

                CollectionAssert.AreEquivalent(new NestedDictionary
                {
                    {
                        "Meta", new FlatDictionary
                        {
                            { "Guid", booster.Guid },
                            { "Tag", "Booster" },
                            { "Deleted", false }
                        }
                    },
                    {
                        "Data", new FlatDictionary
                        {
                            {
                                "Fields", new FlatDictionary
                                {
                                    { "SerialNumber", booster.GetValueAsync("SerialNumber").Result },
                                    { "State", "Unready" }
                                }
                            },
                            {
                                "Edges", new FlatDictionary
                                {
                                    { "FuelTank", expectedFuelTank }
                                }
                            },
                            {
                                "EdgeCollections", new FlatDictionary()
                            }
                        }
                    }
                }, await booster.AsDictionaryAsync());
            }

            Assert.That(actualFuelTankCount, Is.EqualTo(numFuelTanks));
        };
        await stepTwoAssertions();

        await cursor.NextAsync();
        rocketship = cursor.NodeGuids[rocketshipGuid];
        boosters = (EdgeCollection<Node, Node>?)await rocketship!.GetValueAsync("Boosters");

        // Check that all boosters are in state "Ready"
        foreach (var booster in await boosters!.GetAllNodesAsync())
            Assert.That(await booster.GetValueAsync("State"), Is.EqualTo("Ready"));

        await cursor.PreviousAsync();
        rocketship = cursor.NodeGuids[rocketshipGuid];

        await stepTwoAssertions();
    }

    [Test]
    public void TestCopy()
    {
        Assert.Fail();
    }

    [Test]
    public void TestCompare()
    {
        Assert.Fail();
    }

    [Test]
    public void TestFork()
    {
        Assert.Fail();
    }

    [Test]
    public async Task TestCursorPropagatesToEdgeNode()
    {
        var cursor = Fixtures.GetCursor();
        var booster = Fixtures.GetBooster();
        var fuelTank = Fixtures.GetFuelTank();

        booster.Cursor = cursor;

        await booster.SetValueAsync("FuelTank", fuelTank);

        Assert.NotNull(booster.Cursor);
        Assert.That(booster.Cursor == fuelTank.Cursor);
    }

    [Test]
    public void TestGetNodeByGuid()
    {
        var boosters = Fixtures.GetBoosters(3);
        Cursor cursor = new(Fixtures.GetSession(), boosters);

        foreach (Booster booster in boosters) Assert.That(ReferenceEquals(cursor.GetNode(booster.Guid), booster));
    }

    [Test]
    public async Task TestRetriveEdgeNode()
    {
        var cursor = await Fixtures.GetCursorWithHistoryAsync();

        var fuelTank = await cursor.Nodes.Select(n => n).Where(n => n.Tag == "Booster").ToList().Last()
            .GetValueAsync("FuelTank");

        Assert.That(fuelTank, Is.InstanceOf(typeof(FuelTank)));
        var guid = ((Node)fuelTank!).Guid;

        var retrievedEdgeNode = await cursor.SearchAsync(guid);
        if (retrievedEdgeNode == null) Assert.Fail("Failed to retrieve edge node from data store.");

        CollectionAssert.AreEquivalent(new NestedDictionary
        {
            {
                "Meta", new FlatDictionary
                {
                    { "Guid", ((Node)fuelTank).Guid },
                    { "Tag", "FuelTank" },
                    { "Deleted", false }
                }
            },
            {
                "Data", new FlatDictionary
                {
                    {
                        "Fields", new FlatDictionary
                        {
                            { "SerialNumber", ((Node)fuelTank).GetValueAsync("SerialNumber").Result },
                            { "FuelRemaining", 100 }
                        }
                    },
                    {
                        "Edges", new FlatDictionary()
                    },
                    {
                        "EdgeCollections", new FlatDictionary()
                    }
                }
            }
        }, await retrievedEdgeNode!.AsDictionaryAsync());
    }

    [Test]
    public async Task TestRetriveEdgeCollectionNode()
    {
        var cursor = await Fixtures.GetCursorWithHistoryAsync();
        var rocketship = cursor.Nodes.Select(n => n).Where(n => n.Tag == "Rocketship").ToList()[0];

        var boosters = (EdgeCollection<Node, Node>?)await rocketship.GetValueAsync("Boosters");
        Assert.That(boosters, Is.InstanceOf(typeof(EdgeCollection<Node, Node>)));

        var booster = (await boosters!.GetAllNodesAsync())[0];
        Assert.That(booster, Is.InstanceOf(typeof(Node)));
        var guid = booster.Guid;

        var retrievedEdgeNode = await cursor.SearchAsync(guid);
        if (retrievedEdgeNode == null) Assert.Fail("Failed to retrieve edge node from data store.");

        CollectionAssert.AreEquivalent(new NestedDictionary
        {
            {
                "Meta", new FlatDictionary
                {
                    { "Guid", booster.Guid },
                    { "Tag", "Booster" },
                    { "Deleted", false }
                }
            },
            {
                "Data", new FlatDictionary
                {
                    {
                        "Fields", new FlatDictionary
                        {
                            { "SerialNumber", booster.GetValueAsync("SerialNumber").Result },
                            { "State", "Ready" }
                        }
                    },
                    {
                        "Edges", new FlatDictionary
                        {
                            { "FuelTank", null }
                        }
                    },
                    {
                        "EdgeCollections", new FlatDictionary()
                    }
                }
            }
        }, await retrievedEdgeNode!.AsDictionaryAsync());

        // TODO: test retrieve by index
    }

    [Test]
    public async Task TestInMemoryReplay()
    {
        var cursor = await Fixtures.GetInMemoryTestCursorAsync();
        var rocketship = cursor.Nodes.Select(n => n).Where(n => n.Tag == "Rocketship").ToList()[0];
        var rocketshipGuid = rocketship.Guid;

        var getBoosterCount = () => cursor.Nodes.Select(n => n).Where(n => n.Tag == "Booster").ToList().Count;
        var getFuelTankCount = () => cursor.Nodes.Select(n => n).Where(n => n.Tag == "FuelTank").ToList().Count;

        // The cursor does not yet know about these! They will be lazy loaded.
        Assert.That(getBoosterCount(), Is.EqualTo(0));
        Assert.That(getFuelTankCount(), Is.EqualTo(0));

        var boosters = (EdgeCollection<Node, Node>?)await rocketship!.GetValueAsync("Boosters");
        Assert.That(boosters, Is.InstanceOf(typeof(EdgeCollection<Node, Node>)));
        Assert.That(boosters!.Count, Is.EqualTo(6));

        rocketship = await cursor.InceptionAsync(rocketshipGuid);

        // Assertions for cursor at inception
        Assert.That(rocketship, Is.InstanceOf(typeof(Rocketship)));
        Assert.That(cursor.State, Is.EqualTo(CursorStateEnum.Rewind));
        Assert.That(cursor.Nodes.Count, Is.EqualTo(1));

        boosters = (EdgeCollection<Node, Node>?)await rocketship!.GetValueAsync("Boosters");
        Assert.That(boosters, Is.InstanceOf(typeof(EdgeCollection<Node, Node>)));
        Assert.That(boosters!.Count, Is.EqualTo(3));

        foreach (var booster in await boosters!.GetAllNodesAsync())
        {
            Assert.IsInstanceOf(typeof(Booster), booster);

            CollectionAssert.AreEquivalent(new NestedDictionary
            {
                {
                    "Meta", new FlatDictionary
                    {
                        { "Guid", booster.Guid },
                        { "Tag", "Booster" },
                        { "Deleted", false }
                    }
                },
                {
                    "Data", new FlatDictionary
                    {
                        {
                            "Fields", new FlatDictionary
                            {
                                { "SerialNumber", booster.GetValueAsync("SerialNumber").Result },
                                { "State", "Unready" }
                            }
                        },
                        {
                            "Edges", new FlatDictionary
                            {
                                { "FuelTank", null }
                            }
                        },
                        {
                            "EdgeCollections", new FlatDictionary()
                        }
                    }
                }
            }, await booster.AsDictionaryAsync());
        }

        Assert.That(cursor.Nodes.Count, Is.EqualTo(4));

        await cursor.NextAsync();
        rocketship = cursor.NodeGuids[rocketshipGuid];

        // Assertions for cursor after adding extra boosters
        Func<Task> stepTwoAssertions = async () =>
        {
            Assert.That(cursor.State, Is.EqualTo(CursorStateEnum.Rewind));
            Assert.That(cursor.Nodes.Count, Is.EqualTo(getBoosterCount() + getFuelTankCount() + 1));

            boosters = (EdgeCollection<Node, Node>?)await rocketship!.GetValueAsync("Boosters");
            Assert.That(boosters?.Count, Is.EqualTo(6));

            FlatDictionary? expectedFuelTank;
            var actualFuelTankCount = 0;
            foreach (var booster in await boosters!.GetAllNodesAsync())
            {
                Assert.IsInstanceOf(typeof(Booster), booster);

                var fuelTank = (Node?)await booster.GetValueAsync("FuelTank");

                if (fuelTank is FuelTank)
                {
                    fuelTank = await fuelTank.LoadAsync();
                    Assert.That(await fuelTank!.GetValueAsync("FuelRemaining"), Is.EqualTo(100));
                    Assert.That(await fuelTank.GetValueAsync("SerialNumber"), Is.Not.Null);

                    expectedFuelTank = new FlatDictionary
                    {
                        {
                            "Meta", new FlatDictionary
                            {
                                { "Guid", fuelTank.Guid },
                                { "Tag", "FuelTank" },
                                { "Deleted", false }
                            }
                        }
                    };
                    actualFuelTankCount++;
                }
                else
                {
                    expectedFuelTank = null;
                }

                CollectionAssert.AreEquivalent(new NestedDictionary
                {
                    {
                        "Meta", new FlatDictionary
                        {
                            { "Guid", booster.Guid },
                            { "Tag", "Booster" },
                            { "Deleted", false }
                        }
                    },
                    {
                        "Data", new FlatDictionary
                        {
                            {
                                "Fields", new FlatDictionary
                                {
                                    { "SerialNumber", booster.GetValueAsync("SerialNumber").Result },
                                    { "State", "Unready" }
                                }
                            },
                            {
                                "Edges", new FlatDictionary
                                {
                                    { "FuelTank", expectedFuelTank }
                                }
                            },
                            {
                                "EdgeCollections", new FlatDictionary()
                            }
                        }
                    }
                }, await booster.AsDictionaryAsync());
            }

            Assert.That(actualFuelTankCount, Is.EqualTo(getFuelTankCount()));
        };
        await stepTwoAssertions();

        await cursor.NextAsync();
        rocketship = cursor.NodeGuids[rocketshipGuid];
        boosters = (EdgeCollection<Node, Node>?)await rocketship!.GetValueAsync("Boosters");

        // Check that all boosters are in state "Ready"
        foreach (var booster in await boosters!.GetAllNodesAsync())
            Assert.That(await booster.GetValueAsync("State"), Is.EqualTo("Ready"));

        await cursor.PreviousAsync();
        rocketship = cursor.NodeGuids[rocketshipGuid];

        await stepTwoAssertions();
    }

    [Test]
    public async Task TestTargetRelevantKeyframes()
    {
        var cursor = Fixtures.GetApiTestCursor(Guid.NewGuid());

        // Inspect rocketship and get a booster
        var rocketship = await cursor.SearchAsync(Constants.RocketshipGuid);
        var boosters = (EdgeCollection<Node, Node>?)await rocketship!.GetValueAsync("Boosters");
        var booster = await boosters!.GetNodeAtIndexAsync(0);

        Assert.That(await booster!.GetValueAsync("FuelTank"), Is.Null);

        // Attach a fuel tank to the booster
        await cursor.TogetherAsync(ModeEnum.Data, operation: async Task () =>
        {
            var fuelTank = Fixtures.GetFuelTank("F_4", 99);

            await Task.WhenAll(new List<Task>
            {
                booster.SetValueAsync("FuelTank", fuelTank),
                booster.SaveAsync(),
                fuelTank.SaveAsync()
            });
        });

        // Let's take a moment to verify the lazy-loading behavior when state is Live,
        // since the logic must contradict the following test cases.
        booster = await cursor.PreviousAsync(booster.Guid);
        // Assertions for the last recorded keyframe, which is technically not Live
        var fuelTank = (Node?)await booster!.GetValueAsync("FuelTank");
        Assert.That(fuelTank?.Tag, Is.EqualTo("FuelTank"));

        booster = await cursor.PreviousAsync(booster.Guid);
        Assert.That(await booster!.GetValueAsync("FuelTank"), Is.Null);

        await cursor.NowAsync(new List<Guid> { rocketship.Guid, booster.Guid });
        rocketship = cursor.NodeGuids[rocketship.Guid];
        booster = cursor.NodeGuids[booster.Guid];
        fuelTank = (Node?)await booster!.GetValueAsync("FuelTank");
        Assert.That(fuelTank?.Tag, Is.EqualTo("FuelTank"));

        // Include a rocketship delta (this delta should be skipped during the following replay sequence)
        await rocketship!.SetValueAsync("Name", "Plywood Pelican");

        // Include a delta that's relevant only to the fuel tank...
        fuelTank = await fuelTank!.LoadAsync();
        await fuelTank!.SetValueAsync("FuelRemaining", 50);
        await fuelTank.SaveAsync();

        // ...followed by a delta that's relevant only to the booster.
        await booster.SetValueAsync("State", "ReadyFreddy");
        await booster.SaveAsync();

        // Replay and verify that each keyframe is relevant to the inspected booster
        booster = await cursor.InceptionAsync(booster.Guid);
        Assert.That(await booster!.GetValueAsync("State"), Is.EqualTo("Unready"));
        Assert.That(await booster.GetValueAsync("FuelTank"), Is.Null);

        booster = await cursor.NextAsync(booster.Guid);
        Assert.That(await booster!.GetValueAsync("State"), Is.EqualTo("Ready"));
        Assert.That(await booster.GetValueAsync("FuelTank"), Is.Null);

        booster = await cursor.NextAsync(booster!.Guid);
        fuelTank = (Node?)await booster!.GetValueAsync("FuelTank");
        Assert.That(fuelTank?.Tag, Is.EqualTo("FuelTank"));
        fuelTank = await fuelTank!.LoadAsync();
        Assert.That(await fuelTank!.GetValueAsync("FuelRemaining"), Is.EqualTo(99));

        // Include the fuel tank in the search, and make sure we hit the delta that's specific to it,
        // but not the rocketship delta.
        await cursor.NextAsync(new List<Guid> { booster.Guid, fuelTank.Guid });
        booster = cursor.NodeGuids[booster.Guid];
        fuelTank = cursor.NodeGuids[fuelTank.Guid];
        Assert.That(await fuelTank.GetValueAsync("FuelRemaining"), Is.EqualTo(50));
        Assert.That(await booster.GetValueAsync("State"), Is.EqualTo("Ready"));

        await cursor.NextAsync(new List<Guid> { booster.Guid, fuelTank.Guid });
        booster = cursor.NodeGuids[booster.Guid];
        Assert.That(await booster.GetValueAsync("State"), Is.EqualTo("ReadyFreddy"));

        // Test Cursor.PreviousAsync...

        // If we only include the fuel tank, the booster-specific delta should be skipped
        await cursor.NowAsync();
        fuelTank = await cursor.PreviousAsync(fuelTank!.Guid);
        Assert.That(await fuelTank!.GetValueAsync("FuelRemaining"), Is.EqualTo(50));

        fuelTank = await cursor.PreviousAsync(fuelTank.Guid);
        Assert.That(await fuelTank!.GetValueAsync("FuelRemaining"), Is.EqualTo(99));

        // If we include the booster and the fuel tank, the rocketship delta should be skipped
        await cursor.NowAsync();
        await cursor.PreviousAsync(new List<Guid> { booster.Guid, fuelTank.Guid });
        booster = cursor.NodeGuids[booster.Guid];
        fuelTank = cursor.NodeGuids[fuelTank.Guid];
        Assert.That(await booster!.GetValueAsync("State"), Is.EqualTo("ReadyFreddy"));
        Assert.That(await fuelTank!.GetValueAsync("FuelRemaining"), Is.EqualTo(50));

        await cursor.PreviousAsync(new List<Guid> { booster.Guid, fuelTank.Guid });
        booster = cursor.NodeGuids[booster.Guid];
        Assert.That(await booster!.GetValueAsync("State"), Is.EqualTo("Ready"));

        await cursor.PreviousAsync(new List<Guid> { booster.Guid, fuelTank.Guid });
        fuelTank = cursor.NodeGuids[fuelTank.Guid];
        Assert.That(await fuelTank!.GetValueAsync("FuelRemaining"), Is.EqualTo(99));

        await cursor.PreviousAsync(new List<Guid> { booster.Guid, fuelTank.Guid });
        booster = cursor.NodeGuids[booster.Guid];
        Assert.That(await booster!.GetValueAsync("FuelTank"), Is.Null);
    }

    [Test]
    public async Task TestSchemaless()
    {
        var cursor = await Fixtures.GetInMemoryTestCursorAsync();

        // Instantiate from a schemaless node rep -- do not include edges or edge collections
        var bicycleRep = new NodeRep();
        bicycleRep.Meta.Tag = "Bicycle";
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

        var bicycle = new Node(
            null,
            cursor,
            bicycleRep,
            Utils.TimestampMillis(),
            "Bicycle"
        );
        await bicycle.SaveAsync(ModeEnum.Data);

        bicycle = await cursor.SearchAsync(bicycle.Guid);
        Assert.That(bicycle, Is.Not.Null);
        Assert.That(await bicycle!.GetValueAsync("Name"), Is.EqualTo("Speedster"));
        Assert.That(await bicycle.GetValueAsync("Color"), Is.EqualTo("Red"));
        Assert.That(await bicycle.GetValueAsync("Price"), Is.EqualTo(999.99m));

        // Change a value and introduce a new field.
        await bicycle.SetValueAsync("Name", "Slowpoke");
        await bicycle.SetValueAsync("Type", "Mountain");

        await bicycle.SaveAsync();

        bicycle = await cursor.NowAsync(bicycle.Guid);
        Assert.That(bicycle, Is.Not.Null);
        Assert.That(await bicycle!.GetValueAsync("Name"), Is.EqualTo("Slowpoke"));
        Assert.That(await bicycle.GetValueAsync("Type"), Is.EqualTo("Mountain"));

        // Introduce a new Edge.
        var seat = Fixtures.NewSchemalessNode("Saddle", cursor);
        var seatGuid = seat.Guid;

        await cursor.TogetherAsync(ModeEnum.Data, operation: async Task () =>
        {
            var tasks = new List<Task>
            {
                bicycle.SetValueAsync("Seat", seat),
                seat.SaveAsync(),
                bicycle.SaveAsync()
            };
            await Task.WhenAll(tasks);
        });

        bicycle = await cursor.NowAsync(bicycle.Guid);
        Assert.That(bicycle, Is.Not.Null);
        seat = (Node?)await bicycle!.GetValueAsync("Seat");
        Assert.That(seat, Is.Not.Null);
        Assert.That(seat!.Guid, Is.EqualTo(seatGuid));
        Assert.That(seat.Tag, Is.EqualTo("Saddle"));

        // Introduce a new EdgeCollection.
        var frontWheel = Fixtures.NewSchemalessNode("Wheel", cursor);
        var rearWheel = Fixtures.NewSchemalessNode("Wheel", cursor);

        var wheels = new EdgeCollection<Node, Node>(bicycle, "Wheel",
            new List<Node> { frontWheel, rearWheel });

        await cursor.TogetherAsync(ModeEnum.Data, operation: async Task () =>
        {
            var tasks = new List<Task>
            {
                bicycle.SetValueAsync("Wheels", wheels),
                frontWheel.SaveAsync(),
                rearWheel.SaveAsync(),
                bicycle.SaveAsync()
            };
            await Task.WhenAll(tasks);
        });

        bicycle = await cursor.NowAsync(bicycle.Guid);
        Assert.That(bicycle, Is.Not.Null);
        wheels = (EdgeCollection<Node, Node>?)await bicycle!.GetValueAsync("Wheels");
        Assert.That(wheels, Is.Not.Null);
        Assert.That(wheels!.Count, Is.EqualTo(2));
        Assert.That(wheels.GetGuidsForRange(0, 2), Is.EquivalentTo(new List<Guid>
            { frontWheel.Guid, rearWheel.Guid }));

        // Test deletion propagation
        seat = (Node?)await bicycle.GetValueAsync("Seat");
        Assert.That(seat, Is.Not.Null);

        frontWheel = (Node?)await wheels.GetNodeAtIndexAsync(0);
        Assert.That(frontWheel, Is.Not.Null);

        // The following assertions depend on InMemoryDataStore.GetReferencingNodesAsync(),
        // which is not yet implemented and is likely not worth implementing.
        /* await cursor.TogetherAsync(ModeEnum.Data, operation: async Task() =>
        {
            await Task.WhenAll(new List<Task>
            {
                seat!.DeleteAsync(),
                frontWheel!.DeleteAsync()
            });
        });

        bicycle = await cursor.NowAsync(bicycle.Guid);
        Assert.That(bicycle, Is.Not.Null);
        seat = (Node?)await bicycle!.GetValueAsync("Seat");
        Assert.That(seat, Is.Null);
        wheels = (EdgeCollection<Node, Node>?)await bicycle!.GetValueAsync("Wheels");
        Assert.That(wheels, Is.Not.Null);
        Assert.That(wheels!.Count, Is.EqualTo(1));
        Assert.That(wheels.GetGuidAtIndex(0), Is.EqualTo(rearWheel.Guid)); */
    }

    [Test]
    public async Task TestDeleteNodes()
    {
        var cursor = new Cursor(new Session(new MongoDataStore()));
        Fixtures.GetBicycleSchema(cursor);

        var bicycle = cursor.Nodes.Where(n => n.Tag == "Bicycle").First();
        var bicycleGuid = bicycle.Guid;
        var chain = cursor.Nodes.Where(n => n.Tag == "Chain").First();
        var chainGuid = chain.Guid;
        var bell = cursor.Nodes.Where(n => n.Tag == "Bell").First();
        var bellGuid = bell.Guid;
        var seat = cursor.Nodes.Where(n => n.Tag == "Saddle").First();
        var seatGuid = seat.Guid;
        var leftPedal = cursor.Nodes.Where(n => n.Tag == "Pedal").First();
        var leftPedalGuid = leftPedal.Guid;
        var rightPedal = cursor.Nodes.Where(n => n.Tag == "Pedal").Last();
        var rightPedalGuid = rightPedal.Guid;

        var bicycle2 = Fixtures.NewSchemalessNode("Bicycle", cursor, new NodeRep());
        var bicycle2Guid = bicycle2.Guid;

        await Task.WhenAll(new List<Task> {
            bicycle2.SetValueAsync("Chain", chain),
            bicycle.SetValueAsync("Chain", chain)
        });
        await cursor.TogetherAsync(ModeEnum.Data, operation: async Task () =>
            await Task.WhenAll(cursor.Nodes.Select(node => node.SaveAsync()))
        );

        await cursor.NowAsync(new List<Guid> { bicycle.Guid, chain.Guid });
        chain = cursor.NodeGuids[chainGuid];

        await chain.DeleteAsync(); // automatically executes a together block

        await cursor.NowAsync(new List<Guid>
        {
            bicycleGuid,
            bicycle2Guid,
            chainGuid,
            bellGuid,
            seatGuid
        });

        // Deleted nodes should not remain on the cursor
        Assert.That(cursor.NodeGuids.ContainsKey(chainGuid), Is.False);

        // Deletions should propagate to all nodes that reference the deleted node...

        // Test shared single edge
        Assert.That(await cursor.NodeGuids[bicycleGuid].GetValueAsync("Chain"), Is.Null);
        Assert.That(await cursor.NodeGuids[bicycle2Guid].GetValueAsync("Chain"), Is.Null);

        // Test multi edge from same node
        bell = cursor.Nodes.Where(n => n.Tag == "Bell").First();
        seat = cursor.Nodes.Where(n => n.Tag == "Saddle").First();
        await cursor.TogetherAsync(ModeEnum.Data, operation: async Task () =>
        {
            await Task.WhenAll(new List<Task>
            {
                bell!.DeleteAsync(),
                seat!.DeleteAsync()
            });
        });

        bicycle = await cursor.NowAsync(bicycleGuid);
        Assert.That(await bicycle!.GetValueAsync("Bell"), Is.Null);
        Assert.That(await bicycle.GetValueAsync("Seat"), Is.Null);

        // Test shared edge collections item from different nodes
        var wheels = (EdgeCollection<Node, Node>?)await bicycle.GetValueAsync("Wheels");
        Assert.That(wheels!.Count, Is.EqualTo(2));
        var frontWheel = await wheels.GetNodeAtIndexAsync(0);
        var frontWheelGuid = frontWheel!.Guid;
        var rearWheel = await wheels.GetNodeAtIndexAsync(1);
        var rearWheelGuid = rearWheel!.Guid;
        bicycle2 = cursor.NodeGuids[bicycle2Guid];
        await bicycle2.SetValueAsync("Wheels", new EdgeCollection<Node, Node>(bicycle2, "Wheels",
            new List<Node> { frontWheel }));

        await bicycle2.SaveAsync(); // non-together block

        await cursor.NowAsync();
        CollectionAssert.Contains(cursor.NodeGuids.Keys, frontWheelGuid);
        await frontWheel!.DeleteAsync(); // automatically executes a together block

        await cursor.NowAsync(new List<Guid> { bicycleGuid, bicycle2Guid });

        bicycle = cursor.NodeGuids[bicycleGuid];
        wheels = (EdgeCollection<Node, Node>?)await bicycle.GetValueAsync("Wheels");
        Assert.That(wheels, Is.Not.Null);
        Assert.That(wheels!.Count, Is.EqualTo(1));
        Assert.That(wheels.GetGuidAtIndex(0), Is.EqualTo(rearWheelGuid));

        var wheels2 = (EdgeCollection<Node, Node>?)await cursor.NodeGuids[bicycle2Guid].GetValueAsync("Wheels");
        Assert.That(wheels2, Is.Not.Null);
        Assert.That(wheels2!.Count, Is.EqualTo(0));

        // Test delete from more than one edge collection on same node
        await bicycle.SetValueAsync("Chain", Fixtures.NewSchemalessNode("Chain", cursor));
        chain = cursor.Nodes.Where(n => n.Tag == "Chain").Last();

        await cursor.TogetherAsync(ModeEnum.Data, operation: async Task () =>
            await Task.WhenAll(new List<Task> {
                chain.SaveAsync(),
                bicycle.SaveAsync()
            })
        );

        await cursor.NowAsync(new List<Guid> { bicycleGuid, leftPedalGuid, rearWheelGuid });

        await cursor.TogetherAsync(ModeEnum.Data, operation: async Task () =>
        {
            await Task.WhenAll(new List<Task>
            {
                leftPedal.DeleteAsync(),
                rearWheel.DeleteAsync()
            });
        });

        bicycle = await cursor.NowAsync(bicycleGuid);
        wheels = (EdgeCollection<Node, Node>?)await bicycle!.GetValueAsync("Wheels");
        Assert.That(wheels!.Count, Is.EqualTo(0));
        var pedals = (EdgeCollection<Node, Node>?)await bicycle.GetValueAsync("Pedals");
        Assert.That(pedals!.Count, Is.EqualTo(1));

        // Test edge / edge collection mix
        rightPedal = await pedals.GetNodeAtIndexAsync(0);

        await cursor.TogetherAsync(ModeEnum.Data, operation: async Task () =>
        {
            await Task.WhenAll(new List<Task>
            {
                chain!.DeleteAsync(),
                rightPedal!.DeleteAsync()
            });
        });

        bicycle = await cursor.NowAsync(bicycleGuid);
        Assert.That(await bicycle!.GetValueAsync("Chain"), Is.Null);
        pedals = (EdgeCollection<Node, Node>?)await bicycle.GetValueAsync("Pedals");
        Assert.That(pedals!.Count, Is.EqualTo(0));
    }
}