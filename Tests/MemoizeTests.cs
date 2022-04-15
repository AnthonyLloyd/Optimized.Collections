using System.Collections.Concurrent;
using CsCheck;
using Optimized.Collections;
using Xunit;

namespace Tests;

public class MemoizeTests
{
    readonly Action<string> writeLine;
    public MemoizeTests(Xunit.Abstractions.ITestOutputHelper output) => writeLine = output.WriteLine;

    static Func<K, V> MemoizeSingleThreadedStandard<K, V>(Func<K, V> f) where K : notnull
    {
        var d = new Dictionary<K, V>();
        return i =>
        {
            if (!d.TryGetValue(i, out var r))
                d.Add(i, r = f(i));
            return r;
        };
    }

    [Fact]
    public void SingleThreaded_Performance()
    {
        var f = (int i) => i;

        Gen.Int.Array
        .Select(a => (a, Memoize.SingleThreaded(f), MemoizeSingleThreadedStandard(f)))
        .Faster(
            (items, m, _) =>
            {
                foreach (var i in items) m(i);
            },
            (items, _, d) =>
            {
                foreach (var i in items) d(i);
            }
        ).Output(writeLine);
    }

    static Func<K, V> MemoizeMultiThreadedStandard<K, V>(Func<K, V> f) where K : notnull
    {
        var d = new ConcurrentDictionary<K, V>();
        return i => d.GetOrAdd(i, f);
    }

    [Fact]
    public void MultiThreaded_Performance()
    {
        var f = (int i) => i;

        Gen.Int.Array
        .Select(a => (a, Memoize.MultiThreaded(f), MemoizeMultiThreadedStandard(f)))
        .Faster(
            (items, m, _) =>
            {
                foreach (var i in items) m(i);
            },
            (items, _, d) =>
            {
                foreach (var i in items) d(i);
            }
        ).Output(writeLine);
    }
}