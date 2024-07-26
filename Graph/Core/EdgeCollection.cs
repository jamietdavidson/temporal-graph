namespace Graph.Core;

#pragma warning disable CS1998
public class EdgeCollection<FromNode, ToNode> : IGraphObjectWithCursor where FromNode : Node where ToNode : Node
{
    private readonly Dictionary<Guid, Guid?> _deltaAdded = new();
    private readonly HashSet<Guid> _deltaRemoved = new();
    private readonly FromNode _fromNode;

    private readonly List<Guid> _lazyLoadQueue = new();

    private LinkedList<Guid> _data;
    private Guid? _first;
    private bool _hasDelta;
    private Guid? _last;
    private Dictionary<Guid, ToNode> _nodeGuids = new();

    public EdgeCollection(FromNode fromNode, string toNodeTag, List<ToNode>? toNodes)
    {
        _fromNode = fromNode;
        Tag = toNodeTag;
        _SetNodes(toNodes);
    }

    public string Tag { get; init; }

    public int Count
    {
        get
        {
            if (_hasDelta)
                return (_data?.Count ?? 0) + _deltaAdded.Count - _deltaRemoved.Count;

            return _data?.Count ?? 0;
        }
    }

    public event EventHandler<NodeEventArgs>? ValueChanged;

    public Cursor? Cursor
    {
        get => _fromNode.Cursor;
        set
        {
            foreach (Node node in _nodeGuids.Values)
                if (node != null)
                    node.Cursor = value;
        }
    }

    public void SetInitialValue(object value)
    {
        if (value is List<ToNode>)
        {
            _SetNodes((List<ToNode>)value);
        }
        else if (value is Dictionary<Guid, Guid?> || value is List<Guid>)
        {
            _data = Utils.AsLinkedList(value);
            _SetFirstAndLastFromData();
        }

        _hasDelta = false;
    }

    public async Task SetValueAsync(object? value)
    {
        _hasDelta = true;
        if (!Node.SchemalessEnabled)
            throw new NotImplementedException();

        if (value == null)
            throw new ArgumentException("Cannot set edge collection field to null. Use EdgeCollection.Clear() instead.");
    }

    public async Task<object?> GetValueAsync(ModeEnum mode, bool silent = false)
    {
        return this;
    }

    public async Task DeltaToDataAsync()
    {
        if (!_hasDelta) return;

        _MergeDeltaInto(ref _data);

        await ClearDeltaAsync();
    }

    public async Task ClearDeltaAsync()
    {
        foreach (var guid in _deltaRemoved)
            _nodeGuids.Remove(guid);

        _deltaRemoved.Clear();
        _deltaAdded.Clear();
        _hasDelta = false;
        _first = _data?.First?.Value;
        _last = _data?.Last?.Value;
    }

    public async Task<List<ToNode>> GetNodesAsync(List<Guid> nodeGuids, ModeEnum mode = ModeEnum.Data,
        bool silent = false)
    {
        if (mode == ModeEnum.Data)
        {
            if (_hasDelta && !silent) throw new InvalidOperationException("Requested data when delta was set.");

            return await _GetNodeSubset(nodeGuids.Where(g => _data.Contains(g)).ToList());
        }

        if (!_hasDelta) return new List<ToNode>();

        return await _GetNodeSubset(nodeGuids.Where(g => !_data.Contains(g)).ToList());
    }

    public async Task<List<ToNode>> GetAllNodesAsync(ModeEnum mode, bool silent = false)
    {
        return await GetNodesAsync(_nodeGuids.Keys.ToList(), mode, silent);
    }

    // Gets all data and delta nodes
    public async Task<List<ToNode>> GetAllNodesAsync()
    {
        await _GetNodeReferences();
        return _nodeGuids.Select(n => n.Value).ToList();
    }

    private async Task<List<ToNode>> _GetNodeSubset(List<Guid> nodeGuids)
    {
        await _GetNodeReferences(nodeGuids);
        return _nodeGuids.Select(n => n.Value).Where(n => nodeGuids.Contains(n.Guid)).ToList();
    }

    public async Task<ToNode?> GetNodeAtIndexAsync(int index)
    {
        var guid = GetGuidAtIndex(index);
        if (guid != null)
        {
            var nodeGuid = (Guid)guid;
            await _GetNodeReferences(new List<Guid> { nodeGuid });

            if (_nodeGuids.ContainsKey(nodeGuid))
                return _nodeGuids[nodeGuid];
        }

        return null;
    }

