namespace Graph.Core;

public class InMemoryDataStore : AbstractDataStore
{
    private readonly SortedDictionary<ulong, Dictionary<Guid, NestedDictionary>> _datastore = new();
    private ulong? _lastAt;

    public InMemoryDataStore(Guid? sampleDataGuid = null, bool useNodeGuidConstants = true)
    {
        if (sampleDataGuid != null)
            _datastore = Constants.GetSampleData((Guid)sampleDataGuid, useNodeGuidConstants);
    }

    public override Task<List<ulong>> GetNodeTimestampsAsync(Guid nodeGuid)
    {
        var timestamps = new List<ulong>();
        foreach (var entry in _datastore)
            if (entry.Value.ContainsKey(nodeGuid))
                timestamps.Add(entry.Key);

        return Task.FromResult(timestamps);
    }

    public override async Task<(ulong? timestamp, List<Node> nodes)> SearchAsync(Query query,
        bool includeRemoved = false)
    {
        (ulong? timestampAt, Dictionary<Guid, NestedDictionary>? responseAt) = (null, null);
        (ulong? timestampAfter, Dictionary<Guid, NestedDictionary>? responseAfter) = (null, null);

        var at = Utils.TimestampMillisOrNull(query.AtTimestamp);
        var after = Utils.TimestampMillisOrNull(query.AfterTimestamp);

        if (at != null) (timestampAt, responseAt) = _SearchAt((ulong)at, query.IncludeGuids);
        if (after != null) (timestampAfter, responseAfter) = _SearchAfter((ulong)after);

        var nextTimestamp = timestampAfter ?? timestampAt;

        var guidsToInclude = query.IncludeGuids;

        if (guidsToInclude.Count() == 0)
            if (query.CurrentNodes != null)
                query.CurrentNodes.ForEach(node => { guidsToInclude.Add(node.Guid); });

        var nodes = await _BuildFromResponsesAsync(responseAt, responseAfter, guidsToInclude, includeRemoved);

        return (nodes.Count() == 0 ? null : nextTimestamp, nodes);
    }

    public override Task<List<Node>> GetReferencingNodesAsync(Guid toNodeGuid)
    {
        throw new NotImplementedException();
    }

    protected override async Task SerializeAsync(List<Node> nodes, ulong? timestamp, ModeEnum mode = ModeEnum.Delta)
    {
        if (timestamp == null)
            timestamp = Utils.TimestampMillis(timestamp);

        var nodeDict = await _SerializeNodeDataAsync(nodes, timestamp, mode);

        if (nodeDict.Any())
            _Insert((ulong)timestamp, nodeDict);
    }

    private async Task<List<NestedDictionary>> _SerializeNodeDataAsync(List<Node> nodes, ulong? timestamp = null,
        ModeEnum mode = ModeEnum.Delta)
    {
        var tasks = new List<Task<NestedDictionary?>>();
        foreach (var node in nodes) tasks.Add(_SerializeNodeAsync(node, timestamp, mode));
        await Task.WhenAll(tasks);

        var dictList = new List<NestedDictionary>();
        foreach (var task in tasks)
            if (task.Result != null)
                dictList.Add(task.Result);

        return dictList;
    }

    private async Task<NestedDictionary?> _SerializeNodeAsync(Node node, ulong? timestamp, ModeEnum mode)
    {
        var serializedNode = await node.AsDictionaryAsync(mode);

        if (serializedNode != null && timestamp != null)
            serializedNode["Meta"]["Timestamp"] = timestamp;

        return serializedNode;
    }

    private void _Insert(ulong timestamp, List<NestedDictionary> nodeDicts)
    {
        if (_datastore.ContainsKey(timestamp)) timestamp += 1;
        //throw new ArgumentException("InMemoryDatabase already contains timestamp: " + timestamp);

        foreach (var nodeDict in nodeDicts)
        {
            var guid = (Guid)nodeDict["Meta"]["Guid"]!;
            if (_datastore.ContainsKey(timestamp))
            {
                var nodeGuidsDict = _datastore[timestamp]!;
                nodeGuidsDict.Add(guid, nodeDict);
            }
            else
            {
                _datastore.Add(timestamp, new Dictionary<Guid, NestedDictionary>
                {
                    { guid, nodeDict }
                });
            }
        }
    }

