namespace Optimized.Collections;

/// <summary>
/// 
/// </summary>
public class Memoize
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
        var d = new Map<T, R>();
        return t => d.GetOrAdd(t, func);
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
        var d = new Map<T, R>();
        return t => d.GetOrLockedAdd(t, func);
    }
}