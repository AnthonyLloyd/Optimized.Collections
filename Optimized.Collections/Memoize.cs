﻿using System.Collections.Concurrent;

namespace Optimized.Collections;

/// <summary>Memoization functions are an optimization technique that stores results for quick reuse.</summary>
public static class Memoize
{
    /// <summary>Memoize a function for single-threaded use.</summary>
    /// <param name="func">The function to memoize. This will only be called once for each input value.</param>
    public static Func<T, R> SingleThreaded<T, R>(Func<T, R> func) where T : IEquatable<T>
        => SingleThreaded(func, new());

    /// <summary>Memoize a function for single-threaded use.</summary>
    /// <param name="func">The function to memoize. This will only be called once for each input value.</param>
    /// <param name="map">The map to use for memoization.</param>
    public static Func<T, R> SingleThreaded<T, R>(Func<T, R> func, Map<T, R> map) where T : IEquatable<T>
    {
        return t => map.GetOrAdd(t, func);
    }

    /// <summary>Memoize a function for multi-threaded use.</summary>
    /// <param name="func">The function to memoize. This will only be called once for each input value.</param>
    public static Func<T, R> MultiThreaded<T, R>(Func<T, R> func) where T : IEquatable<T>
        => MultiThreaded(func, new());

    /// <summary>Memoize a function for multi-threaded use.</summary>
    /// <param name="func">The function to memoize. This will only be called once for each input value.</param>
    /// <param name="map">The map to use for memoization.</param>
    public static Func<T, R> MultiThreaded<T, R>(Func<T, R> func, Map<T, R> map) where T : IEquatable<T>
    {
        return t => map.GetOrLockedAdd(t, func);
    }

    /// <summary>Memoize a set based function for single-threaded use. Each individual input value wll only be called once.</summary>
    /// <param name="func">The set based function to memoize.</param>
    public static Func<Set<T>, Task<R[]>> SingleThreaded<T, R>(Func<Set<T>, Task<R[]>> func) where T : IEquatable<T>
        => SingleThreaded(func, new());

    /// <summary>Memoize a set based function for single-threaded use. Each individual input value wll only be called once.</summary>
    /// <param name="func">The set based function to memoize.</param>
    /// <param name="map">The map to use for memoization.</param>
    public static Func<Set<T>, Task<R[]>> SingleThreaded<T, R>(Func<Set<T>, Task<R[]>> func, Map<T, R> map) where T : IEquatable<T>
    {
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

    /// <summary>Memoize a set based function for single-threaded use. Each individual input value wll only be called once.</summary>
    /// <param name="func">The set based function to memoize.</param>
    public static Func<IReadOnlyList<T>, Task<R[]>> SingleThreaded<T, R>(Func<IReadOnlyList<T>, Task<R[]>> func) where T : IEquatable<T>
        => SingleThreaded(func, new());

    /// <summary>Memoize a set based function for single-threaded use. Each individual input value wll only be called once.</summary>
    /// <param name="func">The set based function to memoize.</param>
    /// <param name="map">The map to use for memoization.</param>
    public static Func<IReadOnlyList<T>, Task<R[]>> SingleThreaded<T, R>(Func<IReadOnlyList<T>, Task<R[]>> func, Map<T, R> map) where T : IEquatable<T>
    {
        return async requested =>
        {
            var results = new R[requested.Count];
            var missing = new Set<T>();
            var missingIndex = new Vec<(T, int)>();
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
                    missingIndex.Add((t, i));
                }
            }
            if (missing.Count != 0)
            {
                var task = func(missing);
                map.EnsureCapacity(map.Count + missing.Count);
                var missingResults = await task;
                for (int i = 0; i < missing.Count; i++)
                {
                    map.Add(missing[i], missingResults[i]);
                }
                for(int i = 0; i < missingIndex.Count; i++)
                {
                    var (t, index) = missingIndex[i];
                    results[index] = missingResults[missing.IndexOf(t)];
                }
            }
            return results;
        };
    }

    /// <summary>Memoize a set based function for multi-threaded use. Each individual input value wll only be called once.</summary>
    /// <param name="func">The set based function to memoize.</param>
    public static Func<Set<T>, Task<R[]>> MultiThreaded<T, R>(Func<Set<T>, Task<R[]>> func) where T : IEquatable<T>
        => MultiThreaded(func, new());

    /// <summary>Memoize a set based function for multi-threaded use. Each individual input value wll only be called once.</summary>
    /// <param name="func">The set based function to memoize.</param>
    /// <param name="map">The map to use for memoization.</param>
    public static Func<Set<T>, Task<R[]>> MultiThreaded<T, R>(Func<Set<T>, Task<R[]>> func, Map<T, R> map) where T : IEquatable<T>
    {
        var runningLock = new object();
        VecLink<(Set<T>, Task)>? running = null;
        return async requested =>
        {
            var results = new R[requested.Count];
            var missing = new Set<T>();
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
                                    : missing.Except(runningTasks!.SelectMany(i => i.Item1)))
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
                        for (int i = 0; i < requested.Count; i++)
                        {
                            var index = remaining.IndexOf(requested[i]);
                            if (index != -1)
                            {
                                results[i] = remainingResults[index];
                            }
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
                    var set = node.Value.Item1;
                    if (set.Overlaps(missing))
                    {
                        await node.Value.Item2;
                        for (int i = 0; i < requested.Count; i++)
                        {
                            var t = requested[i];
                            if (set.Contains(t))
                            {
                                results[i] = map[t];
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

    /// <summary>Memoize a set based function for multi-threaded use. Each individual input value wll only be called once.</summary>
    /// <param name="func">The set based function to memoize.</param>
    public static Func<IReadOnlyList<T>, Task<R[]>> MultiThreaded<T, R>(Func<IReadOnlyList<T>, Task<R[]>> func) where T : IEquatable<T>
        => MultiThreaded(func, new());

    /// <summary>Memoize a set based function for multi-threaded use. Each individual input value wll only be called once.</summary>
    /// <param name="func">The set based function to memoize.</param>
    /// <param name="map">The map to use for memoization.</param>
    public static Func<IReadOnlyList<T>, Task<R[]>> MultiThreaded<T, R>(Func<IReadOnlyList<T>, Task<R[]>> func, Map<T, R> map) where T : IEquatable<T>
    {
        var runningLock = new object();
        VecLink<(Set<T>, Task)>? running = null;
        return async requested =>
        {
            var results = new R[requested.Count];
            var missing = new Set<T>();
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
                    if (node.Value.Item1.Overlaps(missing))
                    {
                        someAlreadyRunning = true;
                        break;
                    }
                    node = node.Next;
                }

                var remaining = someAlreadyRunning
                              ? (runningTasks!.Next is null
                                    ? missing.Except(runningTasks!.Value.Item1)
                                    : missing.Except(runningTasks!.SelectMany(i => i.Item1)))
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
                        for (int i = 0; i < requested.Count; i++)
                        {
                            var index = remaining.IndexOf(requested[i]);
                            if (index != -1)
                            {
                                results[i] = remainingResults[index];
                            }
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
                    var set = node.Value.Item1;
                    if (set.Overlaps(missing))
                    {
                        await node.Value.Item2;
                        for (int i = 0; i < requested.Count; i++)
                        {
                            var t = requested[i];
                            if (set.Contains(t))
                            {
                                results[i] = map[t];
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