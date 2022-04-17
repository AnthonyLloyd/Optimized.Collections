using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Optimized.Collections;

/// <summary>
/// Faster lookup. Lower memory. Lock free for read while Adding.
/// </summary>
/// <typeparam name="K"></typeparam>
/// <typeparam name="V"></typeparam>
public sealed class Map<K, V> : IReadOnlyDictionary<K, V>, IReadOnlyList<KeyValuePair<K, V>> where K : IEquatable<K>
{
    struct Entry { internal int Bucket; internal int Next; internal K Key; internal V Value; }
    static class Holder { internal readonly static Entry[] Initial = new Entry[1]; }
    int _count;
    Entry[] _entries;

    /// <summary>Initializes a new instance of the <see cref="Map{K, V}"/> class that is empty, has the default initial capacity, and uses the default equality comparer for the key type.</summary>
    public Map() => _entries = Holder.Initial;

    /// <summary>Initializes a new instance of the <see cref="Map{K, V}"/> class that is empty and has the specified initial capacity</summary>
    /// <param name="capacity">The initial number of elements that the <see cref="Map{K, V}"/> can contain.</param>
    public Map(int capacity)
    {
        if (capacity < 2) capacity = 2;
        _entries = new Entry[Helper.PowerOf2(capacity)];
    }

    /// <summary>Initializes a new instance of the <see cref="Map{K, V}"/> class that contains elements copied from the specified <see cref="IEnumerable{T}"/>.</summary>
    /// <param name="collection">The <see cref="IEnumerable{T}"/> whose elements are copied to the new <see cref="Map{K, V}"/>.</param>
    public Map(IEnumerable<(K, V)> collection)
    {
        _entries = new Entry[2];
        foreach (var (k, v) in collection) this[k] = v;
    }

    //public Map(Map<K, V> items) ?

    /// <summary>Initializes a new instance of the <see cref="Map{K, V}"/> class that contains elements copied from the specified <see cref="IDictionary{K, V}"/> and uses the default equality comparer for the key type.</summary>
    /// <param name="dictionary">The <see cref="IDictionary{K, V}"/> whose elements are copied to the new <see cref="Map{K, V}"/>.</param>
    public Map(IDictionary<K, V> dictionary)
    {
        var count = dictionary.Count;
        if (count == 0)
        {
            _entries = Holder.Initial;
            return;
        }
        _entries = new Entry[count > 2 ? Helper.PowerOf2(count) : 2];
        foreach (var i in dictionary) this[i.Key] = i.Value;
    }

    /// <summary>Gets the number of key/value pairs contained in the <see cref="Map{K, V}"/>.</summary>
    /// <returns>The number of key/value pairs contained in the <see cref="Map{K, V}"/>.</returns>
    public int Count => _count;

    /// <summary>Adds the specified key and value to the <see cref="Map{K, V}"/>.</summary>
    /// <param name="key">The key of the element to add.</param>
    /// <param name="value">The value of the element to add. The value can be null for reference types.</param>
    public void Add(K key, V value)
    {
        var ent = _entries;
        var hashCode = key.GetHashCode();
        var i = ent[hashCode & (ent.Length - 1)].Bucket - 1;
        while (i >= 0 && !key.Equals(ent[i].Key)) i = ent[i].Next;
        if (i >= 0) Helper.ThrowElementWithSaemKeyAlreadyExistsInTheMap();
        AddItem(key, value, hashCode);
    }

    /// <summary>Attempts to add the specified key and value to the <see cref="Map{K, V}"/>.</summary>
    /// <param name="key">The key of the element to add.</param>
    /// <param name="value">The value of the element to add. It can be null.</param>
    /// <returns>true if the key/value pair was added to the <see cref="Map{K, V}"/> successfully; otherwise, false.</returns>
    public bool TryAdd(K key, V value)
    {
        var ent = _entries;
        var hashCode = key.GetHashCode();
        var i = ent[hashCode & (ent.Length - 1)].Bucket - 1;
        while (i >= 0 && !key.Equals(ent[i].Key)) i = ent[i].Next;
        if (i >= 0) return false;
        AddItem(key, value, hashCode);
        return true;
    }



