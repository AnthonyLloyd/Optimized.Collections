//using System.Collections.Concurrent;
//using CsCheck;
//using Optimized.Collections;
//using Xunit;

//namespace Tests;

//public class MemoizeTests
//{
//    readonly Action<string> writeLine;
//    public MemoizeTests(Xunit.Abstractions.ITestOutputHelper output) => writeLine = output.WriteLine;

//    [Fact]
//    public void SingleThreaded_Performance()
//    {
//        static Func<T, R> MemoizeSingleThreadedStandard<T, R>(Func<T, R> func) where T : notnull
//        {
//            var d = new Dictionary<T, R>();
//            return i =>
//            {
//                if (!d.TryGetValue(i, out var r))
//                    d.Add(i, r = func(i));
//                return r;
//            };
//        }

//        var f = (int i) => i;

//        Gen.Int[0, 1000].Array
//        .Select(a => (a, Memoize.SingleThreaded(f), MemoizeSingleThreadedStandard(f)))
//        .Faster(
//            (items, m, _) =>
//            {
//                for(int i = 0; i < items.Length; i++) m(items[i]);
//            },
//            (items, _, d) =>
//            {
//                for (int i = 0; i < items.Length; i++) d(items[i]);
//            }
//        , sigma: 100, threads: 1).Output(writeLine);
//    }

//    [Fact]
//    public void MultiThreaded_Performance()
//    {
//        static Func<T, R> MemoizeMultiThreadedStandard<T, R>(Func<T, R> func) where T : notnull
//        {
//            var d = new ConcurrentDictionary<T, R>();
//            return i => d.GetOrAdd(i, func);
//        }

//        var f = (int i) => { Thread.Sleep(20); return i; };

//        Gen.Int[0, 9].Array[100]
//        .Select(a => (a, Memoize.MultiThreaded(f), MemoizeMultiThreadedStandard(f)))
//        .Faster(
//            (items, m, _) => Parallel.For(0, items.Length, i => m(items[i])),
//            (items, _, d) => Parallel.For(0, items.Length, i => d(items[i]))
//        , threads: 1).Output(writeLine);
//    }

//    [Fact]
//    public async Task SingleThreadedMany()
//    {
//        var f = (IReadOnlyCollection<int> s) =>
//        {
//            var r = new int[s.Count];
//            var i = 0;
//            foreach (var x in s) r[i++] = x;
//            return Task.FromResult(r);
//        };

//        await Gen.Int.HashSet.Select(hs => new Set<int>(hs)).Array
//        .SampleAsync(async sets =>
//        {
//            var correct = true;
//            var requested = new Set<int>();
//            var m = Memoize.SingleThreaded((IReadOnlyCollection<int> r) =>
//            {
//                foreach (var i in r)
//                {
//                    var index = requested.Add(i);
//                    if (index != requested.Count - 1)
//                        correct = false;
//                }
//                return f(r);
//            });
//            foreach (var set in sets)
//            {
//                var results = await m(set);
//                for (int i = 0; i < results.Length; i++)
//                {
//                    if (results[i] != set[i])
//                    {
//                        return false;
//                    }
//                }
//            }
//            return correct;
//        });
//    }

//    [Fact]
//    public void SingleThreadedMany_Performance()
//    {
//        static Func<HashSet<T>, Task<R[]>> MemoizeSingleThreadedMany<T, R>(Func<HashSet<T>, Task<R[]>> func) where T : IEquatable<T>
//        {
//            var d = new Dictionary<T, R>();
//            return async keys =>
//            {
//                var missing = new HashSet<T>();
//                var missingIndex = new List<int>();
//                var results = new R[keys.Count];
//                var i = 0;
//                foreach (var key in keys)
//                {
//                    if (d.TryGetValue(key, out var result))
//                    {
//                        results[i++] = result;
//                    }
//                    else
//                    {
//                        missing.Add(key);
//                        missingIndex.Add(i++);
//                    }
//                }
//                if (missing.Count > 0)
//                {
//                    var missingResults = await func(missing);
//                    i = 0;
//                    foreach (var missingKey in missing)
//                    {
//                        var missingResult = missingResults[i];
//                        d.Add(missingKey, missingResult);
//                        results[missingIndex[i++]] = missingResult;
//                    }
//                }
//                return results;
//            };
//        }