    public Guid? GetGuidAtIndex(int index)
    {
        var guids = GetGuidsForRange(index, 1);

        if (guids.Any())
            return guids[0];

        return null;
    }

    public Guid? GetNext(Guid? afterGuid)
    {
        if (afterGuid == null) return null;

        return _GetLinkedListItem(_data, (Guid)afterGuid).Next?.Value;
    }

    public Guid? GetPrevious(Guid? beforeGuid)
    {
        if (beforeGuid == null) return null;

        return _GetLinkedListItem(_data, (Guid)beforeGuid).Previous?.Value;
    }

    public async Task<List<ToNode>> GetRange(Guid? after, int count)
    {
        return await GetNodesAsync(_GetGuidsForRange(_data, after, count));
    }

    public async Task<List<ToNode>> GetRange(int startIndex, int count)
    {
        return await GetNodesAsync(GetGuidsForRange(startIndex, count));
    }

    public List<Guid> GetGuidsForRange(Guid? after, int count, ModeEnum mode = ModeEnum.Data)
    {
        var fromList = mode == ModeEnum.Data ? _data : _MockDeltaToData();
        return _GetGuidsForRange(fromList, after, count);
    }

    public List<Guid> GetGuidsForRange(int startIndex, int count, ModeEnum mode = ModeEnum.Data)
    {
        var fromList = mode == ModeEnum.Data ? _data : _MockDeltaToData();
        return _GetGuidsForRange(fromList, startIndex, count);
    }

    private List<Guid> _GetGuidsForRange(LinkedList<Guid> fromList, int startIndex, int count)
    {
        if (count < 1) return new List<Guid>();

        if (startIndex < 0)
            throw new ArgumentException("Invalid index requested from edge collection.");

        var guids = new List<Guid>();
        var i = 0;
        var endIndex = startIndex + count;

        for (var item = fromList.First; item != null; item = item.Next)
        {
            if (i > endIndex || guids.Count == count) break;
            if (i++ < startIndex) continue;

            guids.Add(item.Value);
        }

        return guids;
    }

    private List<Guid> _GetGuidsForRange(LinkedList<Guid> fromList, Guid? after, int count)
    {
        if (after == null) return _GetGuidsForRange(fromList, 0, count);

        if (count < 1) return new List<Guid>();

        var afterItemInList = _GetLinkedListItem(fromList, (Guid)after);

        var guids = new List<Guid>();
        for (var item = afterItemInList!.Next; item != null; item = item.Next)
        {
            if (guids.Count == count) break;

            guids.Add(item.Value);
        }

        return guids;
    }

    public async Task<ToNode?> GetNodeAsync(Guid nodeGuid)
    {
        return _nodeGuids[nodeGuid];
    }

    public object AsDictionary(ModeEnum mode = ModeEnum.Data, bool silent = false)
    {
        if (mode == ModeEnum.Data)
        {
            if (_hasDelta && !silent) throw new InvalidOperationException("Requested data when delta was set.");

            var dict = new Dictionary<Guid, Guid?>();
            if (_data != null)
            {
                for (var item = _data.First; item != null; item = item.Next)
                    dict[item.Value] = item.Next?.Value;
            }

            return dict;
        }

        if (!_hasDelta) return new Ignorable();

        var deltaDict = new FlatDictionary
        {
            { "Added", new Dictionary<Guid, Guid?>(_deltaAdded) },
            { "Removed", _deltaRemoved.ToList().Distinct() }
        };
        return deltaDict;
    }

    private LinkedList<Guid> _MockDeltaToData()
    {
        if (!_hasDelta) return _data;

        var dataCopy = _data != null ? new LinkedList<Guid>(_data) : new LinkedList<Guid>();
        _MergeDeltaInto(ref dataCopy);

        return dataCopy;
    }

    private void _MergeDeltaInto(ref LinkedList<Guid> list)
    {
        list ??= new LinkedList<Guid>();

        foreach (var guid in _deltaRemoved)
            list.Remove(guid);

        var todo = new Dictionary<Guid, Guid>();
        foreach (var item in _deltaAdded)
            if (item.Value != null)
            {
                var next = list.Find((Guid)item.Value);
                if (next != null)
                    list.AddBefore(next, item.Key);
                else
                    todo.Add(item.Key, (Guid)item.Value);
            }
            else
            {
                list.AddLast(item.Key);
            }

        var removed = new List<Guid>();
        while (todo.Count > 0)
        {
            removed.Clear();

            if (list.Count > 0)
            {
                foreach (var item in todo)
                {
                    var next = list.Find(item.Value);
                    if (next != null)
                    {
                        list.AddBefore(next, item.Key);
                        removed.Add(item.Key);
                    }
                }

                foreach (var key in removed)
                    todo.Remove(key);
            }
            else
            {
                // The list was empty,
                // so simply add all of the todo items in order.
                foreach (var item in todo)
                {
                    list.AddLast(item.Key);
                    todo.Remove(item.Key);
                }
            }

        }
    }

