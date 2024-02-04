namespace Optimized.Collections;

using System.Collections;
using System.Collections.Concurrent;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

public class SieveLruCache<K, V>(int capacity) : IEnumerable<KeyValuePair<K, V>> where K : notnull
{
    class Node(K key, V value)
    {
        public Node? Next;
        public readonly K Key = key;
        public readonly V Value = value;
        public bool Visited;
    }

    private readonly ConcurrentDictionary<K, Node> _dictionary = [];
    private readonly ConcurrentDictionary<K, TaskCompletionSource<V>> _taskCompletionSources = [];
    private Node? head, tail, hand;

    private void Evict()
    {
        Node? prev = null;
        var node = hand ?? tail!;
        while (node.Visited)
        {
            node.Visited = false;
            prev = node;
            node = node.Next ?? tail!;
        }
        hand = node.Next;
        _dictionary.TryRemove(node.Key, out _);
        RemoveNode(node, prev);
    }

    private void RemoveNode(Node node, Node? prev)
    {
        if (node.Next is null)
            head = prev;
        if (prev is null)
            tail = node.Next;
        else
            prev.Next = node.Next;
    }

    private void AddToHead(Node node)
    {
        if (head is not null)
            head.Next = node;
        head = node;
        tail ??= node;
    }

    public async Task<V> GetAsync(K key, Func<K, Task<V>> factory)
    {
        if (_dictionary.TryGetValue(key, out var node))
        {
            node.Visited = true;
            return node.Value;
        }
        var myTcs = new TaskCompletionSource<V>();
        var tcs = _taskCompletionSources.GetOrAdd(key, myTcs);
        if (tcs != myTcs) return await tcs.Task;
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
                    if (_dictionary.Count == capacity) Evict();
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