    [MethodImpl(MethodImplOptions.NoInlining)]
    Entry[] Resize(int capacity)
    {
        var old_items = _entries;
        if (capacity == 2) return _entries = new Entry[2];
        var new_items = new Entry[capacity];
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
        var count = _count;
        var entries = _entries;
        if (entries.Length == count || entries.Length == 1) entries = Resize(entries.Length * 2);
        var bucketIndex = hashCode & (entries.Length - 1);
        entries[count].Next = entries[bucketIndex].Bucket - 1;
        entries[count].Key = key;
        entries[count].Value = value;
        entries[bucketIndex].Bucket = ++_count;
    }

    /// <summary>Gets or sets the value associated with the specified key.</summary>
    /// <param name="key">The key of the value to get or set.</param>
    /// <returns>The value associated with the specified key. If the specified key is not found, a get operation throws a <see cref="KeyNotFoundException"/>, and a set operation creates a new element with the specified key.</returns>
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

    /// <summary>Gets the value associated with the specified key.</summary>
    /// <param name="key">The key of the value to get.</param>
    /// <param name="value">When this method returns, contains the value associated with the specified key, if the key is found; otherwise, the default value for the type of the value parameter. This parameter is passed uninitialized.</param>
    /// <returns>true if the <see cref="Map{K, V}"/> contains an element with the specified key; otherwise, false.</returns>
    public bool TryGetValue(K key,
#if NET6_0
        [MaybeNullWhen(false)]
#endif
        out V value)
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
#if !NET6_0
#pragma warning disable CS8601 // Possible null reference assignment.
#endif
            value = default;
#if !NET6_0
#pragma warning restore CS8601 // Possible null reference assignment.
#endif
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
        var entries = _entries;
        var hashCode = key.GetHashCode();
        var i = entries[hashCode & (entries.Length - 1)].Bucket - 1;
        while (i >= 0 && !key.Equals(entries[i].Key)) i = entries[i].Next;
        if (i >= 0) return ref entries[i].Value;
        else
        {
            i = _count;
            if (entries.Length == i || entries.Length == 1) entries = Resize(entries.Length * 2);
            var bucketIndex = hashCode & (entries.Length - 1);
            entries[i].Next = entries[bucketIndex].Bucket - 1;
            entries[i].Key = key;
#pragma warning disable CS8601 // Possible null reference assignment.
            entries[i].Value = default;
#pragma warning restore CS8601 // Possible null reference assignment.
            entries[bucketIndex].Bucket = ++_count;
            return ref entries[i].Value;
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

    /// <summary>Determines whether the <see cref="Map{K, V}"/> contains the specified key.</summary>
    /// <param name="key">The key to locate in the <see cref="Map{K, V}"/>.</param>
    /// <returns>true if the <see cref="Map{K, V}"/> contains an element with the specified key; otherwise, false.</returns>
    public bool ContainsKey(K key) => IndexOf(key) != -1;

    /// <summary>Determines whether the <see cref="Map{K, V}"/> contains a specific value.</summary>
    /// <param name="value">The value to locate in the <see cref="Map{K, V}"/>. The value can be null for reference types.</param>
    /// <returns>true if the <see cref="Map{K, V}"/> contains an element with the specified value; otherwise, false.</returns>
    public bool ContainsValue(V value)
    {
        throw new NotImplementedException();
    }

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

    /// <summary>Returns the collection of keys in a <see cref="Map{K, V}"/>.</summary>
    public IEnumerable<K> Keys
    {
        get
        {
            for (int i = 0; i < _count; i++)
                yield return _entries[i].Key;
        }
    }

    /// <summary>Returns the collection of values in a <see cref="Map{K, V}"/>.</summary>
    public IEnumerable<V> Values
    {
        get
        {
            for (int i = 0; i < _count; i++)
                yield return _entries[i].Value;
        }
    }

    KeyValuePair<K, V> IReadOnlyList<KeyValuePair<K, V>>.this[int index]
    {
        get
        {
            var entries = _entries;
            return new(entries[index].Key, entries[index].Value);
        }
    }

    /// <summary>Ensures that the <see cref="Map{K, V}"/> can hold up to a specified number of entries without any further expansion of its backing storage.</summary>
    /// <param name="capacity">The number of entries.</param>
    /// <returns>The current capacity of the <see cref="Map{K, V}"/>.</returns>
    public int EnsureCapacity(int capacity)
    {
        if (capacity > _entries.Length) return Resize(Helper.PowerOf2(capacity)).Length;
        else if (_entries.Length > 1) return _entries.Length;
        else if (capacity == 1) return Resize(2).Length;
        else return 0;
    }
}