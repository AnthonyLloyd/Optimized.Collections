using System.Collections;
using System.Runtime.CompilerServices;

namespace Optimized.Collections;

/// <summary>
/// Faster lookup. Lower memory. Lock free for read while Adding.
/// </summary>
/// <typeparam name="K"></typeparam>
/// <typeparam name="V"></typeparam>
public sealed class Map<K, V> : IReadOnlyDictionary<K, V> where K : IEquatable<K>
{
    struct Entry { internal int Bucket; internal int Next; internal K Key; internal V Value; }
    static class Holder { internal readonly static Entry[] Initial = new Entry[1]; }
    int _count;
    Entry[] _entries;
    /// <summary>
    /// 
    /// </summary>
    public Map() => _entries = Holder.Initial;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="capacity"></param>
    public Map(int capacity)
    {
        if (capacity < 2) capacity = 2;
        _entries = new Entry[PowerOf2(capacity)];
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="items"></param>
    public Map(IEnumerable<(K, V)> items)
    {
        _entries = new Entry[2];
        foreach (var (k, v) in items) this[k] = v;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="dictionary"></param>
    public Map(IDictionary<K, V> dictionary)
    {
        var count = dictionary.Count;
        _entries = new Entry[count <= 2 ? 2 : PowerOf2(count)];
        foreach (var i in dictionary) this[i.Key] = i.Value;
    }

    /// <summary>
    /// 
    /// </summary>
    public int Count => _count;

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
        var old_items = _entries;
        if (old_items.Length == 1) return _entries = new Entry[2];
        var new_items = new Entry[old_items.Length * 2];
        for (int i = 0; i < old_items.Length;)
        {
            var bucketIndex = old_items[i].Key.GetHashCode() & (new_items.Length - 1);
            new_items[i].Next = new_items[bucketIndex].Bucket - 1;
            new_items[i].Key = old_items[i].Key;
            new_items[i].Value = old_items[i].Value;
            new_items[bucketIndex].Bucket = ++i;
        }
        return _entries = new_items;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    void AddItem(K key, V value, int hashCode)
    {
        var i = _count;
        var ent = _entries;
        if (ent.Length == i || ent.Length == 1) ent = Resize();
        var bucketIndex = hashCode & (ent.Length - 1);
        ent[i].Next = ent[bucketIndex].Bucket - 1;
        ent[i].Key = key;
        ent[i].Value = value;
        ent[bucketIndex].Bucket = ++_count;
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
            var ent = _entries;
            var hashCode = key.GetHashCode();
            var i = ent[hashCode & (ent.Length - 1)].Bucket - 1;
            while (i >= 0 && !key.Equals(ent[i].Key)) i = ent[i].Next;
            return ent[i].Value;
        }
        set
        {
            var ent = _entries;
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
    public bool TryGetValue(K key, out V value)
    {
        var ent = _entries;
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
#pragma warning disable CS8601 // Possible null reference assignment.
            value = default;
#pragma warning restore CS8601 // Possible null reference assignment.
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
        var ent = _entries;
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
        var ent = _entries;
        var hashCode = key.GetHashCode();
        var i = ent[hashCode & (ent.Length - 1)].Bucket - 1;
        while (i >= 0 && !key.Equals(ent[i].Key)) i = ent[i].Next;
        if (i >= 0) return ent[i].Value;
        else
        {
            lock (this)
            {
                ent = _entries;
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
        var ent = _entries;
        var hashCode = key.GetHashCode();
        var i = ent[hashCode & (ent.Length - 1)].Bucket - 1;
        while (i >= 0 && !key.Equals(ent[i].Key)) i = ent[i].Next;
        if (i >= 0) return ref ent[i].Value;
        else
        {
            i = _count;
            if (ent.Length == i || ent.Length == 1) ent = Resize();
            var bucketIndex = hashCode & (ent.Length - 1);
            ent[i].Next = ent[bucketIndex].Bucket - 1;
            ent[i].Key = key;
#pragma warning disable CS8601 // Possible null reference assignment.
            ent[i].Value = default;
#pragma warning restore CS8601 // Possible null reference assignment.
            ent[bucketIndex].Bucket = ++_count;
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
        var ent = _entries;
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
    public K Key(int i) => _entries[i].Key;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="i"></param>
    /// <returns></returns>
    public V Value(int i) => _entries[i].Value;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public bool ContainsKey(K key) => IndexOf(key) != -1;

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    IEnumerator<KeyValuePair<K, V>> IEnumerable<KeyValuePair<K, V>>.GetEnumerator()
    {
        for (int i = 0; i < _count; i++)
            yield return new(_entries[i].Key, _entries[i].Value);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        for (int i = 0; i < _count; i++)
            yield return new KeyValuePair<K, V>(_entries[i].Key, _entries[i].Value);
    }

    /// <summary>
    /// 
    /// </summary>
    public IEnumerable<K> Keys
    {
        get
        {
            for (int i = 0; i < _count; i++)
                yield return _entries[i].Key;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public IEnumerable<V> Values
    {
        get
        {
            for (int i = 0; i < _count; i++)
                yield return _entries[i].Value;
        }
    }
}

/// <summary>
/// 
/// </summary>
public static class Map
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
    public static Func<T, R> MemoizeMultiThreaded<T, R>(Func<T, R> func) where T : IEquatable<T>
    {
        var d = new Map<T, R>();
        return t => d.GetOrLockedAdd(t, func);
    }
}