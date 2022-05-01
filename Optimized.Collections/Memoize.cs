using System.Collections.Concurrent;

namespace Optimized.Collections;

/// <summary>
/// 
/// </summary>
public static class Memoize
{
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="R"></typeparam>
    /// <param name="func"></param>
    /// <returns></returns>
    public static Func<T, R> SingleThreaded<T, R>(Func<T, R> func) where T : IEquatable<T>
    {
        var map = new Map<T, R>();
        return t => map.GetOrAdd(t, func);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="R"></typeparam>
    /// <param name="func"></param>
    /// <returns></returns>
    public static Func<T, R> MultiThreaded<T, R>(Func<T, R> func) where T : IEquatable<T>
    {
        var map = new Map<T, R>();
        return t => map.GetOrLockedAdd(t, func);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="R"></typeparam>
    /// <param name="func"></param>
    /// <returns></returns>
    public static Func<Set<T>, Task<R[]>> SingleThreaded<T, R>(Func<Set<T>, Task<R[]>> func) where T : IEquatable<T>
    {
        var map = new Map<T, R>();
        return async requested =>
        {
            var results = new R[requested.Count];
            var missing = new Set<T>();
            var missingIndex = new Vec<int>();
            for (int i = 0; i < results.Length; i++)
            {
                var t = requested[i];
                if (map.TryGetValue(t, out var result))
                {
                    results[i] = result;
                }
                else
                {
                    missing.Add(t);
                    missingIndex.Add(i);
                }
            }
            if (missing.Count != 0)
            {
                var task = func(missing);
                map.EnsureCapacity(map.Count + missing.Count);
                var missingResults = await task;
                for (int i = 0; i < missingResults.Length; i++)
                {
                    var r = missingResults[i];
                    map.Add(missing[i], r);
                    results[missingIndex[i]] = r;
                }
            }
            return results;
        };
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="R"></typeparam>
    /// <param name="func"></param>
    /// <returns></returns>
    public static Func<Set<T>, Task<R[]>> MultiThreaded<T, R>(Func<Set<T>, Task<R[]>> func) where T : IEquatable<T>
    {
        var map = new Map<T, R>();
        var runningLock = new object();
        VecLink<(Set<T>, Task)>? running = null;
        return async requested =>
        {
            var results = new R[requested.Count];
            var missing = new Set<T>();
            var missingIndex = new Vec<int>();
            var runningTasks = running;
            for (int i = 0; i < requested.Count; i++)
            {
                var t = requested[i];
                if (map.TryGetValue(t, out var result))
                {
                    results[i] = result;
                }
                else
                {
                    missing.Add(t);
                    missingIndex.Add(i);
                }
            }

            if (missing.Count == 0) return results;

            var someAlreadyRunning = false;
            Task remainingTask;
            lock (runningLock)
            {
                var node = runningTasks;
                while (node is not null)
                {
                    if (missing.Overlaps(node.Value.Item1))
                    {
                        someAlreadyRunning = true;
                        break;
                    }
                    node = node.Next;
                }

                var remaining = someAlreadyRunning
                              ? (runningTasks!.Next is null
                                    ? missing.Except(runningTasks!.Value.Item1)
                                    : new Set<T>(missing.Except(runningTasks!.SelectMany(i => i.Item1))))
                              : missing;

                if (remaining.Count == 0)
                {
                    remainingTask = Task.CompletedTask;
                }
                else
                {
                    remainingTask = Task.Run(async () =>
                    {
                        var remainingResults = await func(remaining);
                        lock (map)
                        {
                            map.EnsureCapacity(map.Count + remaining.Count);
                            for (int i = 0; i < remaining.Count; i++)
                            {
                                map.Add(remaining[i], remainingResults[i]);
                            }
                        }
                        for (int i = 0; i < remaining.Count; i++)
                        {
                            T t = remaining[i];
                            results[missingIndex[missing.IndexOf(t)]] = remainingResults[i];
                        }
                    });

                    if (running is null) running = new VecLink<(Set<T>, Task)>((remaining, remainingTask));
                    else running.Add((remaining, remainingTask));
                }
            }

            if (someAlreadyRunning)
            {
                var node = runningTasks;
                while (node is not null && node.Value.Item2 != remainingTask)
                {
                    if (missing.Overlaps(node.Value.Item1))
                    {
                        await node.Value.Item2;
                        foreach (T t in node.Value.Item1)
                        {
                            var i = missing.IndexOf(t);
                            if (i != -1)
                            {
                                results[missingIndex[i]] = map[t];
                            }
                        }
                    }
                    node = node.Next;
                }
            }

            await remainingTask;

            lock (runningLock)
            {
                while (running is not null && running.Value.Item2.IsCompleted)
                {
                    running = running.Next;
                }
            }

            return results;
        };
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="R"></typeparam>
    /// <param name="func"></param>
    /// <returns></returns>
    public static Func<HashSet<T>, Task<R[]>> MultiThreaded<T, R>(Func<HashSet<T>, Task<R[]>> func) where T : IEquatable<T>
    {
        var dictionary = new ConcurrentDictionary<T, R>();
        VecLink<(HashSet<T>, Task)>? running = null;
        var runningLock = new object();
        return async keys =>
        {
            var missing = new Dictionary<T, int>();
            var results = new R[keys.Count];
            var runningTasks = running;
            var i = 0;
            foreach (var key in keys)
            {
                if (dictionary.TryGetValue(key, out var result))
                {
                    results[i++] = result;
                }
                else
                {
                    missing.Add(key, i++);
                }
            }
            if (missing.Count == 0) return results;

            bool someAlreadyRunning;
            Task remainingTask;
            lock (runningLock)
            {
                var remaining = new HashSet<T>(
                    runningTasks is null ? missing.Keys
                                         : missing.Keys.Except(runningTasks.SelectMany(i => i.Item1)));

                someAlreadyRunning = remaining.Count < missing.Count;

                if (remaining.Count == 0)
                {
                    remainingTask = Task.CompletedTask;
                }
                else
                {
                    remainingTask = Task.Run(async () =>
                    {
                        var remainingResults = await func(remaining);
                        int j = 0;
                        foreach (var remainingItem in remaining)
                        {
                            dictionary.TryAdd(remainingItem, remainingResults[j++]);
                        }
                        int i = 0;
                        foreach (var t in remaining)
                        {
                            results[missing[t]] = remainingResults[i++];
                        }
                    });

                    if (running is null) running = new VecLink<(HashSet<T>, Task)>((remaining, remainingTask));
                    else running.Add((remaining, remainingTask));
                }
            }

            if (someAlreadyRunning)
            {
                var node = runningTasks;
                while (node is not null && node.Value.Item2 != remainingTask)
                {
                    if (node.Value.Item1.Overlaps(missing.Keys))
                    {
                        await node.Value.Item2;
                        foreach (T t in node.Value.Item1)
                        {
                            if (missing.TryGetValue(t, out var index))
                            {
                                var result = dictionary[t];
                                results[index] = result;
                            }
                        }
                    }
                    node = node.Next;
                }
            }

            await remainingTask;

            lock (runningLock)
            {
                while (running is not null && running.Value.Item2.IsCompleted)
                {
                    running = running.Next;
                }
            }

            return results;
        };
    }
}