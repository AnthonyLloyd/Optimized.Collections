using System.Collections.Concurrent;
using CsCheck;
using Optimized.Collections;
using Xunit;

namespace Tests;

public class MemoizeTests
{
    readonly Action<string> writeLine;
    public MemoizeTests(Xunit.Abstractions.ITestOutputHelper output) => writeLine = output.WriteLine;

    [Fact]
    public void SingleThreaded_Performance()
    {
        static Func<T, R> MemoizeSingleThreadedStandard<T, R>(Func<T, R> func) where T : notnull
        {
            var d = new Dictionary<T, R>();
            return i =>
            {
                if (!d.TryGetValue(i, out var r))
                    d.Add(i, r = func(i));
                return r;
            };
        }
        
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

    [Fact]
    public void MultiThreaded_Performance()
    {
        static Func<T, R> MemoizeMultiThreadedStandard<T, R>(Func<T, R> func) where T : notnull
        {
            var d = new ConcurrentDictionary<T, R>();
            return i => d.GetOrAdd(i, func);
        }
        
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
        static Func<HashSet<T>, Task<R[]>> MemoizeSingleThreadedMany<T, R>(Func<HashSet<T>, Task<R[]>> func) where T : IEquatable<T>
        {
            var d = new Dictionary<T, R>();
            return async keys =>
            {
                var missing = new HashSet<T>();
                var missingIndex = new List<int>();
                var results = new R[keys.Count];
                var i = 0;
                foreach (var key in keys)
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
                    var missingResults = await func(missing);
                    i = 0;
                    foreach (var missingKey in missing)
                    {
                        var missingResult = missingResults[i];
                        d.Add(missingKey, missingResult);
                        results[missingIndex[i++]] = missingResult;
                    }
                }
                return results;
            };
        }
        
        var fSet = (Set<int> s) =>
        {
            var r = new int[s.Count];
            for (int i = 0; i < r.Length; i++)
                r[i] = s[i];
            return Task.FromResult(r);
        };

        var fHashSet = (HashSet<int> s) =>
        {
            var r = new int[s.Count];
            var i = 0;
            foreach(var v in s)
                r[i++] = v;
            return Task.FromResult(r);
        };

        Gen.Int.HashSet.Select(hs => (Set: new Set<int>(hs), HashSet: hs)).Array
        .Select(a => (a, Memoize.SingleThreaded(fSet), MemoizeSingleThreadedMany(fHashSet)))
        .Faster(
            (items, m, _) =>
            {
                for (int i = 0; i < items.Length; i++) m(items[i].Set).Wait();
            },
            (items, _, d) =>
            {
                for (int i = 0; i < items.Length; i++) d(items[i].HashSet).Wait();
            }
        ).Output(writeLine);
    }

    [Fact]
    public async Task MultiThreadedMany()
    {
        var func = (Set<int> set) =>
        {
            var r = new int[set.Count];
            for (int i = 0; i < r.Length; i++)
                r[i] = set[i];
            return Task.FromResult(r);
        };

        await Gen.Int[1, 10_000].HashSet.Select(hs => new Set<int>(hs)).Array
        .SampleAsync(async sets =>
        {
            var correct = true;
            var requested = new Set<int>();
            var memo = Memoize.MultiThreaded((Set<int> r) =>
            {
                lock (requested)
                {
                    foreach (var i in r)
                    {
                        var index = requested.Add(i);
                        if (index != requested.Count - 1)
                            correct = false;
                    }
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

    [Fact]
    public async Task MultiThreadedManyHashSet()
    {
        var func = (HashSet<int> set) =>
        {
            var r = new int[set.Count];
            int i = 0;
            foreach(var result in set)
                r[i++] = result;
            return Task.FromResult(r);
        };

        await Gen.Int[1, 10_000].HashSet.Array
        .SampleAsync(async sets =>
        {
            var correct = true;
            var requested = new Set<int>();
            var memo = Memoize.MultiThreaded((HashSet<int> r) =>
            {
                lock (requested)
                {
                    foreach (var i in r)
                    {
                        var index = requested.Add(i);
                        if (index != requested.Count - 1)
                            correct = false;
                    }
                }
                return func(r);
            });
            var tasks = Array.ConvertAll(sets, s => memo(s));
            for (int i = 0; i < sets.Length; i++)
            {
                var set = sets[i];
                var results = await tasks[i];
                int j = 0;
                foreach(var value in set)
                    if (results[j++] != value)
                        return false;
            }
            return correct;
        });
    }

    [Fact]
    public void MultiThreadedMany_Performance()
    {
        var fSet = (Set<int> s) =>
        {
            var r = new int[s.Count];
            for (int i = 0; i < r.Length; i++)
                r[i] = s[i];
            return Task.Delay(10).ContinueWith(_ => r);
        };

        var fHashSet = (HashSet<int> s) =>
        {
            var r = new int[s.Count];
            var i = 0;
            foreach (var v in s)
                r[i++] = v;
            return Task.Delay(10).ContinueWith(_ => r);
        };

        Gen.Int[1, 10_000].HashSet.Select(hs => (Set: new Set<int>(hs), HashSet: hs)).Array
        .Select(a => (a, Memoize.MultiThreaded(fSet), Memoize.MultiThreaded(fHashSet)))
        .Faster(
            (items, m, _) =>
            {
                var tasks = new Task[items.Length];
                for (int i = 0; i < items.Length; i++)
                    tasks[i] = m(items[i].Set);
                Task.WaitAll(tasks);
            },
            (items, _, d) =>
            {
                var tasks = new Task[items.Length];
                for (int i = 0; i < items.Length; i++)
                    tasks[i] = d(items[i].HashSet);
                Task.WaitAll(tasks);
            }
        ).Output(writeLine);
    }
}