    public void Append(ToNode node)
    {
        Append(new List<ToNode> { node });
    }

    public void Append(List<ToNode> nodes)
    {
        AddAtIndex(nodes, Count);
    }

    public void Prepend(ToNode node)
    {
        Prepend(new List<ToNode> { node });
    }

    public void Prepend(List<ToNode> nodes)
    {
        AddAtIndex(nodes, 0);
    }

    public void AddAtIndex(ToNode node, int index)
    {
        AddAtIndex(new List<ToNode> { node }, index);
    }

    public void AddAtIndex(List<ToNode> nodes, int index)
    {
        if (nodes.Where(n => n.Tag != Tag).Any())
            throw new ArgumentException("Edge collections cannot include more than one variety of node tag.");

        if (nodes.Count == 0) return;

        var cursor = Cursor;
        if (cursor == null)
            throw new InvalidOperationException("Cannot add nodes to cursorless edge collection.");

        var (prevBookend, nextBookend) = _GetBookends(index);
        var prev = prevBookend;

        if (prevBookend != null)
        {
            var pb = (Guid)prevBookend;
            if (_data?.Contains(pb) ?? false)
                _deltaRemoved.Add(pb);

            if (_deltaAdded.ContainsKey(pb))
                _deltaAdded.Remove(pb); // will be added back with new next
        }
        else // prepending
        {
            _first = nodes[0].Guid;
        }

        Node currentNode;
        for (var i = 0; i < nodes.Count; i++)
        {
            currentNode = nodes[i];
            if (prev != null) _deltaAdded.Add((Guid)prev, currentNode.Guid);
            _RegisterNode(currentNode);
            prev = currentNode.Guid;
        }

        if (!_deltaAdded.ContainsKey((Guid)prev!))
            _deltaAdded.Add((Guid)prev!, nextBookend);

        if (nextBookend == null) // appending
            _last = prev;

        _hasDelta = true;
    }

    public void MoveAfter(ToNode node, ToNode afterMe)
    {
        // Get what's next afterMe, then remove afterMe.
        Guid? next;
        if (_deltaAdded.ContainsKey(afterMe.Guid))
        {
            next = _deltaAdded[afterMe.Guid];

            if (next == node.Guid) return; // no action required

            _deltaAdded.Remove(afterMe.Guid);
        }
        else
        {
            var afterMeInData = _GetLinkedListItem(_data, afterMe.Guid);

            next = afterMeInData.Next?.Value;

            if (next == node.Guid) return; // no action required

            _deltaRemoved.Add(afterMe.Guid);
        }

        _PrepareToMove(node);

        // Include node afterMe.
        _deltaAdded.Add(afterMe.Guid, node.Guid);
        _deltaAdded.Add(node.Guid, next);
        if (next == null) _last = node.Guid;

        _hasDelta = true;
    }

    public void MoveToFirst(ToNode node)
    {
        if (_first == node.Guid) return; // no action required

        _PrepareToMove(node);
        _deltaAdded[node.Guid] = _first;
        _first = node.Guid;

        _hasDelta = true;
    }

    private void _PrepareToMove(ToNode node)
    {
        // Get the values that surround the node.
        var inList = _GetLinkedListItem(_MockDeltaToData(), node.Guid);

        var next = inList.Next?.Value;
        var previous = inList.Previous?.Value;

        // Remove the node.
        if (_deltaAdded.ContainsKey(node.Guid))
            _deltaAdded.Remove(node.Guid);
        else
            _deltaRemoved.Add(node.Guid);

        // If there was a previous (relative to the node, before moving),
        // point previous to next.
        if (previous != null)
        {
            var prev = (Guid)previous;
            if (!_deltaAdded.ContainsKey(prev) && _data.Contains(prev))
                _deltaRemoved.Add(prev);

            _deltaAdded[prev] = next;
            if (next == null) _last = prev;
        }
    }

    public void Remove(Guid nodeGuid)
    {
        Remove(new List<Guid> { nodeGuid });
    }