    private Task<List<Node>> _BuildFromResponsesAsync(
        Dictionary<Guid, NestedDictionary>? responseAt,
        Dictionary<Guid, NestedDictionary>? responseAfter,
        HashSet<Guid> guidsToInclude,
        bool includeRemoved
    )
    {
        var nodes = new List<Node>();
        if (responseAt != null)
        {
            if (guidsToInclude.Any())
                responseAt = responseAt.Where(
                    nodeInfo => { return guidsToInclude.Contains(nodeInfo.Key); }
                ).ToDictionary(
                    nodeDict => nodeDict.Key,
                    nodeDict => nodeDict.Value
                );

            var includeGuidsAt = responseAt.Keys.ToList();

            if (responseAfter != null)
            {
                if (guidsToInclude.Any())
                    responseAfter = responseAfter.Where(
                        nodeInfo => { return guidsToInclude.Contains(nodeInfo.Key); }
                    ).ToDictionary(
                        nodeDict => nodeDict.Key,
                        nodeDict => nodeDict.Value
                    );

                var accountedForAt = new List<Guid>();
                foreach (var nodeGuid in responseAfter.Keys)
                {
                    NestedDictionary? nodeDict;
                    if (responseAt.ContainsKey(nodeGuid))
                    {
                        nodeDict = responseAt[nodeGuid];
                        nodeDict = Utils.ToData(responseAfter[nodeGuid], nodeDict, ModeEnum.Data);
                        nodeDict = Utils.ToData(responseAfter[nodeGuid], nodeDict, ModeEnum.Delta);

                        accountedForAt.Add(nodeGuid);
                    }
                    else
                    {
                        nodeDict = responseAfter[nodeGuid];
                    }

                    if (!_WasDeleted(nodeDict))
                        nodes.Add(Node.FromDictionary(nodeDict));
                }

                // Some nodes from responseAt might not be included yet.
                includeGuidsAt = includeGuidsAt.Where(g => !accountedForAt.Contains(g)).ToList();
            }

            foreach (var nodeGuid in includeGuidsAt)
                if (!_WasDeleted(responseAt[nodeGuid]))
                    nodes.Add(Node.FromDictionary(responseAt[nodeGuid]));
        }
        else if (responseAfter != null)
        {
            responseAfter = responseAfter.Where(
                nodeInfo => { return guidsToInclude.Contains(nodeInfo.Key); }
            ).ToDictionary(
                nodeDict => nodeDict.Key,
                nodeDict => nodeDict.Value
            );

            foreach (var nodeGuid in responseAfter.Keys)
            {
                var nodeDict = responseAfter[nodeGuid];

                if (!_WasDeleted(nodeDict))
                    nodes.Add(Node.FromDictionary(nodeDict));
            }
        }

        return Task.FromResult(nodes);
    }

    private (ulong?, Dictionary<Guid, NestedDictionary>?) _SearchAt(ulong timestamp, HashSet<Guid> guidsToInclude)
    {
        // Get the latest entry at or before the timestamp.
        var noResults = true;
        foreach (var entry in _datastore.Reverse())
            if (entry.Key <= timestamp)
            {
                timestamp = entry.Key;
                noResults = false;
                break;
            }

        if (noResults) return (null, null);

        _lastAt = timestamp;

        var result = _datastore[timestamp]!;

        Dictionary<Guid, NestedDictionary> data = new();

        foreach (var entry in _datastore)
            if (entry.Key <= timestamp)
                foreach (var nodeInfo in entry.Value)
                {
                    var guid = nodeInfo.Key;
                    if (!guidsToInclude.Contains(guid)) continue;

                    if (data.ContainsKey(guid))
                    {
                        var mode = nodeInfo.Value.ContainsKey(ModeEnum.Data.ToString())
                            ? ModeEnum.Data
                            : ModeEnum.Delta;
                        data[guid] = Utils.ToData(nodeInfo.Value, data[guid], mode);
                    }
                    else
                    {
                        data.Add(guid, nodeInfo.Value);
                    }
                }

        return (_lastAt, data);
    }

    private (ulong?, Dictionary<Guid, NestedDictionary>?) _SearchAfter(ulong timestamp)
    {
        Dictionary<Guid, NestedDictionary> data = new();
        if (timestamp < _datastore.First().Key) return (_datastore.First().Key, _datastore.First().Value);

        ulong? key = null;
        if (_lastAt == null)
            throw new InvalidOperationException("Tried to get next keyframe without a current keyframe.");

        // Get the next entry after the timestamp
        var enumerator = _datastore.Keys.SkipWhile(k => k != _lastAt);
        foreach (var ts in enumerator)
            if (ts > timestamp)
            {
                key = ts;
                break;
            }

        if (key == null) return (null, null);

        return ((ulong)key, _datastore[(ulong)key]);
    }

    private bool _WasDeleted(NestedDictionary nodeDict)
    {
        return (bool)nodeDict["Meta"]["Deleted"]!;
    }
}

