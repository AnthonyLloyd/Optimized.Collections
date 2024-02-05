namespace Optimized.Collections;

using System.Collections;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

public class SieveLruCache<K, V>(int capacity) : IEnumerable<KeyValuePair<K, V>> where K : notnull
{
    class Node(K key, V value)
    {
        public Node Next = default!;
        public readonly K Key = key;
        public readonly V Value = value;
        public bool Visited;
    }

    private readonly Dictionary<K, Node> _dictionary = [];
    private readonly ReaderWriterLockSlim _dictionaryLock = new();
    private readonly Dictionary<K, TaskCompletionSource<V>> _taskCompletionSources = [];
    private Node head = default!;
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
        _dictionary.Remove(node.Key);
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
        _dictionaryLock.EnterReadLock();
        if (_dictionary.TryGetValue(key, out var node))
        {
            _dictionaryLock.ExitReadLock();
            node.Visited = true;
            return node.Value;
        }
        _dictionaryLock.ExitReadLock();
        var myTcs = Interlocked.Exchange(ref _spareTcs, null) ?? new();
        TaskCompletionSource<V>? tcs;
        lock (_taskCompletionSources)
        {
            tcs = _taskCompletionSources.TryAdd(key, myTcs) ? myTcs : _taskCompletionSources[key];
        }
        if (tcs != myTcs)
        {
            _spareTcs = myTcs;
            return await tcs.Task;
        }
        try
        {
            V value;
            _dictionaryLock.EnterReadLock();
            if (_dictionary.TryGetValue(key, out node))
            {
                _dictionaryLock.ExitReadLock();
                value = node!.Value;
                node.Visited = true;
            }
            else
            {
                _dictionaryLock.ExitReadLock();
                value = await factory(key);
                node = new Node(key, value);
                _dictionaryLock.EnterWriteLock();
                AddToHead(node);
                _dictionary[key] = node;
                _dictionaryLock.ExitWriteLock();
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
            lock (_taskCompletionSources)
            {
                _taskCompletionSources.Remove(key);
            }
        }
    }

    public IEnumerator<KeyValuePair<K, V>> GetEnumerator() => _dictionary.Select(kv => KeyValuePair.Create(kv.Key, kv.Value.Value)).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public int Count => _dictionary.Count;
}