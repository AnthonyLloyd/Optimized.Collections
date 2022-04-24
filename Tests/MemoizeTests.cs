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

    static Func<HashSet<K>, Task<V[]>> MemoizeSingleThreadedMany<K, V>(Func<HashSet<K>, Task<V[]>> f) where K : IEquatable<K>
    {
        var d = new Dictionary<K, V>();
        return async keys =>
        {
            var missing = new HashSet<K>();
            var missingIndex = new List<int>();
            var results = new V[keys.Count];
            var i = 0;
            foreach(var key in keys)
            {
                if (d.TryGetValue(key, out var result))
                {
                    results[i++] = result;
                }
                else
                {
                    missing.Add(key);
                    missingIndex.Add(i++);
                }
            }
            if (missing.Count > 0)
            {
                var missingResults = await f(missing);
                i = 0;
                foreach(var missingKey in missing)
                {
                    var missingResult = missingResults[i];
                    d.Add(missingKey, missingResult);
                    results[missingIndex[i++]] = missingResult;
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

        var fhs = (HashSet<int> s) =>
        {
            var r = new int[s.Count];
            var i = 0;
            foreach(var v in s)
                r[i++] = v;
            return Task.FromResult(r);
        };

        Gen.Int.HashSet.Select(hs => (new Set<int>(hs), hs)).Array
        .Select(a => (a, Memoize.SingleThreaded(f), MemoizeSingleThreadedMany(fhs)))
        .Faster(
            (items, m, _) =>
            {
                for (int i = 0; i < items.Length; i++) m(items[i].Item1).Wait();
            },
            (items, _, d) =>
            {
                for (int i = 0; i < items.Length; i++) d(items[i].Item2).Wait();
            }
        ).Output(writeLine);
    }

    [Fact(Skip = "WIP")]
    public async Task MultiThreadedMany()
    {
        var func = (Set<int> set) =>
        {
            var r = new int[set.Count];
            for (int i = 0; i < r.Length; i++)
                r[i] = set[i];
            return Task.Delay(100).ContinueWith(_ => r);
        };

        await Gen.Int.HashSet.Select(hs => new Set<int>(hs)).Array
        .SampleAsync(async sets =>
        {
            var correct = true;
            var requested = new Set<int>();
            var memo = Memoize.MultiThreaded((Set<int> r) =>
            {
                foreach (var i in r)
                {
                    var index = requested.Add(i);
                    if (index != requested.Count - 1)
                        correct = false;
                }
                return func(r);
            });
            var tasks = Array.ConvertAll(sets, s => memo(s));
            for(int i = 0; i < sets.Length; i++)
            {
                var set = sets[i];
                var results = await tasks[i];
                for (int j = 0; j < results.Length; j++)
                    if (results[j] != set[j])
                        return false;
            }
            return correct;
        });
    }
}