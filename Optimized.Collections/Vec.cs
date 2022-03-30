using System.Collections;
using System.Runtime.CompilerServices;

namespace Optimized.Collections;

/// <summary>
/// 
/// </summary>
/// <typeparam name="T"></typeparam>
public class Vec<T> : IReadOnlyList<T>
{
    static readonly T[] emptyArray = new T[0];
    T[] entries;
    int count;

    /// <summary>
    /// 
    /// </summary>
    public Vec() => entries = emptyArray;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="capacity"></param>
    public Vec(int capacity) => entries = new T[capacity];

    /// <summary>
    /// 
    /// </summary>
    /// <param name="items"></param>
    public Vec(IEnumerable<T> items)
    {
        if (items is ICollection<T> ts)
        {
            entries = new T[ts.Count];
            ts.CopyTo(entries, 0);
        }
        else entries = items.ToArray();
        count = entries.Length;
    }

    /// <summary>
    /// 
    /// </summary>
    public int Count => count;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="i"></param>
    /// <returns></returns>
    public T this[int i]
    {
        get => entries[i];
        set => entries[i] = value;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    void AddWithResize(T item)
    {
        if (count == 0)
        {
            entries = new T[2];
            entries[0] = item;
            count = 1;
        }
        else
        {
            var newEntries = new T[count * 2];
            Array.Copy(entries, 0, newEntries, 0, count);
            newEntries[count] = item;
            entries = newEntries;
            count++;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="item"></param>
    public void Add(T item)
    {
        T[] e = entries;
        int c = count;
        if ((uint)c < (uint)e.Length)
        {
            e[c] = item;
            count = c + 1;
        }
        else AddWithResize(item);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public T[] ToArray()
    {
        int c = count;
        var a = new T[c];
        Array.Copy(entries, 0, a, 0, c);
        return a;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public IEnumerator<T> GetEnumerator()
    {
        for (int i = 0; i < count; i++)
            yield return entries[i];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}