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

    static Func<Set<K>, Task<V[]>> MemoizeSingleThreadedMany<K, V>(Func<Set<K>, Task<V[]>> f) where K : IEquatable<K>
    {
        var d = new Dictionary<K, V>();
        return async keys =>
        {
            var missing = new Set<K>();
            var missingIndex = new Vec<int>();
            var results = new V[keys.Count];
            for (int i = 0; i < keys.Count; i++)
            {
                var key = keys[i];
                if (d.TryGetValue(key, out var result))
                {
                    results[i] = result;
                }
                else
                {
                    missing.Add(key);
                    missingIndex.Add(i);
                }
            }
            if (missing.Count > 0)
            {
                var missingResults = await f(missing);
                for (int i = 0; i < missing.Count; i++)
                {
                    results[missingIndex[i]] = missingResults[i];
                    d.Add(missing[i], missingResults[i]);
                }
            }
            return results;
        };
    }

    [Fact]
    public async Task SingleThreadedMany()
    {
        var f = (Set<int> s) =>
        {
            var r = new int[s.Count];
            for (int i = 0; i < r.Length; i++)
                r[i] = s[i];
            return Task.FromResult(r);
        };

        await Gen.Int.HashSet.Select(hs => new Set<int>(hs)).Array
        .SampleAsync(async sets =>
        {
            var correct = true;
            var requested = new Set<int>();
            var m = Memoize.SingleThreaded((Set<int> r) =>
            {
                foreach (var i in r)
                {
                    var index = requested.Add(i);
                    if (index != requested.Count - 1)
                        correct = false;
                }
                return f(r);
            });
            foreach (var set in sets)
            {
                var results = await m(set);
                for (int i = 0; i < results.Length; i++)
                    if (results[i] != set[i])
                        return false;
            }
            return correct;
        });
    }

    [Fact]
    public void SingleThreadedMany_Performance()
    {
        var f = (Set<int> s) =>
        {
            var r = new int[s.Count];
            for (int i = 0; i < r.Length; i++)
                r[i] = s[i];
            return Task.FromResult(r);
        };

        Gen.Int.HashSet.Select(hs => new Set<int>(hs)).Array
        .Select(a => (a, Memoize.SingleThreaded(f), MemoizeSingleThreadedMany(f)))
        .Faster(
            (items, m, _) =>
            {
                for (int i = 0; i < items.Length; i++) m(items[i]).Wait();
            },
            (items, _, d) =>
            {
                for (int i = 0; i < items.Length; i++) d(items[i]).Wait();
            }
        ).Output(writeLine);
    }
}