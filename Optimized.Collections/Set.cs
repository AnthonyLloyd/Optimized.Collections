﻿namespace Optimized.Collections;

using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

/// <summary>Represents a strongly typed grow only set of values that can be accessed by index.</summary>
/// <remarks>
/// - Lock free for reads during modification for reference types (and value types that set atomically).<br/>
/// - 10-20% better performance than <see cref="HashSet{T}"/> in general.<br/>
/// </remarks>
/// <typeparam name="T">The type of elements in the set.</typeparam>
[DebuggerTypeProxy(typeof(IReadOnlyListDebugView<>))]
[DebuggerDisplay("Count = {Count}")]
public sealed class Set<T> : IReadOnlySet<T>, IReadOnlyList<T> where T : IEquatable<T>
{
    const int FIBONACCI_HASH = -1640531527;
    internal struct Entry { internal int Bucket; internal int Next; internal T Item; }
    static class Holder { internal static Entry[] Initial = new Entry[1]; }
    int _count;
    Entry[] _entries;

    /// <summary>Initializes a new instance of the <see cref="Set{T}"/> class that is empty.</summary>
    public Set() => _entries = Holder.Initial;

    /// <summary>Initializes a new instance of the <see cref="Set{T}"/> class with size the smallest power of two that's greater or equal to the given capacity.</summary>
    /// <param name="capacity">The initial capacity of the <see cref="Set{T}"/>.</param>
    public Set(int capacity)
    {
        _entries = capacity > 2 ? new Entry[Helper.PowerOf2(capacity)]
                 : capacity > 0 ? new Entry[2]
                 : Holder.Initial;
    }

    /// <summary>Initializes a new instance of the <see cref="Set{T}"/> class that contains elements copied from the specified collection.</summary>
    /// <param name="collection">The collection whose elements are copied to the new set.</param>
    public Set(IEnumerable<T> collection)
    {
        if (collection is Set<T> set)
        {
            _count = set._count;
            _entries = (Entry[])set._entries.Clone();
        }
        else
        {
            var capacity = collection is IReadOnlyCollection<T> c ? c.Count : 0;
            _entries = capacity > 2 ? new Entry[Helper.PowerOf2(capacity)]
                     : capacity > 0 ? new Entry[2]
                     : Holder.Initial;
            foreach (var item in collection)
                Add(item);
        }
    }

    /// <summary>Gets the number of elements that are contained in a <see cref="Set{T}"/>.</summary>
    /// <returns>The number of elements contained in the <see cref="Set{T}"/>.</returns>
    public int Count => _count;