public static class Constants
{
    public static Guid ApiSampleDataGuid = new("c3c2a144-5e3d-4680-a814-62181ea2dd38");
    public static List<ulong> Timestamps = new() { 1662503987346, 1662504255620, 1662504255891 };
    public static Guid RocketshipGuid = new("12345678-1234-1234-1234-1234567890ab");

    public static List<Guid> BoosterGuids = new()
    {
        new Guid("d737ae62-e0b9-4189-9c89-f6e3d810ab65"),
        new Guid("4ebd0e12-b794-497f-abb4-d1dfe15ccbb3"),
        new Guid("cedf812f-0c1a-4bf8-bb67-73088fd31f8e")
    };

    public static List<Guid> AdditionalBoosterGuids = new()
    {
        new Guid("8cf8417b-a23e-423e-9a8b-2cafe193abcb"),
        new Guid("27e13347-255d-4d7c-919d-773020d306a8"),
        new Guid("e2a80695-6f89-4963-9aad-62b4bdc95f67")
    };

    public static List<Guid> FuelTankGuids = new()
    {
        new Guid("bb421b3a-ee49-47b1-95f3-7163b59e3790"),
        new Guid("28a77e9b-c845-4511-923b-c56a80b91565"),
        new Guid("063420af-a877-40a1-a179-70203afe5a6a")
    };

    public static Dictionary<Guid, SortedDictionary<ulong, Dictionary<Guid, NestedDictionary>>> SampleData = new();