    public void Remove(List<Guid> nodeGuids)
    {
        foreach (var guid in nodeGuids)
            if (!_deltaAdded.ContainsKey(guid))
            {
                _deltaRemoved.Add(guid);
            }
            else
            {
                var prevInAdded = _deltaAdded.FirstOrDefault(item => item.Value == guid).Key;
                var needToBridge = true;
                if (_deltaAdded.ContainsKey(prevInAdded) && _deltaAdded[prevInAdded] == guid)
                {
                    if (_deltaAdded[guid] == null)
                    {
                        // If removing the new _last and prevInAdded is last in _data,
                        // the pointer modification is no longer needed,
                        // and there is no need to bridge.
                        var prevInData = _data.Find(prevInAdded);
                        if (prevInData != null && prevInData == _data.Last)
                        {
                            _deltaAdded.Remove(prevInAdded);
                            _deltaRemoved.Remove(prevInAdded);
                            needToBridge = false;
                        }
                    }

                    if (needToBridge) // bridge the gap
                        _deltaAdded[prevInAdded] = _deltaAdded[guid];
                }

                _deltaAdded.Remove(guid);
            }

        _hasDelta = _deltaAdded.Count > 0 || _deltaRemoved.Count > 0;
    }

    public void Clear()
    {
        _deltaAdded.Clear();

        foreach (var node in _data)
            _deltaRemoved.Add(node);

        _hasDelta = true;
    }

    public bool Contains(Guid nodeGuid)
    {
        if (_deltaAdded.ContainsKey(nodeGuid)) return true;

        return !_deltaRemoved.Contains(nodeGuid) && _data.Contains(nodeGuid);
    }

    private void _SetNodes(List<ToNode>? toNodes)
    {
        if (toNodes != null)
        {
            if (toNodes.Where(n => n.Tag != Tag).Any())
                throw new ArgumentException("Edge collections cannot include more than one variety of node tag.");

            foreach (var node in toNodes) _nodeGuids[node.Guid] = node;
            _data = new LinkedList<Guid>(toNodes.Select(n => n.Guid));
            _SetFirstAndLastFromData();
        }
        else
        {
            _nodeGuids = new Dictionary<Guid, ToNode>();
        }
    }

    private void _SetFirstAndLastFromData()
    {
        _first = _data.First?.Value;
        _last = _data.Last?.Value;
    }

    private void _RegisterNode(Node node)
    {
        _nodeGuids[node.Guid] = (ToNode)node;
        node.Cursor = Cursor;
    }

    private async Task _GetNodeReferences(List<Guid>? nodeGuids = null)
    {
        var cursor = Cursor;
        if (cursor == null || _data == null || _nodeGuids.Count == _data.Count) return;

        if (nodeGuids == null) // get references for all nodes in the collection
        {
            for (var item = _data.First; item != null; item = item.Next)
                _GetReferenceOrQueueLazyLoad(cursor, item.Value);

            foreach (var guid in _deltaAdded.Keys)
                _GetReferenceOrQueueLazyLoad(cursor, guid);
        }
        else
        {
            foreach (var guid in nodeGuids)
                _GetReferenceOrQueueLazyLoad(cursor, guid);
        }

        if (_lazyLoadQueue.Any())
            foreach (var node in await cursor.LazyLoad(_lazyLoadQueue))
                _nodeGuids[node.Guid] = (ToNode)node;

        _lazyLoadQueue.Clear();
    }

    private void _GetReferenceOrQueueLazyLoad(Cursor cursor, Guid guid)
    {
        if (_nodeGuids.ContainsKey(guid)) return;

        if (cursor.NodeGuids.ContainsKey(guid))
            _nodeGuids[guid] = (ToNode)cursor.NodeGuids[guid];
        else
            _lazyLoadQueue.Add(guid);
    }

    private (Guid?, Guid?) _GetBookends(int index)
    {
        if (index < 0 || index > Count)
            throw new IndexOutOfRangeException("Cannot add to edge collection at index: " + index);

        if (index == 0) return (null, _first);

        if (index == Count && !_hasDelta) return (_last, null);

        var bookends = GetGuidsForRange(--index, 2, _hasDelta ? ModeEnum.Delta : ModeEnum.Data);
        return (
            bookends.Count > 0 ? bookends[0] : null,
            bookends.Count > 1 ? bookends[1] : null
        );
    }

    private LinkedListNode<Guid> _GetLinkedListItem(LinkedList<Guid> linkedList, Guid guid)
    {
        var itemInList = linkedList.Find(guid);

        if (itemInList == null)
            throw new ArgumentException($"Guid not found in edge collection: '{guid}'");

        return itemInList;
    }
}