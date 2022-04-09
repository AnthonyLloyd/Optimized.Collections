using System.Collections;
using System.Runtime.CompilerServices;

namespace Optimized.Collections;

/// <summary>
/// 
/// </summary>
/// <typeparam name="T"></typeparam>
public sealed class Set<T> : IReadOnlyCollection<T>, IReadOnlyList<T> where T : IEquatable<T>
{
    struct Entry { internal int Bucket; internal int Next; internal T Item; }
    static class Holder { internal static Entry[] Initial = new Entry[1]; }
    int _count;
    Entry[] _entries;

    /// <summary>
    /// 
    /// </summary>
    public Set() => _entries = Holder.Initial;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="capacity"></param>
    public Set(int capacity)
    {
        if (capacity < 2) capacity = 2;
        _entries = new Entry[PowerOf2(capacity)];
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="items"></param>
    public Set(IEnumerable<T> items)
    {
        _entries = new Entry[2];
        foreach (var i in items) Add(i);
    }

    static int PowerOf2(int capacity)
    {
        if ((capacity & (capacity - 1)) == 0) return capacity;
        int i = 2;
        while (i < capacity) i <<= 1;
        return i;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    Entry[] Resize()
    {
        if (_entries.Length == 1) return _entries = new Entry[2];
        var old_items = _entries;
        var new_items = new Entry[old_items.Length * 2];
        for (int i = 0; i < old_items.Length;)
        {
            var bucketIndex = old_items[i].Item.GetHashCode() & (new_items.Length - 1);
            new_items[i].Next = new_items[bucketIndex].Bucket - 1;
            new_items[i].Item = old_items[i].Item;
            new_items[bucketIndex].Bucket = ++i;
        }
        return _entries = new_items;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    int AddItem(T item, int hashCode)
    {
        var i = _count;
        var ent = _entries;
        if (ent.Length == i || ent.Length == 1) ent = Resize();
        var bucketIndex = hashCode & (ent.Length - 1);
        ent[i].Next = ent[bucketIndex].Bucket - 1;
        ent[i].Item = item;
        ent[bucketIndex].Bucket = ++_count;
        return i;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    public int Add(T item)
    {
        var entries = _entries;
        var hashCode = item.GetHashCode();
        var i = entries[hashCode & (entries.Length - 1)].Bucket - 1;
        while (i >= 0 && !item.Equals(entries[i].Item)) i = entries[i].Next;
        return i >= 0 ? i : AddItem(item, hashCode);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    public int IndexOf(T item)
    {
        var entries = _entries;
        var hashCode = item.GetHashCode();
        var i = entries[hashCode & (entries.Length - 1)].Bucket - 1;
        while (i >= 0 && !item.Equals(entries[i].Item)) i = entries[i].Next;
        return i;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    public bool Contains(T item) => IndexOf(item) >= 0;

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public IEnumerator<T> GetEnumerator()
    {
        var count = _count;
        var entries = _entries;
        for (int i = 0; i < count; i++)
            yield return entries[i].Item;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// 
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    public T this[int index]
    {
        get => _entries[index].Item;
        set => _entries[index].Item = value;
    }

    /// <summary>
    /// 
    /// </summary>
    public int Count => _count;
}