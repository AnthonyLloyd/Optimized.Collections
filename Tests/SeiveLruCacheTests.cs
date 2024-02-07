namespace Tests;

using System.Collections;
using CsCheck;
using Optimized.Collections;
using Xunit;

#pragma warning disable IDE0039 // Use local function
public class SieveLruCacheTests
{
    [Fact]
    public async Task ExampleEvictsTail()
    {
        var cache = new SieveLruCache<char, int>(3);
        var i = 0;
        var usedFactory = (char _) => Task.FromResult(i++);
        await cache.GetAsync('A', usedFactory);
        await cache.GetAsync('B', usedFactory);
        await cache.GetAsync('C', usedFactory);
        await cache.GetAsync('D', usedFactory);
        Assert.Equal(new Dictionary<char, int>{
            {'B', 1},
            {'C', 2},
            {'D', 3},
        }, cache.OrderBy(i => i.Key));
    }

    [Fact]
    public async Task ExampleEvictsHead()
    {
        var cache = new SieveLruCache<int, int>(4);
        var i = 0;
        var usedFactory = (int _) => Task.FromResult(i++);
        var notUsedFactory = Task<int> (int _) => throw new Exception();
        await cache.GetAsync(1, usedFactory);
        await cache.GetAsync(4, usedFactory);
        await cache.GetAsync(1, notUsedFactory);
        await cache.GetAsync(3, usedFactory);
        await cache.GetAsync(2, usedFactory);
        await cache.GetAsync(3, notUsedFactory);
        await cache.GetAsync(4, notUsedFactory);
        await cache.GetAsync(5, usedFactory);
        await cache.GetAsync(1, notUsedFactory);
        await cache.GetAsync(2, usedFactory);
        Assert.Equal(new Dictionary<int, int>{
            {1, 0},
            {2, 5},
            {3, 2},
            {5, 4},
        }, cache.OrderBy(i => i.Key));
    }

    [Fact]
    public async Task ExampleBlog()
    {
        var cache = new SieveLruCache<char, int>(7);
        var i = 0;
        var usedFactory = (char _) => Task.FromResult(i++);
        var notUsedFactory = Task<int> (char _) => throw new Exception();
        // set up initial state
        await cache.GetAsync('A', usedFactory);
        await cache.GetAsync('B', usedFactory);
        await cache.GetAsync('C', usedFactory);
        await cache.GetAsync('D', usedFactory);
        await cache.GetAsync('B', notUsedFactory);
        await cache.GetAsync('E', usedFactory);
        await cache.GetAsync('F', usedFactory);
        await cache.GetAsync('G', usedFactory);
        await cache.GetAsync('A', notUsedFactory);
        await cache.GetAsync('G', notUsedFactory);
        // requests
        await cache.GetAsync('H', usedFactory);
        await cache.GetAsync('A', notUsedFactory);
        await cache.GetAsync('D', notUsedFactory);
        await cache.GetAsync('I', usedFactory);
        await cache.GetAsync('B', notUsedFactory);
        await cache.GetAsync('J', usedFactory);
        Assert.Equal(new Dictionary<char, int>{
            {'A', 0},
            {'B', 1},
            {'D', 3},
            {'G', 6},
            {'H', 7},
            {'I', 8},
            {'J', 9},
        }, cache.OrderBy(i => i.Key));
    }

    [Fact]
    public void SampleModelBased()
    {
        Check.SampleModelBased(
            Gen.Const(() => (new SieveLruCache<int, int>(4), new SieveModel<int, int>(4))),
            Gen.Int[1, 5].Operation<SieveLruCache<int, int>, SieveModel<int, int>>((a, m, i) =>
            {
                a.GetAsync(i, i => Task.FromResult(i)).Wait();
                m.GetAsync(i, i => Task.FromResult(i)).Wait();
            })
        );
    }

    [Fact]
    public void SampleConcurrent()
    {
        Check.SampleConcurrent(
            Gen.Const(() => new SieveLruCache<int, int>(4)),
            Gen.Int[1, 5].Operation<SieveLruCache<int, int>>((d, i) => d.GetAsync(i, i => Task.FromResult(i)).Wait())
        );
    }
}

public class SieveModel<K, V>(int capacity) : IEnumerable<KeyValuePair<K, V>> where K : notnull
{
    class Node(K key, V value)
    {
        public Node? Next, Prev;
        public readonly K Key = key;
        public readonly V Value = value;
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

    public async Task<V> GetAsync(K key, Func<K, Task<V>> factory)
    {
        if (_dictionary.TryGetValue(key, out var node))
        {
            node.Visited = true;
            return node.Value;
        }
        var value = await factory(key);
        if (_dictionary.Count == capacity) Evict();
        node = new Node(key, value);
        AddToHead(node);
        _dictionary.Add(key, node);
        return value;
    }

    public IEnumerator<KeyValuePair<K, V>> GetEnumerator() => _dictionary.Select(kv => KeyValuePair.Create(kv.Key, kv.Value.Value)).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}