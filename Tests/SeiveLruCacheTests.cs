namespace Tests;

using CsCheck;
using System.Collections.Concurrent;
using Optimized.Collections;
using Xunit;

public class SieveLruCacheTests
{
    [Fact]
    public async Task SieveExample()
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

        (char, int)[] expected = [
            ('A', 0),
            ('B', 1),
            ('D', 3),
            ('G', 6),
            ('H', 7),
            ('I', 8),
            ('J', 9),
        ];

        Assert.Equal(expected, cache.Select(kv => (kv.Key, kv.Value)).Order(), EqualityComparer<(char, int)>.Default);
    }

    [Fact]
    public void TestConcurrent()
    {
        Check.SampleConcurrent(
            Gen.Const(() => new SieveLruCache<int, int>(4)),
            Gen.Int[1, 5].Operation<SieveLruCache<int, int>>((d, i) => d.GetAsync(i, j => Task.FromResult(j)).Wait()),
            equal: (d1, d2) => d1.Count == d2.Count
            );
    }
}