//        var fSet = (IReadOnlyCollection<int> s) =>
//        {
//            var r = new int[s.Count];
//            var i = 0;
//            foreach (var x in s) r[i++] = x;
//            return Task.FromResult(r);
//        };

//        var fHashSet = (HashSet<int> s) =>
//        {
//            var r = new int[s.Count];
//            var i = 0;
//            foreach(var v in s)
//                r[i++] = v;
//            return Task.FromResult(r);
//        };

//        Gen.Int.HashSet.Select(hs => (Set: new Set<int>(hs), HashSet: hs)).Array
//        .Select(a => (a, Memoize.SingleThreaded(fSet), MemoizeSingleThreadedMany(fHashSet)))
//        .Faster(
//            (items, m, _) =>
//            {
//                for (int i = 0; i < items.Length; i++) m(items[i].Set).Wait();
//            },
//            (items, _, d) =>
//            {
//                for (int i = 0; i < items.Length; i++) d(items[i].HashSet).Wait();
//            }
//        ).Output(writeLine);
//    }

//    [Fact]
//    public async Task MultiThreadedMany()
//    {
//        var func = (IReadOnlyCollection<int> set) =>
//        {
//            var r = new int[set.Count];
//            var i = 0;
//            foreach (var x in set) r[i++] = x;
//            return Task.FromResult(r);
//        };

//        await Gen.Int[1, 10_000].HashSet.Select(hs => new Set<int>(hs)).Array
//        .SampleAsync(async sets =>
//        {
//            var correct = true;
//            var requested = new Set<int>();
//            var memo = Memoize.MultiThreaded((IReadOnlyCollection<int> r) =>
//            {
//                lock (requested)
//                {
//                    foreach (var i in r)
//                    {
//                        var index = requested.Add(i);
//                        if (index != requested.Count - 1)
//                            correct = false;
//                    }
//                }
//                return func(r);
//            });
//            var tasks = Array.ConvertAll(sets, s => memo(s));
//            for(int i = 0; i < sets.Length; i++)
//            {
//                var set = sets[i];
//                var results = await tasks[i];
//                for (int j = 0; j < results.Length; j++)
//                    if (results[j] != set[j])
//                        return false;
//            }
//            return correct;
//        });
//    }

//    [Fact]
//    public async Task MultiThreadedManyHashSet()
//    {
//        var func = (HashSet<int> set) =>
//        {
//            var r = new int[set.Count];
//            int i = 0;
//            foreach(var result in set)
//                r[i++] = result;
//            return Task.FromResult(r);
//        };

//        await Gen.Int[1, 10_000].HashSet.Array
//        .SampleAsync(async sets =>
//        {
//            var correct = true;
//            var requested = new Set<int>();
//            var memo = MultiThreadedStandard((HashSet<int> r) =>
//            {
//                lock (requested)
//                {
//                    foreach (var i in r)
//                    {
//                        var index = requested.Add(i);
//                        if (index != requested.Count - 1)
//                            correct = false;
//                    }
//                }
//                return func(r);
//            });
//            var tasks = Array.ConvertAll(sets, s => memo(s));
//            for (int i = 0; i < sets.Length; i++)
//            {
//                var set = sets[i];
//                var results = await tasks[i];
//                int j = 0;
//                foreach(var value in set)
//                    if (results[j++] != value)
//                        return false;
//            }
//            return correct;
//        });
//    }

//    [Fact]
//    public void MultiThreadedMany_Performance()
//    {
//        var fSet = async (IReadOnlyCollection<int> s) =>
//        {
//            var r = new int[s.Count];
//            var i = 0;
//            foreach (var x in s) r[i++] = x;
//            await Task.Delay(100 + s.Count * 10);
//            return r;
//        };