    /// <summary>Gets or sets the element at the specified index.</summary>
    /// <param name="index">The zero-based index of the element to get or set.</param>
    /// <returns>The element at the specified index.</returns>
    public T this[int index]
    {
        get => _entries[index].Item;
        set => _entries[index].Item = value;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    Entry[] ResizeFull(int capacity)
    {
        var newEntries = new Entry[capacity];
        var entries = _entries;
        for (int i = 0; i < entries.Length;)
        {
            var bucketIndex = (entries[i].Item.GetHashCode() * FIBONACCI_HASH) & (newEntries.Length - 1);
            newEntries[i].Next = newEntries[bucketIndex].Bucket - 1;
            newEntries[i].Item = entries[i].Item;
            newEntries[bucketIndex].Bucket = ++i;
        }
        return _entries = newEntries;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    int AddItem(T item, int hashCode)
    {
        var count = _count;
        var entries = _entries;
        if (entries.Length == count) entries = ResizeFull(entries.Length * 2);
        else if (entries.Length == 1) entries = _entries = new Entry[2];
        var bucketIndex = hashCode & (entries.Length - 1);
        entries[count].Next = entries[bucketIndex].Bucket - 1;
        entries[count].Item = item;
        entries[bucketIndex].Bucket = ++_count;
        return count;
    }

    /// <summary>Adds the specified element to the <see cref="Set{T}"/>.</summary>
    /// <param name="item">The object to add to the <see cref="Set{T}"/>.</param>
    public int Add(T item)
    {
        var entries = _entries;
        var hashCode = item.GetHashCode() * FIBONACCI_HASH;
        var i = entries[(entries.Length - 1) & hashCode].Bucket - 1;
        while (i >= 0)
        {
            if (entries[i].Item.Equals(item))
                return i;
            i = entries[i].Next;
        }
        return AddItem(item, hashCode);
    }

    /// <summary>Adds or replaces the specified element to the <see cref="Set{T}"/>.</summary>
    /// <param name="item">The object to add to the <see cref="Set{T}"/>.</param>
    public int AddOrUpdate(T item)
    {
        var entries = _entries;
        var hashCode = item.GetHashCode() * FIBONACCI_HASH;
        var i = entries[(entries.Length - 1) & hashCode].Bucket - 1;
        while (i >= 0)
        {
            if (entries[i].Item.Equals(item))
            {
                entries[i].Item = item;
                return i;
            }
            i = entries[i].Next;
        }
        return AddItem(item, hashCode);
    }

    /// <summary>Searches for the specified object and returns the zero-based index.</summary>
    /// <param name="item">The object to locate in the <see cref="Set{T}"/>.</param>
    /// <returns>The zero-based index of the item within the <see cref="Set{T}"/>, if found; otherwise, –1.</returns>
    public int IndexOf(T item)
    {
        var entries = _entries;
        var i = entries[(item.GetHashCode() * FIBONACCI_HASH) & (entries.Length - 1)].Bucket - 1;
        while (i >= 0)
        {
            if (entries[i].Item.Equals(item))
                return i;
            i = entries[i].Next;
        }
        return i;
    }

    /// <summary>Determines whether a <see cref="Set{T}"/> contains the specified item.</summary>
    /// <param name="item">The object to locate in the <see cref="Set{T}"/>.</param>
    /// <returns>true if the item is found in the <see cref="Set{T}"/>; otherwise, false.</returns>
    public bool Contains(T item)
    {
        var entries = _entries;
        var i = entries[(item.GetHashCode() * FIBONACCI_HASH) & (entries.Length - 1)].Bucket - 1;
        while (i >= 0)
        {
            if (entries[i].Item.Equals(item))
                return true;
            i = entries[i].Next;
        }
        return false;
    }

    /// <summary>Searches the <see cref="Set{T}"/> for a given value and returns the equal value it finds, if any.</summary>
    /// <param name="equalValue">The value to search for.</param>
    /// <param name="actualValue">The value from the <see cref="Set{T}"/> that the search found, or the default value of T when the search yielded no match.</param>
    public bool TryGetValue(T equalValue, [MaybeNullWhen(false)] out T actualValue)
    {
        var i = IndexOf(equalValue);
        if (i >= 0)
        {
            actualValue = _entries[i].Item;
            return true;
        }
        else
        {
            actualValue = default;
            return false;
        }
    }

    /// <summary>Creates a <see cref="SetSpan{T}"/></summary>
    public SetSpan<T> AsSpan() => new(_entries, _count);

    /// <summary>Returns an enumerator that iterates through the <see cref="Set{T}"/>.</summary>
    /// <returns>An enumerator for the <see cref="Set{T}"/>.</returns>
    public IEnumerator<T> GetEnumerator()
    {
        var count = _count;
        var entries = _entries;
        for (int i = 0; i < count; i++)
            yield return entries[i].Item;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Returns the items in this set except the ones in other.</summary>
    /// <param name="other">items to exclude</param>
    public Set<T> Except(IEnumerable<T> other)
    {
        var count = _count;
        if (other is Set<T> otherSet)
        {
            var set = new Set<T>(count);
            for (int i = 0; i < count; i++)
            {
                var item = _entries[i].Item;
                if (!otherSet.Contains(item))
                    set.Add(item);
            }
            return set;
        }
        else
        {
            var bitArray = new BitArray(count, true);
            foreach (T item in other)
            {
                int index = IndexOf(item);
                if (index >= 0 && bitArray.Get(index))
                {
                    bitArray.Set(index, false);
                }
            }
            var set = new Set<T>(count);
            for (int i = 0; i < count; i++)
            {
                if (bitArray.Get(i))
                    set.Add(_entries[i].Item);
            }
            return set;
        }
    }

    /// <summary>Determines whether the current <see cref="Set{T}"/> object and a specified collection share common elements.</summary>
    /// <param name="other">The collection to compare to the current <see cref="Set{T}"/> object.</param>
    /// <returns>true if the <see cref="Set{T}"/> object and other share at least one common element; otherwise, false.</returns>
    public bool Overlaps(IEnumerable<T> other)
    {
        foreach (var element in other)
            if (IndexOf(element) != -1)
                return true;
        return false;
    }

    /// <summary>Determines whether a <see cref="Set{T}"/> object and the specified collection contain the same elements.</summary>
    /// <param name="other">The collection to compare to the current <see cref="Set{T}"/> object.</param>
    /// <returns>true if the <see cref="Set{T}"/> object is equal to other; otherwise, false.</returns>
    public bool SetEquals(IEnumerable<T> other)
    {
        var count = _count;

        if (other is Set<T> otherSet)
        {
            if (count != otherSet._count)
                return false;
            var entries = _entries;
            for (int i = 0; i < count; i++)
            {
                if (otherSet.IndexOf(entries[i].Item) == -1)
                    return false;
            }
            return true;
        }
        if (count == 0 && other is IReadOnlyCollection<T> otherAsCollection)
            return otherAsCollection.Count == 0;
        var bitArray = new BitArray(count);
        var uniqueFoundCount = 0;
        foreach (T item in other)
        {
            int index = IndexOf(item);
            if (index == -1)
                return false;
            if (!bitArray.Get(index))
            {
                bitArray.Set(index, true);
                uniqueFoundCount++;
            }
        }
        return uniqueFoundCount == count;
    }

    /// <summary>Determines whether a <see cref="Set{T}"/> object is a subset of the specified collection.</summary>
    /// <param name="other">The collection to compare to the current <see cref="Set{T}"/> object.</param>
    /// <returns>true if the <see cref="Set{T}"/> object is a subset of other; otherwise, false.</returns>
    public bool IsSubsetOf(IEnumerable<T> other)
    {
        var count = _count;
        if (count == 0 || other == this)
            return true;
        if (other is Set<T> otherAsSet)
        {
            if (count > otherAsSet._count)
                return false;
            var entries = _entries;
            for (int i = 0; i < count; i++)
            {
                if (otherAsSet.IndexOf(entries[i].Item) == -1)
                    return false;
            }
            return true;
        }

        if (other is HashSet<T> otherAsHashSet)
        {
            if (count > otherAsHashSet.Count)
                return false;
            var entries = _entries;
            for (int i = 0; i < count; i++)
            {
                if (!otherAsHashSet.Contains(entries[i].Item))
                    return false;
            }
            return true;
        }
        var bitArray = new BitArray(count);
        var uniqueFoundCount = 0;
        foreach (T item in other)
        {
            int index = IndexOf(item);
            if (index >= 0 && !bitArray.Get(index))
            {
                bitArray.Set(index, true);
                uniqueFoundCount++;
            }
        }
        return uniqueFoundCount == count;
    }

    /// <summary>Determines whether a <see cref="Set{T}"/> object is a proper subset of the specified collection.</summary>
    /// <param name="other">The collection to compare to the current <see cref="Set{T}"/> object.</param>
    /// <returns>true if the <see cref="Set{T}"/> object is a proper subset of other; otherwise, false.</returns>
    public bool IsProperSubsetOf(IEnumerable<T> other)
    {
        var count = _count;
        if (other == this)
            return false;
        if (other is Set<T> otherAsSet)
        {
            if (count >= otherAsSet._count)
                return false;
            var entries = _entries;
            for (int i = 0; i < count; i++)
            {
                if (otherAsSet.IndexOf(entries[i].Item) == -1)
                    return false;
            }

            return true;
        }
        if (other is IReadOnlyCollection<T> otherAsCollection)
        {
            if (otherAsCollection.Count == 0)
                return false;
            if (count == 0)
                return otherAsCollection.Count > 0;
            if (other is HashSet<T> otherAsHashSet)
            {
                if (count >= otherAsHashSet.Count)
                    return false;
                var entries = _entries;
                for (int i = 0; i < count; i++)
                {
                    if (!otherAsHashSet.Contains(entries[i].Item))
                        return false;
                }

                return true;
            }
        }

        var bitArray = new BitArray(count);
        var uniqueFoundCount = 0;
        var unfound = false;
        foreach (T item in other)
        {
            int index = IndexOf(item);
            if (index == -1)
            {
                if (uniqueFoundCount == count)
                    return true;
                unfound = true;
            }
            else if (!bitArray.Get(index))
            {
                if (++uniqueFoundCount == count && unfound)
                    return true;
                bitArray.Set(index, true);
            }
        }
        return false;
    }

    /// <summary>Determines whether a <see cref="Set{T}"/> object is a proper superset of the specified collection.</summary>
    /// <param name="other">The collection to compare to the current <see cref="Set{T}"/> object.</param>
    /// <returns>true if the <see cref="Set{T}"/> object is a proper superset of other; otherwise, false.</returns>
    public bool IsProperSupersetOf(IEnumerable<T> other)
    {
        var count = _count;
        if (count == 0 || other == this)
            return false;

        if (other is Set<T> otherSet)
        {
            var otherCount = otherSet._count;
            if (otherCount >= _count)
                return false;
            var otherEntries = otherSet._entries;
            for (int i = 0; i < otherCount; i++)
            {
                if (IndexOf(otherEntries[i].Item) == -1)
                    return false;
            }
            return true;
        }

        if (other is IReadOnlyCollection<T> otherAsCollection)
        {
            if (otherAsCollection.Count == 0)
                return true;
            if (other is HashSet<T> otherAsSet)
            {
                if (otherAsSet.Count >= count)
                    return false;
                foreach (T element in otherAsSet)
                {
                    if (IndexOf(element) == -1)
                        return false;
                }
                return true;
            }
        }

        var bitArray = new BitArray(count);
        var uniqueFoundCount = 0;
        foreach (T item in other)
        {
            int index = IndexOf(item);
            if (index == -1)
                return false;
            if (!bitArray.Get(index))
            {
                bitArray.Set(index, true);
                uniqueFoundCount++;
            }
        }
        return uniqueFoundCount < count;
    }

    /// <summary>Determines whether a <see cref="Set{T}"/> object is a superset of the specified collection.</summary>
    /// <param name="other">The collection to compare to the current <see cref="Set{T}"/> object.</param>
    /// <returns>true if the <see cref="Set{T}"/> object is a superset of other; otherwise, false.</returns>
    public bool IsSupersetOf(IEnumerable<T> other)
    {
        if (other == this) return true;

        if (other is Set<T> otherSet)
        {
            var otherCount = otherSet._count;
            if (otherCount > _count)
                return false;
            var otherEntries = otherSet._entries;
            for (int i = 0; i < otherCount; i++)
            {
                if (IndexOf(otherEntries[i].Item) == -1)
                    return false;
            }
            return true;
        }

        if (other is IReadOnlyCollection<T> otherAsCollection)
        {
            if (otherAsCollection.Count == 0)
                return true;
            if (other is HashSet<T> otherAsHashSet && otherAsHashSet.Count > _count)
                return false;
        }
        foreach (T element in other)
        {
            if (IndexOf(element) == -1)
                return false;
        }
        return true;
    }

    /// <summary>Copies the elements of a <see cref="Set{T}"/> object to an array.</summary>
    /// <param name="array">The one-dimensional array that is the destination of the elements copied from the <see cref="Set{T}"/> object. The array must have zero-based indexing.</param>
    public void CopyTo(T[] array)
    {
        var count = _count;
        var entries = _entries;
        for (int i = 0; i < count; i++)
            array[i] = entries[i].Item;
    }

    /// <summary>Copies the elements of a <see cref="Set{T}"/> object to an array, starting at the specified array index.</summary>
    /// <param name="array">The one-dimensional array that is the destination of the elements copied from the <see cref="Set{T}"/> object. The array must have zero-based indexing.</param>
    /// <param name="arrayIndex">The zero-based index in array at which copying begins.</param>
    public void CopyTo(T[] array, int arrayIndex)
    {
        var count = _count;
        var entries = _entries;
        for (int i = 0; i < count; i++)
            array[i + arrayIndex] = entries[i].Item;
    }

    /// <summary>Copies the specified number of elements of a <see cref="Set{T}"/> object to an array, starting at the specified array index.</summary>
    /// <param name="array">The one-dimensional array that is the destination of the elements copied from the <see cref="Set{T}"/> object. The array must have zero-based indexing.</param>
    /// <param name="arrayIndex">The zero-based index in array at which copying begins.</param>
    /// <param name="count">The number of elements to copy to array.</param>
    public void CopyTo(T[] array, int arrayIndex, int count)
    {
        var entries = _entries;
        for (int i = 0; i < count; i++)
            array[i + arrayIndex] = entries[i].Item;
    }

    /// <summary>Ensures that this set can hold the specified number of elements without growing.</summary>
    /// <param name="capacity">The minimum capacity to ensure.</param>
    /// <returns>The new capacity of this instance.</returns>
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

    Entry[] ResizeCount(int capacity)
    {
        var newEntries = new Entry[capacity];
        var count = _count;
        var entries = _entries;
        for (int i = 0; i < count;)
        {
            var bucketIndex = (entries[i].Item.GetHashCode() * FIBONACCI_HASH) & (newEntries.Length - 1);
            newEntries[i].Next = newEntries[bucketIndex].Bucket - 1;
            newEntries[i].Item = entries[i].Item;
            newEntries[bucketIndex].Bucket = ++i;
        }
        return _entries = newEntries;
    }
}