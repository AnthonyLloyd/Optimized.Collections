namespace Optimized.Collections;

using System.Collections;
using System.Diagnostics;
using System.Runtime.CompilerServices;

/// <summary>Represents a strongly typed grow only collection of keys and values.</summary>
/// <remarks>
/// - Lock free for reads during modification for reference types (and value types that set atomically).<br/>
/// - Better performance than <see cref="Dictionary{K, V}"/> in general.<br/>
/// </remarks>
/// <typeparam name="K">The type of the keys in the <see cref="Map{K, V}"/>.</typeparam>
/// <typeparam name="V">The type of the values in the <see cref="Map{K, V}"/>.</typeparam>
[DebuggerTypeProxy(typeof(MapDebugView<,>))]
[DebuggerDisplay("Count = {Count}")]
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
        _entries = capacity > 2 ? new Entry[Helper.PowerOf2(capacity)]
                 : capacity > 0 ? new Entry[2]
                 : Holder.Initial;
    }

    /// <summary>Initializes a new instance of the <see cref="Map{K, V}"/> class that contains elements copied from the specified <see cref="IEnumerable{T}"/>.</summary>
    /// <param name="collection">The <see cref="IEnumerable{T}"/> whose elements are copied to the new <see cref="Map{K, V}"/>.</param>
    public Map(IEnumerable<(K, V)> collection)
    {
        if (collection is Map<K, V> map)
        {
            _count = map._count;
            _entries = (Entry[])map._entries.Clone();
        }
        else
        {
            if (collection is IReadOnlyCollection<(K, V)> col)
            {
                _entries = col.Count > 2 ? new Entry[Helper.PowerOf2(col.Count)]
                         : col.Count > 0 ? new Entry[2]
                         : Holder.Initial;
            }
            else
            {
                _entries = Holder.Initial;
            }

            foreach (var (key, value) in collection)
                Add(key, value);
        }
    }

    /// <summary>Initializes a new instance of the <see cref="Map{K, V}"/> class that contains elements copied from the specified <see cref="IDictionary{K, V}"/> and uses the default equality comparer for the key type.</summary>
    /// <param name="dictionary">The <see cref="IDictionary{K, V}"/> whose elements are copied to the new <see cref="Map{K, V}"/>.</param>
    public Map(IReadOnlyDictionary<K, V> dictionary)
    {
        var count = dictionary.Count;
        if (count > 0)
        {
            _entries = new Entry[count > 2 ? Helper.PowerOf2(count) : 2];
            foreach (var kv in dictionary)
                Add(kv.Key, kv.Value);
        }
        else
        {
            _entries = Holder.Initial;
        }
    }

    /// <summary>Gets the number of key/value pairs contained in the <see cref="Map{K, V}"/>.</summary>
    /// <returns>The number of key/value pairs contained in the <see cref="Map{K, V}"/>.</returns>
    public int Count => _count;

    /// <summary>Adds the specified key and value to the <see cref="Map{K, V}"/>.</summary>
    /// <param name="key">The key of the element to add.</param>
    /// <param name="value">The value of the element to add. The value can be null for reference types.</param>
    public void Add(K key, V value)
    {
        var hashCode = key.GetHashCode();
        var entries = _entries;
        var i = entries[hashCode & (entries.Length - 1)].Bucket - 1;
        while (i >= 0 && !key.Equals(entries[i].Key)) i = entries[i].Next;
        if (i >= 0) Helper.ThrowElementWithSaemKeyAlreadyExistsInTheMap();
        AddItem(key, value, hashCode);
    }

    /// <summary>Attempts to add the specified key and value to the <see cref="Map{K, V}"/>.</summary>
    /// <param name="key">The key of the element to add.</param>
    /// <param name="value">The value of the element to add. It can be null.</param>
    /// <returns>true if the key/value pair was added to the <see cref="Map{K, V}"/> successfully; otherwise, false.</returns>
    public bool TryAdd(K key, V value)
    {
        var hashCode = key.GetHashCode();
        var entries = _entries;
        var i = entries[hashCode & (entries.Length - 1)].Bucket - 1;
        while (i >= 0 && !key.Equals(entries[i].Key)) i = entries[i].Next;
        if (i >= 0) return false;
        AddItem(key, value, hashCode);
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    Entry[] ResizeFull(int capacity)
    {
        var newEntries = new Entry[capacity];
        var entries = _entries;
        for (int i = 0; i < entries.Length;)
        {
            var bucketIndex = entries[i].Key.GetHashCode() & (newEntries.Length - 1);
            newEntries[i].Next = newEntries[bucketIndex].Bucket - 1;
            newEntries[i].Key = entries[i].Key;
            newEntries[i].Value = entries[i].Value;
            newEntries[bucketIndex].Bucket = ++i;
        }
        return _entries = newEntries;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    void AddItem(K key, V value, int hashCode)
    {
        var count = _count;
        var entries = _entries;
        if (entries.Length == count) entries = ResizeFull(entries.Length * 2);
        else if (entries.Length == 1) entries = _entries = new Entry[2];
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
            var hashCode = key.GetHashCode();
            var entries = _entries;
            var i = entries[hashCode & (entries.Length - 1)].Bucket - 1;
            while (i >= 0 && !key.Equals(entries[i].Key)) i = entries[i].Next;
            return entries[i].Value;
        }
        set
        {
            var hashCode = key.GetHashCode();
            var entries = _entries;
            var i = entries[hashCode & (entries.Length - 1)].Bucket - 1;
            while (i >= 0 && !key.Equals(entries[i].Key)) i = entries[i].Next;
            if (i >= 0) entries[i].Value = value;
            else AddItem(key, value, hashCode);
        }
    }

    /// <summary>Gets the value associated with the specified key.</summary>
    /// <param name="key">The key of the value to get.</param>
    /// <param name="value">When this method returns, contains the value associated with the specified key, if the key is found; otherwise, the default value for the type of the value parameter. This parameter is passed uninitialized.</param>
    /// <returns>true if the <see cref="Map{K, V}"/> contains an element with the specified key; otherwise, false.</returns>
    public bool TryGetValue(K key,
#if NET6_0
        [System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)]
#endif
        out V value)
    {
        var hashCode = key.GetHashCode();
        var entries = _entries;
        var i = entries[hashCode & (entries.Length - 1)].Bucket - 1;
        while (i >= 0 && !key.Equals(entries[i].Key)) i = entries[i].Next;
        if (i >= 0)
        {
            value = entries[i].Value;
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

    /// <summary>Adds a key/value pair to the <see cref="Map{K, V}"/> by using the specified function if the key does not already exist. Returns the new value, or the existing value if the key exists.</summary>
    /// <param name="key">The key of the element to add.</param>
    /// <param name="valueFactory">The function used to generate a value for the key.</param>
    /// <returns>The value for the key. This will be either the existing value for the key if the key is already in the <see cref="Map{K, V}"/>, or the new value if the key was not in the <see cref="Map{K, V}"/>.</returns>
    public V GetOrAdd(K key, Func<K, V> valueFactory)
    {
        var hashCode = key.GetHashCode();
        var entries = _entries;
        var i = entries[hashCode & (entries.Length - 1)].Bucket - 1;
        while (i >= 0 && !key.Equals(entries[i].Key)) i = entries[i].Next;
        if (i >= 0) return entries[i].Value;
        else
        {
            var value = valueFactory(key);
            AddItem(key, value, hashCode);
            return value;
        }
    }

    /// <summary>Adds a key/value pair to the <see cref="Map{K, V}"/> by using the specified function if the key does not already exist. Returns the new value, or the existing value if the key exists.</summary>
    /// <param name="key">The key of the element to add.</param>
    /// <param name="valueFactory">The function used to generate a value for the key.</param>
    /// <returns>The value for the key. This will be either the existing value for the key if the key is already in the <see cref="Map{K, V}"/>, or the new value if the key was not in the <see cref="Map{K, V}"/>.</returns>
    public V GetOrLockedAdd(K key, Func<K, V> valueFactory)
    {
        var hashCode = key.GetHashCode();
        var entries = _entries;
        var i = entries[hashCode & (entries.Length - 1)].Bucket - 1;
        while (i >= 0 && !key.Equals(entries[i].Key)) i = entries[i].Next;
        if (i >= 0) return entries[i].Value;
        else
        {
            lock (this)
            {
                entries = _entries;
                i = entries[hashCode & (entries.Length - 1)].Bucket - 1;
                while (i >= 0 && !key.Equals(entries[i].Key)) i = entries[i].Next;
                if (i >= 0) return entries[i].Value;
                else
                {
                    var value = valueFactory(key);
                    AddItem(key, value, hashCode);
                    return value;
                }
            }
        }
    }

    /// <summary>Adds a key/value pair to the <see cref="Map{K, V}"/> by using the specified function if the key does not already exist. Returns the new value, or the existing value if the key exists.</summary>
    /// <param name="key">The key of the element to add.</param>
    /// <returns>The value for the key. This will be either the existing value for the key if the key is already in the <see cref="Map{K, V}"/>, or a ref to the value in the <see cref="Map{K, V}"/>.</returns>
    public ref V GetValueOrNullRef(K key)
    {
        var hashCode = key.GetHashCode();
        var entries = _entries;
        var i = entries[hashCode & (entries.Length - 1)].Bucket - 1;
        while (i >= 0 && !key.Equals(entries[i].Key)) i = entries[i].Next;
        if (i >= 0) return ref entries[i].Value;
        else
        {
            i = _count;
            if (entries.Length == 1) entries = _entries = new Entry[2];
            else if (entries.Length == i) entries = ResizeFull(entries.Length * 2);
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

    /// <summary>Searches for the specified object and returns the zero-based index.</summary>
    /// <param name="key">The key to locate in the <see cref="Map{K, V}"/>.</param>
    /// <returns>The zero-based index of the item within the <see cref="Map{K, V}"/>, if found; otherwise, –1.</returns>
    public int IndexOf(K key)
    {
        var entries = _entries;
        var i = entries[(entries.Length - 1) & key.GetHashCode()].Bucket - 1;
        while (i >= 0 && !key.Equals(entries[i].Key)) i = entries[i].Next;
        return i;
    }

    /// <summary>Gets the key at the specified index.</summary>
    /// <param name="index">The zero-based index of the key to get.</param>
    /// <returns>The key at the specified index.</returns>
    public K Key(int index) => _entries[index].Key;

    /// <summary>Gets the value at the specified index.</summary>
    /// <param name="index">The zero-based index of the value to get.</param>
    /// <returns>The value at the specified index.</returns>
    public V Value(int index) => _entries[index].Value;

    /// <summary>Determines whether the <see cref="Map{K, V}"/> contains the specified key.</summary>
    /// <param name="key">The key to locate in the <see cref="Map{K, V}"/>.</param>
    /// <returns>true if the <see cref="Map{K, V}"/> contains an element with the specified key; otherwise, false.</returns>
    public bool ContainsKey(K key)
    {
        var entries = _entries;
        var i = entries[(entries.Length - 1) & key.GetHashCode()].Bucket - 1;
        while (i >= 0 && !key.Equals(entries[i].Key)) i = entries[i].Next;
        return i >= 0;
    }

    /// <summary>Determines whether the <see cref="Map{K, V}"/> contains a specific value.</summary>
    /// <param name="value">The value to locate in the <see cref="Map{K, V}"/>. The value can be null for reference types.</param>
    /// <returns>true if the <see cref="Map{K, V}"/> contains an element with the specified value; otherwise, false.</returns>
    public bool ContainsValue(V value)
    {
        var count = _count;
        var entries = _entries;
        if (value is null)
        {
            for (int i = 0; i < count; i++)
                if (entries[i].Value is null) return true;
        }
        else
        {
            for (int i = 0; i < count; i++)
                if (value.Equals(entries[i].Value)) return true;
        }
        return false;
    }

    IEnumerator<KeyValuePair<K, V>> IEnumerable<KeyValuePair<K, V>>.GetEnumerator()
    {
        var count = _count;
        var entries = _entries;
        for (int i = 0; i < count; i++)
            yield return new(entries[i].Key, entries[i].Value);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        var count = _count;
        var entries = _entries;
        for (int i = 0; i < count; i++)
            yield return new KeyValuePair<K, V>(entries[i].Key, entries[i].Value);
    }

    /// <summary>Returns the collection of keys in a <see cref="Map{K, V}"/>.</summary>
    public IEnumerable<K> Keys
    {
        get
        {
            var count = _count;
            var entries = _entries;
            for (int i = 0; i < count; i++)
                yield return entries[i].Key;
        }
    }

    /// <summary>Returns the collection of values in a <see cref="Map{K, V}"/>.</summary>
    public IEnumerable<V> Values
    {
        get
        {
            var count = _count;
            var entries = _entries;
            for (int i = 0; i < count; i++)
                yield return entries[i].Value;
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
        if (_count == 0)
        {
            if (capacity > 1) return (_entries = new Entry[Helper.PowerOf2(capacity)]).Length;
            if (capacity == 1)
            {
                _entries = new Entry[2];
                return 2;
            }
            return 0;
        }
        else
        {
            if (_entries.Length >= capacity) return _entries.Length;
            return ResizeCount(Helper.PowerOf2(capacity)).Length;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    Entry[] ResizeCount(int capacity)
    {
        var newEntries = new Entry[capacity];
        var count = _count;
        var entries = _entries;
        for (int i = 0; i < count;)
        {
            var bucketIndex = entries[i].Key.GetHashCode() & (newEntries.Length - 1);
            newEntries[i].Next = newEntries[bucketIndex].Bucket - 1;
            newEntries[i].Key = entries[i].Key;
            newEntries[i].Value = entries[i].Value;
            newEntries[bucketIndex].Bucket = ++i;
        }
        return _entries = newEntries;
    }
}