    public static SortedDictionary<ulong, Dictionary<Guid, NestedDictionary>> GetSampleData(
        Guid sampleDataGuid, bool useNodeGuidConstants = true
    )
    {
        if (!SampleData.ContainsKey(sampleDataGuid))
        {
            var rocketshipGuid = useNodeGuidConstants ? RocketshipGuid : Guid.NewGuid();
            var boosterGuids = useNodeGuidConstants
                ? BoosterGuids
                : new List<Guid>
                {
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    Guid.NewGuid()
                };
            var additionalBoosterGuids = useNodeGuidConstants
                ? AdditionalBoosterGuids
                : new List<Guid>
                {
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    Guid.NewGuid()
                };
            var fuelTankGuids = useNodeGuidConstants
                ? FuelTankGuids
                : new List<Guid>
                {
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    Guid.NewGuid()
                };

            SampleData[sampleDataGuid] = new SortedDictionary<ulong, Dictionary<Guid, NestedDictionary>>
            {
                {
                    Timestamps[0], new Dictionary<Guid, NestedDictionary>
                    {
                        {
                            rocketshipGuid, new NestedDictionary
                            {
                                {
                                    "Meta", new FlatDictionary
                                    {
                                        { "Guid", rocketshipGuid },
                                        { "Tag", "Rocketship" },
                                        { "Deleted", false },
                                        { "Timestamp", Timestamps[0] }
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
                                                        { boosterGuids[0], boosterGuids[1] },
                                                        { boosterGuids[1], boosterGuids[2] },
                                                        { boosterGuids[2], null }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        },
                        {
                            boosterGuids[0], new NestedDictionary
                            {
                                {
                                    "Meta", new FlatDictionary
                                    {
                                        { "Guid", boosterGuids[0] },
                                        { "Tag", "Booster" },
                                        { "Deleted", false },
                                        { "Timestamp", Timestamps[0] }
                                    }
                                },
                                {
                                    "Data", new FlatDictionary
                                    {
                                        {
                                            "Fields", new FlatDictionary
                                            {
                                                { "SerialNumber", "B_1" },
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
                            }
                        },
                        {
                            boosterGuids[1], new NestedDictionary
                            {
                                {
                                    "Meta", new FlatDictionary
                                    {
                                        { "Guid", boosterGuids[1] },
                                        { "Tag", "Booster" },
                                        { "Deleted", false },
                                        { "Timestamp", Timestamps[0] }
                                    }
                                },
                                {
                                    "Data", new FlatDictionary
                                    {
                                        {
                                            "Fields", new FlatDictionary
                                            {
                                                { "SerialNumber", "B_2" },
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
                            }
                        },
                        {
                            boosterGuids[2], new NestedDictionary
                            {
                                {
                                    "Meta", new FlatDictionary
                                    {
                                        { "Guid", boosterGuids[2] },
                                        { "Tag", "Booster" },
                                        { "Deleted", false },
                                        { "Timestamp", Timestamps[0] }
                                    }
                                },
                                {
                                    "Data", new FlatDictionary
                                    {
                                        {
                                            "Fields", new FlatDictionary
                                            {
                                                { "SerialNumber", "B_3" },
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
                            }
                        }
                    }
                },
                {
                    Timestamps[1], new Dictionary<Guid, NestedDictionary>
                    {
                        {
                            additionalBoosterGuids[0], new NestedDictionary
                            {
                                {
                                    "Meta", new FlatDictionary
                                    {
                                        { "Guid", additionalBoosterGuids[0] },
                                        { "Tag", "Booster" },
                                        { "Deleted", false },
                                        { "Timestamp", Timestamps[1] }
                                    }
                                },
                                {
                                    "Data", new FlatDictionary
                                    {
                                        {
                                            "Fields", new FlatDictionary
                                            {
                                                { "SerialNumber", "B_4" },
                                                { "State", "Unready" }
                                            }
                                        },
                                        {
                                            "Edges", new FlatDictionary
                                            {
                                                {
                                                    "FuelTank", new NestedDictionary
                                                    {
                                                        {
                                                            "Meta", new FlatDictionary
                                                            {
                                                                { "Guid", fuelTankGuids[0] },
                                                                { "Tag", "FuelTank" },
                                                                { "Deleted", false }
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        },
                                        {
                                            "EdgeCollections", new FlatDictionary()
                                        }
                                    }
                                }
                            }
                        },
                        {
                            additionalBoosterGuids[1], new NestedDictionary
                            {
                                {
                                    "Meta", new FlatDictionary
                                    {
                                        { "Guid", additionalBoosterGuids[1] },
                                        { "Tag", "Booster" },
                                        { "Deleted", false },
                                        { "Timestamp", Timestamps[1] }
                                    }
                                },
                                {
                                    "Data", new FlatDictionary
                                    {
                                        {
                                            "Fields", new FlatDictionary
                                            {
                                                { "SerialNumber", "B_5" },
                                                { "State", "Unready" }
                                            }
                                        },
                                        {
                                            "Edges", new FlatDictionary
                                            {
                                                {
                                                    "FuelTank", new NestedDictionary
                                                    {
                                                        {
                                                            "Meta", new FlatDictionary
                                                            {
                                                                { "Guid", fuelTankGuids[1] },
                                                                { "Tag", "FuelTank" },
                                                                { "Deleted", false }
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        },
                                        {
                                            "EdgeCollections", new FlatDictionary()
                                        }
                                    }
                                }
                            }
                        },
                        {
                            additionalBoosterGuids[2], new NestedDictionary
                            {
                                {
                                    "Meta", new FlatDictionary
                                    {
                                        { "Guid", additionalBoosterGuids[2] },
                                        { "Tag", "Booster" },
                                        { "Deleted", false },
                                        { "Timestamp", Timestamps[1] }
                                    }
                                },
                                {
                                    "Data", new FlatDictionary
                                    {
                                        {
                                            "Fields", new FlatDictionary
                                            {
                                                { "SerialNumber", "B_6" },
                                                { "State", "Unready" }
                                            }
                                        },
                                        {
                                            "Edges", new FlatDictionary
                                            {
                                                {
                                                    "FuelTank", new NestedDictionary
                                                    {
                                                        {
                                                            "Meta", new FlatDictionary
                                                            {
                                                                { "Guid", fuelTankGuids[2] },
                                                                { "Tag", "FuelTank" },
                                                                { "Deleted", false }
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        },
                                        {
                                            "EdgeCollections", new FlatDictionary()
                                        }
                                    }
                                }
                            }
                        },
                        {
                            fuelTankGuids[0], new NestedDictionary
                            {
                                {
                                    "Meta", new FlatDictionary
                                    {
                                        { "Guid", fuelTankGuids[0] },
                                        { "Tag", "FuelTank" },
                                        { "Deleted", false },
                                        { "Timestamp", Timestamps[1] }
                                    }
                                },
                                {
                                    "Data", new FlatDictionary
                                    {
                                        {
                                            "Fields", new FlatDictionary
                                            {
                                                { "SerialNumber", "F_1" },
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
                            }
                        },
                        {
                            fuelTankGuids[1], new NestedDictionary
                            {
                                {
                                    "Meta", new FlatDictionary
                                    {
                                        { "Guid", fuelTankGuids[1] },
                                        { "Tag", "FuelTank" },
                                        { "Deleted", false },
                                        { "Timestamp", Timestamps[1] }
                                    }
                                },
                                {
                                    "Data", new FlatDictionary
                                    {
                                        {
                                            "Fields", new FlatDictionary
                                            {
                                                { "SerialNumber", "F_2" },
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
                            }
                        },
                        {
                            fuelTankGuids[2], new NestedDictionary
                            {
                                {
                                    "Meta", new FlatDictionary
                                    {
                                        { "Guid", fuelTankGuids[2] },
                                        { "Tag", "FuelTank" },
                                        { "Deleted", false },
                                        { "Timestamp", Timestamps[1] }
                                    }
                                },
                                {
                                    "Data", new FlatDictionary
                                    {
                                        {
                                            "Fields", new FlatDictionary
                                            {
                                                { "SerialNumber", "F_3" },
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
                            }
                        },
                        {
                            rocketshipGuid, new NestedDictionary
                            {
                                {
                                    "Meta", new FlatDictionary
                                    {
                                        { "Guid", rocketshipGuid },
                                        { "Tag", "Rocketship" },
                                        { "Deleted", false },
                                        { "Timestamp", Timestamps[1] }
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
                                                        { boosterGuids[0], boosterGuids[1] },
                                                        { boosterGuids[1], boosterGuids[2] },
                                                        { boosterGuids[2], additionalBoosterGuids[0] },
                                                        { additionalBoosterGuids[0], additionalBoosterGuids[1] },
                                                        { additionalBoosterGuids[1], additionalBoosterGuids[2] },
                                                        { additionalBoosterGuids[2], null }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                },
                {
                    Timestamps[2], new Dictionary<Guid, NestedDictionary>
                    {
                        {
                            boosterGuids[0], new NestedDictionary
                            {
                                {
                                    "Meta", new FlatDictionary
                                    {
                                        { "Guid", boosterGuids[0] },
                                        { "Tag", "Booster" },
                                        { "Deleted", false },
                                        { "Timestamp", Timestamps[1] }
                                    }
                                },
                                {
                                    "Delta", new FlatDictionary
                                    {
                                        {
                                            "Fields", new FlatDictionary
                                            {
                                                { "State", "Ready" }
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
                            }
                        },
                        {
                            boosterGuids[1], new NestedDictionary
                            {
                                {
                                    "Meta", new FlatDictionary
                                    {
                                        { "Guid", boosterGuids[1] },
                                        { "Tag", "Booster" },
                                        { "Deleted", false },
                                        { "Timestamp", Timestamps[1] }
                                    }
                                },
                                {
                                    "Delta", new FlatDictionary
                                    {
                                        {
                                            "Fields", new FlatDictionary
                                            {
                                                { "State", "Ready" }
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
                            }
                        },
                        {
                            boosterGuids[2], new NestedDictionary
                            {
                                {
                                    "Meta", new FlatDictionary
                                    {
                                        { "Guid", boosterGuids[2] },
                                        { "Tag", "Booster" },
                                        { "Deleted", false },
                                        { "Timestamp", Timestamps[1] }
                                    }
                                },
                                {
                                    "Delta", new FlatDictionary
                                    {
                                        {
                                            "Fields", new FlatDictionary
                                            {
                                                { "State", "Ready" }
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
                            }
                        },
                        {
                            additionalBoosterGuids[0], new NestedDictionary
                            {
                                {
                                    "Meta", new FlatDictionary
                                    {
                                        { "Guid", additionalBoosterGuids[0] },
                                        { "Tag", "Booster" },
                                        { "Deleted", false },
                                        { "Timestamp", Timestamps[1] }
                                    }
                                },
                                {
                                    "Delta", new FlatDictionary
                                    {
                                        {
                                            "Fields", new FlatDictionary
                                            {
                                                { "State", "Ready" }
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
                            }
                        },
                        {
                            additionalBoosterGuids[1], new NestedDictionary
                            {
                                {
                                    "Meta", new FlatDictionary
                                    {
                                        { "Guid", additionalBoosterGuids[1] },
                                        { "Tag", "Booster" },
                                        { "Deleted", false },
                                        { "Timestamp", Timestamps[1] }
                                    }
                                },
                                {
                                    "Delta", new FlatDictionary
                                    {
                                        {
                                            "Fields", new FlatDictionary
                                            {
                                                { "State", "Ready" }
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
                            }
                        },
                        {
                            additionalBoosterGuids[2], new NestedDictionary
                            {
                                {
                                    "Meta", new FlatDictionary
                                    {
                                        { "Guid", additionalBoosterGuids[2] },
                                        { "Tag", "Booster" },
                                        { "Deleted", false },
                                        { "Timestamp", Timestamps[1] }
                                    }
                                },
                                {
                                    "Delta", new FlatDictionary
                                    {
                                        {
                                            "Fields", new FlatDictionary
                                            {
                                                { "State", "Ready" }
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
                            }
                        }
                    }
                }
            };
        }

        return SampleData[sampleDataGuid];
    }
}