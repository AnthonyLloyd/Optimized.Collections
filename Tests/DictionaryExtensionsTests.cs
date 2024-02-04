namespace Tests;

using Xunit;
using CsCheck;
using Optimized.Collections;
using System.Collections.Concurrent;

public class DictionaryExtensionsTests
{
    [Fact]
    public void TestGetOrAddAtomic()
    {
        Check.SampleConcurrent(
            Gen.Const(() => new ConcurrentDictionary<int, int>()),
            Gen.Int[1, 5].Operation<ConcurrentDictionary<int, int>>((d, i) => d.GetOrAddAtomicAsync(i, j => Task.FromResult(j)).Wait()),
            equal: (d1, d2) => d1.Count == d2.Count
            );
    }
}
