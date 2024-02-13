namespace Tests;

using System.Diagnostics.CodeAnalysis;
using CsCheck;
using Optimized.Collections;
using Xunit;

#pragma warning disable IDE0039 // Use local function

public class SieveLruCacheTests
{
    [Fact]
    public void ExampleEvictsTail()
    {
        var cache = new SieveLruCache<char, int>(3);
        var i = 0;
        var usedFactory = (char _) => i++;
        cache.GetOrAdd('A', usedFactory);
        cache.GetOrAdd('B', usedFactory);
        cache.GetOrAdd('C', usedFactory);
        cache.GetOrAdd('D', usedFactory);
        Assert.Equal(new Dictionary<char, int>{
            {'B', 1},
            {'C', 2},
            {'D', 3},
        }, cache.Keys.ToDictionary(k => k, k => cache.TryGetValue(k, out var v) ? v : 999));
    }

    [Fact]
    public void ExampleEvictsHead()
    {
        var cache = new SieveLruCache<int, int>(4);
        var i = 0;
        var usedFactory = (int _) => i++;
        var notUsedFactory = int (int _) => throw new Exception();
        cache.GetOrAdd(1, usedFactory);
        cache.GetOrAdd(4, usedFactory);
        cache.GetOrAdd(1, notUsedFactory);
        cache.GetOrAdd(3, usedFactory);
        cache.GetOrAdd(2, usedFactory);
        cache.GetOrAdd(3, notUsedFactory);
        cache.GetOrAdd(4, notUsedFactory);
        cache.GetOrAdd(5, usedFactory);
        cache.GetOrAdd(1, notUsedFactory);
        cache.GetOrAdd(2, usedFactory);
        Assert.Equal(new Dictionary<int, int>{
            {1, 0},
            {2, 5},
            {3, 2},
            {5, 4},
        }, cache.Keys.ToDictionary(k => k, k => cache.TryGetValue(k, out var v) ? v : 999));
    }

    [Fact]
    public void ExampleBlog()
    {
        var cache = new SieveLruCache<char, int>(7);
        var i = 0;
        var usedFactory = (char _) => i++;
        var notUsedFactory = int (char _) => throw new Exception();
        // set up initial state
        cache.GetOrAdd('A', usedFactory);
        cache.GetOrAdd('B', usedFactory);
        cache.GetOrAdd('C', usedFactory);
        cache.GetOrAdd('D', usedFactory);
        cache.GetOrAdd('B', notUsedFactory);
        cache.GetOrAdd('E', usedFactory);
        cache.GetOrAdd('F', usedFactory);
        cache.GetOrAdd('G', usedFactory);
        cache.GetOrAdd('A', notUsedFactory);
        cache.GetOrAdd('G', notUsedFactory);
        // requests
        cache.GetOrAdd('H', usedFactory);
        cache.GetOrAdd('A', notUsedFactory);
        cache.GetOrAdd('D', notUsedFactory);
        cache.GetOrAdd('I', usedFactory);
        cache.GetOrAdd('B', notUsedFactory);
        cache.GetOrAdd('J', usedFactory);
        Assert.Equal(new Dictionary<char, int>{
            {'A', 0},
            {'B', 1},
            {'D', 3},
            {'G', 6},
            {'H', 7},
            {'I', 8},
            {'J', 9},
        }, cache.Keys.ToDictionary(k => k, k => cache.TryGetValue(k, out var v) ? v : 999));
    }

    [Fact]
    public void SampleModelBased()
    {
        Check.SampleModelBased(
            Gen.Const(() => (new SieveLruCache<int, int>(4), new SieveModel<int, int>(4))),
            Gen.Int[1, 5].Operation<SieveLruCache<int, int>, SieveModel<int, int>>((a, m, i) =>
            {
                a.GetOrAdd(i, i => i);
                m.GetOrAdd(i, i => i);
            }),
            equal: (a, m) => Check.Equal(a.Keys.ToHashSet(), m.Keys.ToHashSet()),
            printActual: a => Check.Print(a.Keys),
            printModel: m => Check.Print(m.Keys)
        );
    }

    [Fact]
    public void SampleConcurrent()
    {
        Check.SampleConcurrent(
            Gen.Const(() => new SieveLruCache<int, int>(4)),
            Gen.Int[1, 5].Operation<SieveLruCache<int, int>>((d, i) => d.GetOrAdd(i, i => i)),
            equal: (a, b) => Check.Equal(a.Keys, b.Keys),
            print: a => Check.Print(a.Keys)
        );
    }
}

internal static class SieveLruCacheExtensions
{
    public static V GetOrAdd<K, V>(this ICache<K, V> cache, K key, Func<K, V> factory) where K : notnull
    {
        if (!cache.TryGetValue(key, out var value))
        {
            value = factory(key);
            cache.Set(key, value);
        }
        return value;
    }
}

public class SieveModel<K, V>(int capacity) : ICache<K, V> where K : notnull
{
    class Node(K key, V value)
    {
        public Node? Next, Prev;
        public readonly K Key = key;
        public V Value = value;
        public volatile bool Visited;
    }

    private readonly Dictionary<K, Node> _dictionary = [];
    private Node? head, hand, tail;

    private void Evict()
    {
        var node = hand ?? tail;
        while (node!.Visited)
        {
            node.Visited = false;
            node = node.Prev ?? tail;
        }
        hand = node.Prev;
        _dictionary.Remove(node.Key);
        RemoveNode(node);
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

    private void AddToHead(Node node)
    {
        node.Next = head;
        if (head is not null)
            head.Prev = node;
        head = node;
        tail ??= node;
    }

    public void Set(K key, V value)
    {
        if (_dictionary.TryGetValue(key, out var node))
        {
            node.Value = value;
        }
        else
        {
            node = new Node(key, value);
            if (_dictionary.Count == capacity) Evict();
            AddToHead(node);
            _dictionary.Add(key, node);
        }
    }

    public bool TryGetValue(K key, [MaybeNullWhen(false)] out V value)
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

    public int Count => _dictionary.Count;
    public IEnumerable<K> Keys => _dictionary.Keys;
}