namespace Tests;

using CsCheck;
using Optimized.Collections;
using Xunit;

#pragma warning disable IDE0039 // Use local function
public class SieveLruCacheTests
{
    [Fact]
    public async Task EvictsFirstItem()
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
    public async Task BlogExample()
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
    public void SampleConcurrent()
    {
        //for (int i = 0; i < 10; i++)
            Check.SampleConcurrent(
                Gen.Const(() => new SieveLruCache<int, int>(4)),
                Gen.Int[1, 5].Operation<SieveLruCache<int, int>>((d, i) => d.GetAsync(i, _ => Task.FromResult(i)).Wait()),
            time: 10);
    }
}