//        var fHashSet = async (HashSet<int> s) =>
//        {
//            var r = new int[s.Count];
//            var i = 0;
//            foreach (var v in s)
//                r[i++] = v;
//            await Task.Delay(100 + s.Count * 10);
//            return r;
//        };

//        Gen.Int[1, 1000].HashSet.Select(hs => (Set: new Set<int>(hs), HashSet: new HashSet<int>(hs))).Array[2, 10]
//        .Select(a => (a, Memoize.MultiThreaded(fSet), MultiThreadedStandard(fHashSet)))
//        .Faster(
//            (items, m, _) =>
//            {
//                var tasks = new Task[items.Length];
//                for (int i = 0; i < items.Length; i++)
//                    tasks[i] = m(items[i].Set);
//                Task.WaitAll(tasks);
//            },
//            (items, _, d) =>
//            {
//                var tasks = new Task[items.Length];
//                for (int i = 0; i < items.Length; i++)
//                    tasks[i] = d(items[i].HashSet);
//                Task.WaitAll(tasks);
//            }
//        , threads: 1, raiseexception: false).Output(writeLine);
//    }

//    static Func<HashSet<T>, Task<R[]>> MultiThreadedStandard<T, R>(Func<HashSet<T>, Task<R[]>> func) where T : IEquatable<T>
//    {
//        var dictionary = new ConcurrentDictionary<T, R>();
//        VecLink<(HashSet<T>, Task)>? running = null;
//        var runningLock = new object();
//        return async keys =>
//        {
//            var missing = new Dictionary<T, int>();
//            var results = new R[keys.Count];
//            var runningTasks = running;
//            var i = 0;
//            foreach (var key in keys)
//            {
//                if (dictionary.TryGetValue(key, out var result))
//                {
//                    results[i++] = result;
//                }
//                else
//                {
//                    missing.Add(key, i++);
//                }
//            }
//            if (missing.Count == 0) return results;

//            bool someAlreadyRunning;
//            Task remainingTask;
//            lock (runningLock)
//            {
//                var remaining = new HashSet<T>(
//                    runningTasks is null ? missing.Keys
//                                         : missing.Keys.Except(runningTasks.SelectMany(i => i.Item1)));

//                someAlreadyRunning = remaining.Count < missing.Count;

//                if (remaining.Count == 0)
//                {
//                    remainingTask = Task.CompletedTask;
//                }
//                else
//                {
//                    remainingTask = Task.Run(async () =>
//                    {
//                        var remainingResults = await func(remaining);
//                        int j = 0;
//                        foreach (var remainingItem in remaining)
//                        {
//                            dictionary.TryAdd(remainingItem, remainingResults[j++]);
//                        }
//                        int i = 0;
//                        foreach (var t in remaining)
//                        {
//                            results[missing[t]] = remainingResults[i++];
//                        }
//                    });

//                    if (running is null) running = new VecLink<(HashSet<T>, Task)>((remaining, remainingTask));
//                    else running.Add((remaining, remainingTask));
//                }
//            }

//            if (someAlreadyRunning)
//            {
//                var node = runningTasks;
//                while (node is not null && node.Value.Item2 != remainingTask)
//                {
//                    if (node.Value.Item1.Overlaps(missing.Keys))
//                    {
//                        await node.Value.Item2;
//                        foreach (T t in node.Value.Item1)
//                        {
//                            if (missing.TryGetValue(t, out var index))
//                            {
//                                results[index] = dictionary[t];
//                            }
//                        }
//                    }
//                    node = node.Next;
//                }
//            }

//            await remainingTask;

//            lock (runningLock)
//            {
//                while (running is not null && running.Value.Item2.IsCompleted)
//                {
//                    running = running.Next;
//                }
//            }

//            return results;
//        };
//    }
//}