namespace Optimized.Collections;

using System.Diagnostics.CodeAnalysis;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

public class SieveLruCache<K, V>(int capacity) : ICache<K, V> where K : notnull
{
    class Node(K key, V value)
    {
        public Node Next = null!;
        public readonly K Key = key;
        public V Value = value;
        public bool Visited;
    }

    private readonly Dictionary<K, Node> _dictionary = [];
    private readonly ReaderWriterLockSlim _lock = new();
    private Node head = null!, hand = null!;

    private void Evict()
    {
        var prev = hand;
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
        _dictionary.Remove(node.Key);
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
        if (head == hand)
            hand = node;
        head = node;
    }

    public bool TryGetValue(K key, [MaybeNullWhen(false)] out V value)
    {
        _lock.EnterReadLock();
        try
        {
            if (_dictionary.TryGetValue(key, out var node))
            {
                node.Visited = true;
                value = node.Value;
                return true;
            }
            value = default;
            return false;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public void Set(K key, V value)
    {
        _lock.EnterWriteLock();
        try
        {
            if (_dictionary.TryGetValue(key, out var node))
            {
                node.Value = value;
            }
            else
            {
                node = new Node(key, value);
                AddToHead(node);
                _dictionary.Add(key, node);
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public int Count
    {
        get
        {
            _lock.EnterReadLock();
            var count = _dictionary.Count;
            _lock.ExitReadLock();
            return count;
        }
    }

    public IEnumerable<K> Keys
    {
        get
        {
            _lock.EnterReadLock();
            var keys = _dictionary.Keys.ToArray();
            _lock.ExitReadLock();
            return keys;
        }
    }
}