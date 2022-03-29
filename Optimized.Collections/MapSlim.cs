using System.Runtime.CompilerServices;

namespace Optimized.Collections;

/// <summary>
/// 
/// </summary>
public static class MapSlim
{
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="R"></typeparam>
    /// <param name="func"></param>
    /// <returns></returns>
    public static Func<T, R> Memoize<T, R>(Func<T, R> func) where T : IEquatable<T>
    {
        var d = new MapSlim<T, R>();
        return t => d.GetOrAdd(t, func);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="R"></typeparam>
    /// <param name="func"></param>
    /// <returns></returns>
    public static Func<T, R> MemoizeMultiThreaded<T, R>(Func<T, R> func) where T : IEquatable<T>
    {
        var d = new MapSlim<T, R>();
        return t => d.GetOrLockedAdd(t, func);
    }
}

/// <summary>
/// Faster lookup. Lower memory. Lock free for read while Adding.
/// </summary>
/// <typeparam name="K"></typeparam>
/// <typeparam name="V"></typeparam>
public sealed class MapSlim<K, V> where K : IEquatable<K>
{
    struct Entry { internal int Bucket; internal int Next; internal K Key; internal V Value; }
    static class Holder { internal static Entry[] Initial = new Entry[1]; }
    int count;
    Entry[] entries;
    /// <summary>
    /// 
    /// </summary>
    public MapSlim() => entries = Holder.Initial;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="capacity"></param>
    public MapSlim(int capacity)
    {
        if (capacity < 2) capacity = 2;
        entries = new Entry[PowerOf2(capacity)];
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="items"></param>
    public MapSlim(IEnumerable<(K, V)> items)
    {
        entries = new Entry[2];
        foreach (var (k, v) in items) this[k] = v;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="dictionary"></param>
    public MapSlim(IDictionary<K, V> dictionary)
    {
        var count = dictionary.Count;
        entries = new Entry[count <= 2 ? 2 : PowerOf2(count)];
        foreach (var i in dictionary) this[i.Key] = i.Value;
    }

    /// <summary>
    /// 
    /// </summary>
    public int Count => count;

    static int PowerOf2(int capacity)
    {
        if ((capacity & (capacity - 1)) == 0) return capacity;
        int i = 2;
        while (i < capacity) i <<= 1;
        return i;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    Entry[] Resize()
    {
        var oldEntries = entries;
        if (oldEntries.Length == 1) return entries = new Entry[2];
        var newEntries = new Entry[oldEntries.Length * 2];
        for (int i = 0; i < oldEntries.Length;)
        {
            var bucketIndex = oldEntries[i].Key.GetHashCode() & (newEntries.Length - 1);
            newEntries[i].Next = newEntries[bucketIndex].Bucket - 1;
            newEntries[i].Key = oldEntries[i].Key;
            newEntries[i].Value = oldEntries[i].Value;
            newEntries[bucketIndex].Bucket = ++i;
        }
        return entries = newEntries;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    void AddItem(K key, V value, int hashCode)
    {
        var i = count;
        var ent = entries;
        if (ent.Length == i || ent.Length == 1) ent = Resize();
        var bucketIndex = hashCode & (ent.Length - 1);
        ent[i].Next = ent[bucketIndex].Bucket - 1;
        ent[i].Key = key;
        ent[i].Value = value;
        ent[bucketIndex].Bucket = ++count;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public V this[K key]
    {
        get
        {
            var ent = entries;
            var hashCode = key.GetHashCode();
            var i = ent[hashCode & (ent.Length - 1)].Bucket - 1;
            while (i >= 0 && !key.Equals(ent[i].Key)) i = ent[i].Next;
            return ent[i].Value;
        }
        set
        {
            var ent = entries;
            var hashCode = key.GetHashCode();
            var i = ent[hashCode & (ent.Length - 1)].Bucket - 1;
            while (i >= 0 && !key.Equals(ent[i].Key)) i = ent[i].Next;
            if (i >= 0) ent[i].Value = value;
            else AddItem(key, value, hashCode);
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public bool TryGetValue(K key, out V? value)
    {
        var ent = entries;
        var hashCode = key.GetHashCode();
        var i = ent[hashCode & (ent.Length - 1)].Bucket - 1;
        while (i >= 0 && !key.Equals(ent[i].Key)) i = ent[i].Next;
        if (i >= 0)
        {
            value = ent[i].Value;
            return true;
        }
        else
        {
            value = default;
            return false;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="key"></param>
    /// <param name="valueFactory"></param>
    /// <returns></returns>
    public V GetOrAdd(K key, Func<K, V> valueFactory)
    {
        var ent = entries;
        var hashCode = key.GetHashCode();
        var i = ent[hashCode & (ent.Length - 1)].Bucket - 1;
        while (i >= 0 && !key.Equals(ent[i].Key)) i = ent[i].Next;
        if (i >= 0) return ent[i].Value;
        else
        {
            var value = valueFactory(key);
            AddItem(key, value, hashCode);
            return value;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="key"></param>
    /// <param name="valueFactory"></param>
    /// <returns></returns>
    public V GetOrLockedAdd(K key, Func<K, V> valueFactory)
    {
        var ent = entries;
        var hashCode = key.GetHashCode();
        var i = ent[hashCode & (ent.Length - 1)].Bucket - 1;
        while (i >= 0 && !key.Equals(ent[i].Key)) i = ent[i].Next;
        if (i >= 0) return ent[i].Value;
        else
        {
            lock (this)
            {
                ent = entries;
                i = ent[hashCode & (ent.Length - 1)].Bucket - 1;
                while (i >= 0 && !key.Equals(ent[i].Key)) i = ent[i].Next;
                if (i >= 0) return ent[i].Value;
                else
                {
                    var value = valueFactory(key);
                    AddItem(key, value, hashCode);
                    return value;
                }
            }
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public ref V GetValueOrNullRef(K key)
    {
        var ent = entries;
        var hashCode = key.GetHashCode();
        var i = ent[hashCode & (ent.Length - 1)].Bucket - 1;
        while (i >= 0 && !key.Equals(ent[i].Key)) i = ent[i].Next;
        if (i >= 0) return ref ent[i].Value;
        else
        {
            i = count;
            if (ent.Length == i || ent.Length == 1) ent = Resize();
            var bucketIndex = hashCode & (ent.Length - 1);
            ent[i].Next = ent[bucketIndex].Bucket - 1;
            ent[i].Key = key;
#pragma warning disable CS8601 // Possible null reference assignment.
            ent[i].Value = default;
#pragma warning restore CS8601 // Possible null reference assignment.
            ent[bucketIndex].Bucket = ++count;
            return ref ent[i].Value;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public int IndexOf(K key)
    {
        var ent = entries;
        var hashCode = key.GetHashCode();
        var i = ent[hashCode & (ent.Length - 1)].Bucket - 1;
        while (i >= 0 && !key.Equals(ent[i].Key)) i = ent[i].Next;
        return i;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="i"></param>
    /// <returns></returns>
    public K Key(int i) => entries[i].Key;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="i"></param>
    /// <returns></returns>
    public V Value(int i) => entries[i].Value;
}
