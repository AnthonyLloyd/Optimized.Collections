using System.Collections;
using System.Runtime.CompilerServices;

namespace Optimized.Collections;

/// <summary>Represents a strongly typed grow only set of values that can be accessed by index.</summary>
/// <remarks>
/// - Lock free for reads during modification for reference types (and value types that set atomically).<br/>
/// - Better performance than <see cref="HashSet{T}"/> in general.<br/>
/// </remarks>
/// <typeparam name="T">The type of elements in the set.</typeparam>
public sealed class Set<T> :
#if NET6_0
    IReadOnlySet<T>,
#endif
    IReadOnlyList<T> where T : IEquatable<T>
{
    struct Entry { internal int Bucket; internal int Next; internal T Item; }
    static class Holder { internal static Entry[] Initial = new Entry[1]; }
    int _count;
    Entry[] _entries;

    /// <summary>Initializes a new instance of the <see cref="Set{T}"/> class that is empty</summary>
    public Set() => _entries = Holder.Initial;

    /// <summary>Initializes a new instance of the <see cref="Set{T}"/> class that contains elements copied from the specified collection.</summary>
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

    /// <summary>Adds the specified element to the <see cref="Set{T}"/>.</summary>
    /// <param name="item">The object to add to the <see cref="Set{T}"/>.</param>
    public int Add(T item)
    {
        var entries = _entries;
        var hashCode = item.GetHashCode();
        var i = entries[hashCode & (entries.Length - 1)].Bucket - 1;
        while (i >= 0 && !item.Equals(entries[i].Item)) i = entries[i].Next;
        return i >= 0 ? i : AddItem(item, hashCode);
    }

    /// <summary>Searches for the specified object and returns the zero-based index.</summary>
    /// <param name="item">The object to locate in the <see cref="Set{T}"/>.</param>
    /// <returns>The zero-based index of the item within the <see cref="Set{T}"/>, if found; otherwise, –1.</returns>
    public int IndexOf(T item)
    {
        var entries = _entries;
        var hashCode = item.GetHashCode();
        var i = entries[hashCode & (entries.Length - 1)].Bucket - 1;
        while (i >= 0 && !item.Equals(entries[i].Item)) i = entries[i].Next;
        return i;
    }

    /// <summary>Determines whether a <see cref="Set{T}"/> contains the specified item.</summary>
    /// <param name="item">The object to locate in the <see cref="Set{T}"/>.</param>
    /// <returns>true if the item is found in the <see cref="Set{T}"/>; otherwise, false.</returns>
    public bool Contains(T item) => IndexOf(item) >= 0;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="equalValue"></param>
    /// <param name="actualValue"></param>
    /// <returns></returns>
    public bool TryGetValue(T equalValue, out T actualValue)
    {
        throw new NotImplementedException();
    }

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

    /// <summary>
    /// 
    /// </summary>
    /// <param name="other"></param>
    /// <returns></returns>
    public bool IsProperSubsetOf(IEnumerable<T> other)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="other"></param>
    /// <returns></returns>
    public bool IsProperSupersetOf(IEnumerable<T> other)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="other"></param>
    /// <returns></returns>
    public bool IsSubsetOf(IEnumerable<T> other)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="other"></param>
    /// <returns></returns>
    public bool IsSupersetOf(IEnumerable<T> other)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="other"></param>
    /// <returns></returns>
    public bool Overlaps(IEnumerable<T> other)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="other"></param>
    /// <returns></returns>
    public bool SetEquals(IEnumerable<T> other)
    {
        throw new NotImplementedException();
    }

    /// <summary>Copies the elements of a <see cref="Set{T}"/> object to an array.</summary>
    /// <param name="array">The one-dimensional array that is the destination of the elements copied from the <see cref="Set{T}"/> object. The array must have zero-based indexing.</param>
    public void CopyTo(T[] array)
    {
        throw new NotImplementedException();
    }

    /// <summary>Copies the elements of a <see cref="Set{T}"/> object to an array, starting at the specified array index.</summary>
    /// <param name="array">The one-dimensional array that is the destination of the elements copied from the <see cref="Set{T}"/> object. The array must have zero-based indexing.</param>
    /// <param name="arrayIndex">The zero-based index in array at which copying begins.</param>
    public void CopyTo(T[] array, int arrayIndex)
    {
        throw new NotImplementedException();
    }

    /// <summary>Copies the specified number of elements of a <see cref="Set{T}"/> object to an array, starting at the specified array index.</summary>
    /// <param name="array">The one-dimensional array that is the destination of the elements copied from the <see cref="Set{T}"/> object. The array must have zero-based indexing.</param>
    /// <param name="arrayIndex">The zero-based index in array at which copying begins.</param>
    /// <param name="count">The number of elements to copy to array.</param>
    public void CopyTo(T[] array, int arrayIndex, int count)
    {
        throw new NotImplementedException();
    }

    /// <summary>Ensures that this set can hold the specified number of elements without growing.</summary>
    /// <param name="capacity">The minimum capacity to ensure.</param>
    /// <returns>The new capacity of this instance.</returns>
    public int EnsureCapacity(int capacity)
    {
        throw new NotImplementedException();
    }

    /// <summary>Sets the capacity of a <see cref="Set{T}"/> object to the actual number of elements it contains, rounded up to a nearby, implementation-specific value.</summary>
    public void TrimExcess()
    {
        throw new NotImplementedException();
    }
}