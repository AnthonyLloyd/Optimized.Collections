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

    /// <summary>Memoize a function for single-threaded use.</summary>
    /// <param name="func">The function to memoize. This will only be called once for each input value.</param>
    /// <param name="map">The map to use for memoization.</param>
    public static Func<T, Task<R>> SingleThreaded<T, R>(Func<T, Task<R>> func, Map<T, R> map) where T : IEquatable<T>
    {
        return async t =>
        {
            if (map.TryGetValue(t, out var result))
                return result;
            result = await func(t);
            map.Add(t, result);
            return result;
        };
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
        var running = new VecLink2<(T, Task<R>)>();
        var runningLock = new object();
        return t =>
        {
            var node = running;
            if (map.TryGetValue(t, out var result1))
                return result1;
            TaskCompletionSource<R> tcs;
            lock (runningLock)
            {
                while (node.Next is not null)
                {
                    if (node.Value.Item1.Equals(t))
                        return node.Value.Item2.Result;
                    node = node.Next;
                }
                tcs = new TaskCompletionSource<R>();
                running.Add((t, tcs.Task));
            }
            var result = func(t);
            lock (map)
            {
                map.Add(t, result);
            }
            tcs.SetResult(result);
            lock (runningLock)
            {
                while (running.Next is not null && running.Value.Item2.IsCompleted)
                {
                    running = running.Next;
                }
            }
            return result;
        };
    }

    /// <summary>Memoize a function for multi-threaded use.</summary>
    /// <param name="func">The function to memoize. This will only be called once for each input value.</param>
    /// <param name="map">The map to use for memoization.</param>
    public static Func<T, Task<R>> MultiThreaded<T, R>(Func<T, Task<R>> func, Map<T, R> map) where T : IEquatable<T>
    {
        var runningLock = new object();
        VecLink<(T, Task<R>)>? running = null;
        return t =>
        {
            var runningTasks = running;
            if (map.TryGetValue(t, out var result))
                return Task.FromResult(result);
            Task<R> resultAsync;
            lock (runningLock)
            {
                while (runningTasks is not null)
                {
                    var (runningT, runningTask) = runningTasks.Value;
                    if (runningT.Equals(t))
                        return runningTask;
                    runningTasks = runningTasks.Next;
                }
                resultAsync = func(t);
                if (running is null) running = new((t, resultAsync));
                else running.Add((t, resultAsync));
            }
            return resultAsync.ContinueWith(r =>
            {
                lock (map)
                {
                    map.Add(t, r.Result);
                }
                lock (runningLock)
                {
                    while (running is not null && running.Value.Item2.IsCompleted)
                    {
                        running = running.Next;
                    }
                }
                return r.Result;
            });
        };
    }

    /// <summary>Memoize a function for multi-threaded use.</summary>
    /// <param name="func">The function to memoize. This will only be called once for each input value.</param>
    public static Func<T, Task<R>> MultiThreaded<T, R>(Func<T, Task<R>> func) where T : IEquatable<T>
        => MultiThreaded<T, R>(func, new());

    /// <summary>Memoize a collection based function for single-threaded use. Each individual input value wll only be called once.</summary>
    /// <param name="func">The set based function to memoize.</param>
    /// <param name="map">The map to use for memoization.</param>
    public static Func<IReadOnlyCollection<T>, Task<R[]>> SingleThreaded<T, R>(Func<IReadOnlyCollection<T>, Task<R[]>> func, Map<T, R> map) where T : IEquatable<T>
    {
        return async requested =>
        {
            var results = new R[requested.Count];
            var missing = new Set<T>();
            var missingIndex = new Vec<(T, int)>(); // Assumes only one
            var i = 0;
            foreach (var t in requested)
            {
                if (map.TryGetValue(t, out var result))
                {
                    results[i++] = result;
                }
                else
                {
                    missing.Add(t);
                    missingIndex.Add((t, i++));
                }
            }
            if (missing.Count != 0)
            {
                var task = func(missing);
                map.EnsureCapacity(map.Count + missing.Count);
                var missingResults = await task;
                for (i = 0; i < missing.Count; i++)
                {
                    map.Add(missing[i], missingResults[i]);
                }
                for (i = 0; i < missingIndex.Count; i++)
                {
                    var (t, index) = missingIndex[i];
                    results[index] = missingResults[missing.IndexOf(t)];
                }
            }
            return results;
        };
    }

    /// <summary>Memoize a colleciton based function for single-threaded use. Each individual input value wll only be called once.</summary>
    /// <param name="func">The set based function to memoize.</param>
    public static Func<IReadOnlyCollection<T>, Task<R[]>> SingleThreaded<T, R>(Func<IReadOnlyCollection<T>, Task<R[]>> func) where T : IEquatable<T>
        => SingleThreaded(func, new());

    /// <summary>Memoize a collection based function for single-threaded use. Each individual input value wll only be called once.</summary>
    /// <param name="func">The set based function to memoize.</param>
    /// <param name="map">The map to use for memoization.</param>
    public static Func<IReadOnlyCollection<T>, R[]> SingleThreaded<T, R>(Func<IReadOnlyCollection<T>, R[]> func, Map<T, R> map) where T : IEquatable<T>
    {
        return requested =>
        {
            var results = new R[requested.Count];
            var missing = new Set<T>();
            var missingIndex = new Vec<(T, int)>(); // Assumes only one
            var i = 0;
            foreach (var t in requested)
            {
                if (map.TryGetValue(t, out var result))
                {
                    results[i++] = result;
                }
                else
                {
                    missing.Add(t);
                    missingIndex.Add((t, i++));
                }
            }
            if (missing.Count != 0)
            {
                var missingResults = func(missing);
                map.EnsureCapacity(map.Count + missing.Count);
                for (i = 0; i < missing.Count; i++)
                {
                    map.Add(missing[i], missingResults[i]);
                }
                for (i = 0; i < missingIndex.Count; i++)
                {
                    var (t, index) = missingIndex[i];
                    results[index] = missingResults[missing.IndexOf(t)];
                }
            }
            return results;
        };
    }

    /// <summary>Memoize a collection based function for single-threaded use. Each individual input value wll only be called once.</summary>
    /// <param name="func">The set based function to memoize.</param>
    public static Func<IReadOnlyCollection<T>, R[]> SingleThreaded<T, R>(Func<IReadOnlyCollection<T>, R[]> func) where T : IEquatable<T>
        => SingleThreaded(func, new());

    /// <summary>Memoize a collection based function for multi-threaded use. Each individual input value wll only be called once.</summary>
    /// <param name="func">The set based function to memoize.</param>
    /// <param name="map">The map to use for memoization.</param>
    public static Func<IReadOnlyCollection<T>, Task<R[]>> MultiThreaded<T, R>(Func<IReadOnlyCollection<T>, Task<R[]>> func, Map<T, R> map) where T : IEquatable<T>
    {
        var runningLock = new object();
        VecLink<(Set<T>, Task)>? running = null;
        return async requested =>
        {
            var results = new R[requested.Count];
            var missing = new Set<T>();
            var runningTasks = running;
            var i = 0;
            foreach (var t in requested)
            {
                if (map.TryGetValue(t, out var result))
                {
                    results[i++] = result;
                }
                else
                {
                    missing.Add(t);
                    i++;
                }
            }

            if (missing.Count == 0) return results;

            var someAlreadyRunning = false;
            Task remainingTask;
            lock (runningLock)
            {
                var remaining = missing;
                if (runningTasks is not null)
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

                    if (someAlreadyRunning)
                    {
                        remaining = runningTasks.Next is null
                            ? missing.Except(runningTasks.Value.Item1)
                            : missing.Except(runningTasks.SelectMany(i => i.Item1));
                    }
                }

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
                            for (int j = 0; j < remaining.Count; j++)
                            {
                                map.Add(remaining[j], remainingResults[j]);
                            }
                        }
                        var i = 0;
                        foreach (var t in requested)
                        {
                            var index = remaining.IndexOf(t);
                            if (index != -1)
                            {
                                results[i] = remainingResults[index];
                            }
                            i++;
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
                        i = 0;
                        foreach (var t in requested)
                        {
                            if (set.Contains(t))
                            {
                                results[i] = map[t];
                            }
                            i++;
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

    /// <summary>Memoize a collection based function for multi-threaded use. Each individual input value wll only be called once.</summary>
    /// <param name="func">The set based function to memoize.</param>
    public static Func<IReadOnlyCollection<T>, Task<R[]>> MultiThreaded<T, R>(Func<IReadOnlyCollection<T>, Task<R[]>> func) where T : IEquatable<T>
        => MultiThreaded(func, new());

    /// <summary>Memoize a collection based function for multi-threaded use. Each individual input value wll only be called once.</summary>
    /// <param name="func">The set based function to memoize.</param>
    /// <param name="map">The map to use for memoization.</param>
    public static Func<IReadOnlyCollection<T>, Task<R[]>> MultiThreaded<T, R>(Func<IReadOnlyCollection<T>, R[]> func, Map<T, R> map) where T : IEquatable<T>
    {
        var runningLock = new object();
        VecLink<(Set<T>, Task)>? running = null;
        return async requested =>
        {
            var results = new R[requested.Count];
            var missing = new Set<T>();
            var runningTasks = running;
            var i = 0;
            foreach (var t in requested)
            {
                if (map.TryGetValue(t, out var result))
                {
                    results[i++] = result;
                }
                else
                {
                    missing.Add(t);
                    i++;
                }
            }

            if (missing.Count == 0) return results;

            var someAlreadyRunning = false;
            Task remainingTask;
            lock (runningLock)
            {
                var remaining = missing;
                if (runningTasks is not null)
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

                    if (someAlreadyRunning)
                    {
                        remaining = runningTasks.Next is null
                            ? missing.Except(runningTasks.Value.Item1)
                            : missing.Except(runningTasks.SelectMany(i => i.Item1));
                    }
                }

                if (remaining.Count == 0)
                {
                    remainingTask = Task.CompletedTask;
                }
                else
                {
                    remainingTask = Task.Run(() =>
                    {
                        var remainingResults = func(remaining);
                        var i = 0;
                        lock (map)
                        {
                            map.EnsureCapacity(map.Count + remaining.Count);
                            for (i = 0; i < remaining.Count; i++)
                            {
                                map.Add(remaining[i], remainingResults[i]);
                            }
                        }
                        i = 0;
                        foreach (var t in requested)
                        {
                            var index = remaining.IndexOf(t);
                            if (index != -1)
                            {
                                results[i] = remainingResults[index];
                            }
                            i++;
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
                        i = 0;
                        foreach (var t in requested)
                        {
                            if (set.Contains(t))
                            {
                                results[i] = map[t];
                            }
                            i++;
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

    /// <summary>Memoize a collection based function for multi-threaded use. Each individual input value wll only be called once.</summary>
    /// <param name="func">The set based function to memoize.</param>
    public static Func<IReadOnlyCollection<T>, Task<R[]>> MultiThreaded<T, R>(Func<IReadOnlyCollection<T>, R[]> func) where T : IEquatable<T>
        => MultiThreaded(func, new());
}