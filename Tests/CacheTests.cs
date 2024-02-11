namespace Tests;

using Xunit;
using CsCheck;
using Optimized.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;

public class CacheTests
{
    class ConcurrentDictionaryCache<K, V> : ConcurrentDictionary<K, V>, ICache<K, V> where K : notnull
    {
        IEnumerable<K> ICache<K, V>.Keys => Keys;
        public void Set(K key, V value) => this[key] = value;
    }

    [Fact]
    public void GetOrAddAtomicAsync_SampleConcurrent()
    {
        Check.SampleConcurrent(
            Gen.Const(() => new ConcurrentDictionaryCache<int, int>()),
            Gen.Int[1, 5].Operation<ConcurrentDictionaryCache<int, int>>((d, i) => d.GetOrAddAtomicAsync(i, i => Task.FromResult(i)).Wait()),
            equal: (a, b) => Check.Equal(a.Keys, b.Keys),
            print: a => Check.Print(a.Keys)
        );
    }

    [Fact]
    public async Task GetOrAddAtomicAsync_Exception()
    {
        var cache = new ConcurrentDictionaryCache<int, int>();
        var exception = await Assert.ThrowsAsync<Exception>(() => cache.GetOrAddAtomicAsync(1, _ => Task.Run(int () => throw new Exception("no"))));
        Assert.Equal("no", exception.Message);
    }

    [Fact]
    public async Task GetOrAddAtomicAsync_ExceptionSync()
    {
        var cache = new ConcurrentDictionaryCache<int, int>();
        var exception = await Assert.ThrowsAsync<Exception>(() => cache.GetOrAddAtomicAsync(1, _ => throw new Exception("no")));
        Assert.Equal("no", exception.Message);
    }
}
