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
        public bool Visited;
    }

    private readonly ConcurrentDictionary<K, Node> _dictionary = [];
    private readonly ConcurrentDictionary<K, TaskCompletionSource<V>> _taskCompletionSources = [];
    private Node head = null!;
    private Node? hand;
    private volatile TaskCompletionSource<V>? _spareTcs;

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
            head = node.Next;
        _dictionary.TryRemove(node.Key, out _);
    }

    private void AddToHead(Node node)
    {
        var count = _dictionary.Count;
        if (count > 0)
        {
            if (count == capacity) Evict();
            node.Next = head.Next;
            head.Next = node;
            head = node;
        }
        else
        {
            node.Next = node;
            head = node;
        }
    }

    public async Task<V> GetAsync(K key, Func<K, Task<V>> factory)
    {
        if (_dictionary.TryGetValue(key, out var node))
        {
            node.Visited = true;
            return node.Value;
        }
        var myTcs = Interlocked.Exchange(ref _spareTcs, null) ?? new();
        var tcs = _taskCompletionSources.GetOrAdd(key, myTcs);
        if (tcs != myTcs)
        {
            _spareTcs = myTcs;
            return await tcs.Task;
        }
        try
        {
            V value;
            if (_dictionary.TryGetValue(key, out node))
            {
                value = node.Value;
                node.Visited = true;
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
            myTcs.SetResult(value);
            return value;
        }
        catch (Exception ex)
        {
            myTcs.SetException(ex);
            throw;
        }
        finally
        {
            _taskCompletionSources.TryRemove(key, out _);
        }
    }

    public IEnumerator<KeyValuePair<K, V>> GetEnumerator() => _dictionary.Select(kv => KeyValuePair.Create(kv.Key, kv.Value.Value)).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public int Count => _dictionary.Count;
}
