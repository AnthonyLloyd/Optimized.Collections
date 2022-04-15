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
    public static Func<Set<T>, Task<Vec<R>>> SingleThreaded<T, R>(Func<Set<T>, Task<Vec<R>>> func) where T : IEquatable<T>
    {
        var map = new Map<T, R>();
        return async requested =>
        {
            var results = new Vec<R>(requested.Count);
            var missing = new Set<T>();
            var missingIndex = new Set<int>();
            for (int i = 0; i < requested.Count; i++)
            {
                var t = requested[i];
                if (map.TryGetValue(t, out var r))
                {
                    results[i] = r;
                }
                else
                {
                    missing.Add(t);
                    missingIndex.Add(i);
                }
            }
            if (missing.Count != 0)
            {
                var missingResults = await func(missing);
                for (int i = 0; i < missingResults.Count; i++)
                {
                    var t = missing[i];
                    var r = missingResults[i];
                    map[t] = r;
                    results[missingIndex[i]] = r;
                }
            }
            return results;
        };
    }
}