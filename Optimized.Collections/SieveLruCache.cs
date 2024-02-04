namespace Optimized.Collections;

using System.Collections;
using System.Collections.Concurrent;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

public class SieveLruCache<K, V>(int capacity) : IEnumerable<KeyValuePair<K, V>> where K : notnull
{
    class Node(K key, V value)
    {
        public Node? Prev;
        public Node? Next;
        public readonly K Key = key;
        public readonly V Value = value;
        public bool Visited;
    }

    private readonly ConcurrentDictionary<K, Node> _dictionary = [];
    private readonly ConcurrentDictionary<K, TaskCompletionSource<V>> _taskCompletionSources = [];
    private Node? head;
    private Node? tail;
    private Node? hand;

    private void Evict()
    {
        var node = hand ?? tail;
        while (node is not null && node.Visited)
        {
            node.Visited = false;
            node = node.Prev ?? tail;
        }
        hand = node?.Prev;
        if (node is not null)
        {
            _dictionary.TryRemove(node.Key, out _);
            RemoveNode(node);
        }
    }

    private void AddToHead(Node node)
    {
        node.Next = head;
        if (head is not null)
            head.Prev = node;
        head = node;
        tail ??= node;
    }

    private void RemoveNode(Node node)
    {
        if (node.Prev is not null)
            node.Prev.Next = node.Next;
        else
            head = node.Next;
        if (node.Next is not null)
            node.Next.Prev = node.Prev;
        else
            tail = node.Prev;
    }

    public async Task<V> GetAsync(K key, Func<K, Task<V>> factory)
    {
        if (_dictionary.TryGetValue(key, out var node))
        {
            node.Visited = true;
            return node.Value;
        }
        var myTCS = new TaskCompletionSource<V>();
        var tcs = _taskCompletionSources.GetOrAdd(key, myTCS);
        if (tcs != myTCS) return await tcs.Task;
        try
        {
            if (_dictionary.TryGetValue(key, out node))
            {
                node.Visited = true;
                return node.Value;
            }
            var value = await factory(key);
            node = new Node(key, value);
            lock (_dictionary)
            {
                if (_dictionary.Count == capacity) Evict();
                AddToHead(node);
                _dictionary[key] = node;
            }
            tcs.SetResult(value);
            return value;
        }
        catch (Exception ex)
        {
            tcs.SetException(ex);
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
