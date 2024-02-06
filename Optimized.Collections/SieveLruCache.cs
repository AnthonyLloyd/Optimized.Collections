namespace Optimized.Collections;

using System.Collections;
using System.Collections.Concurrent;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

public class SieveLruCache<K, V>(int capacity) : IEnumerable<KeyValuePair<K, V>> where K : notnull
{
    class Node(K key, V value)
    {
        public Node Next = null!;
        public readonly K Key = key;
        public readonly V Value = value;
        public volatile bool Visited;
    }

    private readonly ConcurrentDictionary<K, Node> _dictionary = [];
    private readonly Dictionary<K, TaskCompletionSource<Node>> _taskCompletionSources = [];
    private Node head = null!;
    private Node? hand;
    private volatile TaskCompletionSource<Node>? _spareTcs;

    private void Evict()
    {
        var prev = hand ?? head;
        var node = prev.Next;
        while (node.Visited)
        {
            node.Visited = false;
            prev = node;
            node = node.Next;
        }
        prev.Next = node.Next;
        hand = prev;
        if (head == node)
            head = prev;
        _dictionary.TryRemove(node.Key, out _);
    }

    private void AddToHead(Node node)
    {
        var count = _dictionary.Count;
        if (count > 1)
        {
            if (count == capacity) Evict();
            node.Next = head.Next;
            head.Next = node;
        }
        else if (count == 1)
        {
            node.Next = head;
            head.Next = node;
        }
        else
        {
            node.Next = node;
        }
        if (head == hand)
            hand = null;
        head = node;
    }

    public async Task<V> GetAsync(K key, Func<K, Task<V>> factory)
    {
        if (_dictionary.TryGetValue(key, out var node))
        {
            node.Visited = true;
            return node.Value;
        }
        var myTcs = Interlocked.Exchange(ref _spareTcs, null) ?? new();
        TaskCompletionSource<Node> tcs;
        lock (_taskCompletionSources)
            tcs = _taskCompletionSources.TryAdd(key, myTcs) ? myTcs : _taskCompletionSources[key];
        if (tcs != myTcs)
        {
            _spareTcs = myTcs;
            node = await tcs.Task;
            node.Visited = true;
            return node.Value;
        }
        try
        {
            V value;
            if (_dictionary.TryGetValue(key, out node))
            {
                node.Visited = true;
                value = node.Value;
            }
            else
            {
                value = await factory(key);
                node = new Node(key, value);
                lock (_dictionary)
                {
                    AddToHead(node);
                    _dictionary[key] = node;
                }
            }
            myTcs.SetResult(node);
            return value;
        }
        catch (Exception ex)
        {
            myTcs.SetException(ex);
            throw;
        }
        finally
        {
            lock (_taskCompletionSources)
                _taskCompletionSources.Remove(key);
        }
    }

    public IEnumerator<KeyValuePair<K, V>> GetEnumerator()
    {
        if (_dictionary.Count > 1)
        {
            var loopKeys = new HashSet<K>();
            var node = head.Next;
            while (true)
            {
                if (!_dictionary.ContainsKey(node.Key))
                    throw new Exception($"Dictionary does not contain {node.Key}");
                loopKeys.Add(node.Key);
                if (node == head) break;
                node = node.Next;
            }
            if (loopKeys.Count != _dictionary.Count)
                throw new Exception($"Counts differ {loopKeys.Count} {_dictionary.Count}");
        }
        return _dictionary.Select(kv => KeyValuePair.Create(kv.Key, kv.Value.Value)).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public int Count => _dictionary.Count;
}