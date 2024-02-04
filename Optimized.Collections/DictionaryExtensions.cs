namespace Optimized.Collections;

using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

public static class DictionaryExtensions
{
    static class TaskCompletionSources<K, V>
    {
        public static ConcurrentDictionary<(IDictionary<K, V>, K), TaskCompletionSource> Current = [];
    }

    /// <summary>Hi</summary>
    public static async Task<V> GetOrAddAtomicAsync<K, V>(this IDictionary<K, V> dictionary, K key, Func<K, Task<V>> factory) where K : notnull
    {
        while (true)
        {
            if (dictionary.TryGetValue(key, out var value))
                return value;
            var myTCS = new TaskCompletionSource();
            var tcs = TaskCompletionSources<K, V>.Current.GetOrAdd((dictionary, key), myTCS);
            if (myTCS == tcs)
            {
                try
                {
                    if (dictionary.TryGetValue(key, out value))
                        return value;
                    dictionary[key] = value = await factory(key);
                    return value;
                }
                finally
                {
                    tcs.SetResult();
                    TaskCompletionSources<K, V>.Current.TryRemove((dictionary, key), out _);
                }
            }
            else
            {
                await tcs.Task;
            }
        }
    }

    static class Exceptions<K, V>
    {
        public static ConcurrentDictionary<(Func<K, Task<V>>, K), (DateTime, Exception)> Current = [];
    }

    public static Func<K, Task<V>> RetryExceptionsOnce<K, V>(this Func<K, Task<V>> func, TimeSpan delay)
    {
        return async k =>
        {
            try
            {
                return await func(k);
            }
            catch
            {
                await Task.Delay(delay);
                return await func(k);
            }
        };
    }

    public static Func<K, Task<V>> CacheExceptionsFor<K, V>(this Func<K, Task<V>> func, TimeSpan timeSpan)
    {
        return async k =>
        {
            try
            {
                if (Exceptions<K, V>.Current.TryGetValue((func, k), out var ex))
                {
                    if (ex.Item1.Add(timeSpan) < DateTime.UtcNow)
                    {
                        Exceptions<K, V>.Current.TryRemove((func, k), out _);
                        return await func(k);
                    }
                    throw ex.Item2;
                }
                else
                    return await func(k);
            }
            catch (Exception ex)
            {
                Exceptions<K, V>.Current[(func, k)] = (DateTime.UtcNow, ex);
                throw;
            }
        };
    }
}