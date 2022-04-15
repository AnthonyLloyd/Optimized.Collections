using System.Collections;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Optimized.Collections;

/// <summary>Represents a syncronised version of <see cref="Vec{T}"/>.</summary>
/// <remarks>
/// - Lock free for reads for reference types (and value types that set atomically).<br/>
/// - Read lock during set allowing multiple in parallel.<br/>
/// - Write lock during Add.<br/>
/// - More control of memory use and excess capacity.<br/>
/// - Much better performance than <see cref="ConcurrentBag{T}"/> in general.<br/>
/// </remarks>
/// <typeparam name="T">The type of elements in the list.</typeparam>
public sealed class VecSync<T> : IReadOnlyList<T>
{
    static readonly T[] s_emptyArray = new T[0];
    readonly ReaderWriterLockSlim _lock = new();
    int _count;
    T[] _items;

    /// <summary>Initializes a new instance of the <see cref="VecSync{T}"/> class that is empty with no initial capacity.</summary>
    public VecSync() => _items = s_emptyArray;

    /// <summary>Initializes a new instance of the <see cref="VecSync{T}"/> that is empty and has the specified initial capacity.</summary>
    /// <param name="capacity">The number of elements that the new <see cref="VecSync{T}"/> can initially store.</param>
    public VecSync(int capacity) => _items = new T[capacity];

    /// <summary>Initializes a new instance of the <see cref="VecSync{T}"/> class that contains elements copied from the specified collection and has sufficient capacity to accommodate the number of elements copied.</summary>
    /// <param name="collection">The collection whose elements are copied to the new list.</param>
    public VecSync(IEnumerable<T> collection)
    {
        if (collection is ICollection<T> ts)
        {
            _items = new T[ts.Count];
            ts.CopyTo(_items, 0);
        }
        else _items = collection.ToArray();
        _count = _items.Length;
    }

    /// <summary>Gets the number of elements contained in the <see cref="VecSync{T}"/>.</summary>
    /// <returns>The number of elements contained in the <see cref="VecSync{T}"/>.</returns>
    public int Count => _count;

    /// <summary>Gets or sets the total number of elements the internal data structure can hold without resizing.</summary>
    /// <returns>The number of elements that the <see cref="VecSync{T}"/> can contain before resizing is required.</returns>
    public int Capacity
    {
        get => _items.Length;
        set
        {
            if (value == _items.Length) return;
            _lock.EnterWriteLock();
            if (value < _count)
            {
                _lock.ExitWriteLock();
                ThrowHelper.CannotReduceCapacityBelowCount();
            }
            if (value == 0)
            {
                _items = s_emptyArray;
            }
            else
            {
                var newItems = new T[value];
                Array.Copy(_items, newItems, _count);
                _items = newItems;
            }
            _lock.ExitWriteLock();
        }
    }

    /// <summary>Gets or sets the element at the specified index.</summary>
    /// <param name="index">The zero-based index of the element to get or set.</param>
    /// <returns>The element at the specified index.</returns>
    public T this[int index]
    {
        get => _items[index];
        set
        {
            _lock.EnterReadLock();
            if ((uint)index >= (uint)_count)
            {
                _lock.ExitReadLock();
                ThrowHelper.CannotReduceCapacityBelowCount();
            }
            _items[index] = value;
            _lock.ExitReadLock();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    void AddWithResize(T item, int expand)
    {
        if (_count == 0)
        {
            _items = new T[] { item };
            _count = 1;
        }
        else
        {
            var new_items = new T[_count + expand];
            Array.Copy(_items, new_items, _count);
            new_items[_count] = item;
            _items = new_items;
            _count++;
        }
    }

    /// <summary>Adds an object to the end of the <see cref="VecSync{T}"/>. If required, the capacity of the list is doubled before adding the new element.</summary>
    /// <param name="item">The object to be added to the end of the <see cref="VecSync{T}"/>.</param>
    public void Add(T item)
    {
        _lock.EnterWriteLock();
        int count = _count;
        var items = _items;
        if ((uint)count < (uint)items.Length)
        {
            items[count] = item;
            _count = count + 1;
        }
        else AddWithResize(item, _count);
        _lock.ExitWriteLock();
    }

    /// <summary>Adds an object to the end of the <see cref="VecSync{T}"/>. If required, the capacity of the list is increase by one before adding the new element.</summary>
    /// <param name="item">The object to be added to the end of the <see cref="VecSync{T}"/>.</param>
    public void AddNoExcess(T item)
    {
        _lock.EnterWriteLock();
        int count = _count;
        var items = _items;
        if ((uint)count < (uint)items.Length)
        {
            items[count] = item;
            _count = count + 1;
        }
        else AddWithResize(item, 1);
        _lock.ExitWriteLock();
    }

    /// <summary>Adds the elements of the specified collection to the end of the <see cref="VecSync{T}"/>.</summary>
    /// <param name="collection">The collection whose elements should be added to the end of the <see cref="VecSync{T}"/>. The collection itself cannot be null, but it can contain elements that are null, if type T is a reference type.</param>
    public void AddRange(IEnumerable<T> collection)
    {
        _lock.EnterWriteLock();
        if (collection is ICollection<T> c)
        {
            var newCount = _count + c.Count;
            if (newCount > _items.Length) Array.Resize(ref _items, newCount);
            c.CopyTo(_items, _count);
            _count = newCount;
        }
        else if (collection is IReadOnlyCollection<T> r)
        {
            var newCount = _count + r.Count;
            if (newCount > _items.Length) Array.Resize(ref _items, newCount);
            using var e = r.GetEnumerator();
            for (int i = _count; i < newCount; i++)
            {
                e.MoveNext();
                _items[i] = e.Current;
            }
            _count = newCount;
        }
        else
        {
            foreach (var t in collection)
                Add(t);
        }
        _lock.ExitWriteLock();
    }

    /// <summary>Sets the capacity to the actual number of elements in the <see cref="VecSync{T}"/>.</summary>
    public void TrimExcess()
    {
        _lock.EnterWriteLock();
        if (_count == 0)
            _items = s_emptyArray;
        else
            Array.Resize(ref _items, _count);
        _lock.ExitWriteLock();
    }

    /// <summary>Determines whether an element is in the <see cref="VecSync{T}"/>.</summary>
    /// <param name="item">The object to locate in the <see cref="VecSync{T}"/>. The value can be null for reference types.</param>
    /// <returns>true if item is found in the <see cref="VecSync{T}"/>; otherwise, false.</returns>
    public bool Contains(T item)
        => IndexOf(item) >= 0;

    /// <summary>Searches for the specified object and returns the zero-based index of the first occurrence within the entire <see cref="VecSync{T}"/>.</summary>
    /// <param name="item">The object to locate in the <see cref="VecSync{T}"/>. The value can be null for reference types.</param>
    /// <returns>The zero-based index of the first occurrence of item within the entire <see cref="VecSync{T}"/>, if found; otherwise, –1.</returns>
    public int IndexOf(T item)
    {
        var count = _count;
        return Array.IndexOf(_items, item, 0, count);
    }

    /// <summary>Searches for the specified object and returns the zero-based index of the first occurrence within the range of elements in the <see cref="VecSync{T}"/> that extends from the specified index to the last element.</summary>
    /// <param name="item">The object to locate in the <see cref="VecSync{T}"/>. The value can be null for reference types.</param>
    /// <param name="index">The zero-based starting index of the search. 0 (zero) is valid in an empty list.</param>
    /// <returns>The zero-based index of the first occurrence of item within the range of elements in the <see cref="VecSync{T}"/> that extends from index to the last element, if found; otherwise, –1.</returns>
    public int IndexOf(T item, int index)
    {
        var count = _count;
        return Array.IndexOf(_items, item, index, count - index);
    }

    /// <summary>Searches for the specified object and returns the zero-based index of the first occurrence within the range of elements in the <see cref="VecSync{T}"/> that starts at the specified index and contains the specified number of elements.</summary>
    /// <param name="item">The object to locate in the <see cref="VecSync{T}"/>. The value can be null for reference types.</param>
    /// <param name="index">The zero-based starting index of the search. 0 (zero) is valid in an empty list.</param>
    /// <param name="count">The number of elements in the section to search.</param>
    /// <returns>The zero-based index of the first occurrence of item within the range of elements in the <see cref="VecSync{T}"/> that starts at index and contains count number of elements, if found; otherwise, –1.</returns>
    public int IndexOf(T item, int index, int count)
    {
        return Array.IndexOf(_items, item, index, count);
    }

    /// <summary>Searches for the specified object and returns the zero-based index of the last occurrence within the entire <see cref="VecSync{T}"/>.</summary>
    /// <param name="item">The object to locate in the <see cref="VecSync{T}"/>. The value can be null for reference types.</param>
    /// <returns>The zero-based index of the last occurrence of item within the entire the <see cref="VecSync{T}"/>, if found; otherwise, –1.</returns>
    public int LastIndexOf(T item)
    {
        var count = _count;
        return count == 0 ? -1 : Array.LastIndexOf(_items, item, count - 1, count);
    }

    /// <summary>Searches for the specified object and returns the zero-based index of the last occurrence within the range of elements in the <see cref="VecSync{T}"/> that extends from the first element to the specified index.</summary>
    /// <param name="item">The object to locate in the <see cref="VecSync{T}"/>. The value can be null for reference types.</param>
    /// <param name="index">The zero-based starting index of the backward search.</param>
    /// <returns>The zero-based index of the last occurrence of item within the range of elements in the <see cref="VecSync{T}"/> that extends from the first element to index, if found; otherwise, –1.</returns>
    public int LastIndexOf(T item, int index)
    {
        return Array.LastIndexOf(_items, item, index, index + 1);
    }

    /// <summary>Searches for the specified object and returns the zero-based index of the last occurrence within the range of elements in the <see cref="VecSync{T}"/> that contains the specified number of elements and ends at the specified index.</summary>
    /// <param name="item">The object to locate in the <see cref="VecSync{T}"/>. The value can be null for reference types.</param>
    /// <param name="index">The zero-based starting index of the backward search.</param>
    /// <param name="count">The number of elements in the section to search.</param>
    /// <returns>The zero-based index of the last occurrence of item within the range of elements in the <see cref="VecSync{T}"/> that contains count number of elements and ends at index, if found; otherwise, –1.</returns>
    public int LastIndexOf(T item, int index, int count)
    {
        return Array.LastIndexOf(_items, item, index, count);
    }

    /// <summary>Converts the elements in the current <see cref="VecSync{T}"/> to another type, and returns a list containing the converted elements.</summary>
    /// <typeparam name="TOutput">The type of the elements of the target <see cref="VecSync{T}"/>.</typeparam>
    /// <param name="converter">A <see cref="Converter{T, TOutput}"/> delegate that converts each element from one type to another type.</param>
    /// <returns>A <see cref="VecSync{T}"/> of the target type containing the converted elements from the current <see cref="VecSync{T}"/>.</returns>
    public VecSync<TOutput> ConvertAll<TOutput>(Converter<T, TOutput> converter)
    {
        var count = _count;
        var items = _items;
        var vec = new VecSync<TOutput>(count);
        for (int i = 0; i < count; i++)
        {
            vec._items[i] = converter(items[i]);
        }
        vec._count = count;
        return vec;
    }

    /// <summary>Copies the entire <see cref="VecSync{T}"/> to a compatible one-dimensional array, starting at the beginning of the target array.</summary>
    /// <param name="array">The one-dimensional System.Array that is the destination of the elements copied from <see cref="VecSync{T}"/>. The System.Array must have zero-based indexing.</param>
    public void CopyTo(T[] array)
    {
        var count = _count;
        Array.Copy(_items, array, count);
    }

    /// <summary>Copies the entire <see cref="VecSync{T}"/> to a compatible one-dimensional array, starting at the specified index of the target array.</summary>
    /// <param name="array">The one-dimensional array that is the destination of the elements copied from <see cref="VecSync{T}"/>. The System.Array must have zero-based indexing.</param>
    /// <param name="arrayIndex">The zero-based index in array at which copying begins.</param>
    public void CopyTo(T[] array, int arrayIndex)
    {
        var count = _count;
        Array.Copy(_items, 0, array, arrayIndex, count);
    }

    /// <summary>Copies a range of elements from the <see cref="VecSync{T}"/> to a compatible one-dimensional array, starting at the specified index of the target array.</summary>
    /// <param name="index">The zero-based index in the source <see cref="VecSync{T}"/> at which copying begins.</param>
    /// <param name="array">The one-dimensional array that is the destination of the elements copied from <see cref="VecSync{T}"/>. The System.Array must have zero-based indexing.</param>
    /// <param name="arrayIndex">The zero-based index in array at which copying begins.</param>
    /// <param name="count">The number of elements to copy.</param>
    public void CopyTo(int index, T[] array, int arrayIndex, int count)
    {
        Array.Copy(_items, index, array, arrayIndex, count);
    }

    /// <summary>Searches for an element that matches the conditions defined by the specified predicate, and returns the first occurrence within the entire <see cref="VecSync{T}"/>.</summary>
    /// <param name="match">The <see cref="Predicate{T}"/> delegate that defines the conditions of the element to search for.</param>
    /// <returns>The first element that matches the conditions defined by the specified predicate, if found; otherwise, the default value for type T.</returns>
    public T? Find(Predicate<T> match)
    {
        var count = _count;
        var items = _items;
        for (int i = 0; i < count; i++)
        {
            if (match(items[i]))
            {
                return items[i];
            }
        }
        return default;
    }

    /// <summary>Retrieves all the elements that match the conditions defined by the specified predicate.</summary>
    /// <param name="match">The <see cref="Predicate{T}"/> delegate that defines the conditions of the elements to search for.</param>
    /// <returns>A <see cref="VecSync{T}"/> containing all the elements that match the conditions defined by the specified predicate, if found; otherwise, an empty <see cref="VecSync{T}"/>.</returns>
    public Vec<T> FindAll(Predicate<T> match)
    {
        var count = _count;
        var items = _items;
        var vec = new Vec<T>();
        for (int i = 0; i < count; i++)
        {
            if (match(items[i]))
            {
                vec.Add(items[i]);
            }
        }
        return vec;
    }

    /// <summary>Searches for an element that matches the conditions defined by the specified predicate, and returns the zero-based index of the first occurrence within the range of elements in the <see cref="VecSync{T}"/> that starts at the specified index and contains the specified number of elements.</summary>
    /// <param name="startIndex">The zero-based starting index of the search.</param>
    /// <param name="count">The number of elements in the section to search.</param>
    /// <param name="match">The <see cref="Predicate{T}"/> delegate that defines the conditions of the element to search for.</param>
    /// <returns>The zero-based index of the first occurrence of an element that matches the conditions defined by match, if found; otherwise, –1.</returns>
    public int FindIndex(int startIndex, int count, Predicate<T> match)
    {
        var items = _items;
        int endIndex = startIndex + count;
        for (int i = startIndex; i < endIndex; i++)
        {
            if (match(items[i])) return i;
        }
        return -1;
    }

    /// <summary>Searches for an element that matches the conditions defined by the specified predicate, and returns the zero-based index of the first occurrence within the range of elements in the <see cref="VecSync{T}"/> that extends from the specified index to the last element.</summary>
    /// <param name="startIndex">The zero-based starting index of the search.</param>
    /// <param name="match">The <see cref="Predicate{T}"/> delegate that defines the conditions of the element to search for.</param>
    /// <returns>The zero-based index of the first occurrence of an element that matches the conditions defined by match, if found; otherwise, –1.</returns>
    public int FindIndex(int startIndex, Predicate<T> match)
        => FindIndex(startIndex, _count - startIndex, match);

    /// <summary>Searches for an element that matches the conditions defined by the specified predicate, and returns the zero-based index of the first occurrence within the entire <see cref="VecSync{T}"/>.</summary>
    /// <param name="match">The <see cref="Predicate{T}"/> delegate that defines the conditions of the element to search for.</param>
    /// <returns>The zero-based index of the first occurrence of an element that matches the conditions defined by match, if found; otherwise, –1.</returns>
    public int FindIndex(Predicate<T> match)
        => FindIndex(0, _count, match);

    /// <summary>Determines whether the <see cref="VecSync{T}"/> contains elements that match the conditions defined by the specified predicate.</summary>
    /// <param name="match">The <see cref="Predicate{T}"/> delegate that defines the conditions of the elements to search for.</param>
    /// <returns>true if the <see cref="VecSync{T}"/> contains one or more elements that match the conditions defined by the specified predicate; otherwise, false.</returns>
    public bool Exists(Predicate<T> match)
        => FindIndex(match) != -1;

    /// <summary>Determines whether the <see cref="VecSync{T}"/> contains elements that match the conditions defined by the specified predicate.</summary>
    /// <param name="match">The <see cref="Predicate{T}"/> delegate that defines the conditions of the elements to search for.</param>
    /// <returns>true if the <see cref="VecSync{T}"/> contains one or more elements that match the conditions defined by the specified predicate; otherwise, false.</returns>
    public T? FindLast(Predicate<T> match)
    {
        var count = _count;
        var items = _items;
        for (int i = count - 1; i >= 0; i--)
        {
            if (match(items[i]))
            {
                return items[i];
            }
        }
        return default;
    }

    /// <summary>Searches for an element that matches the conditions defined by the specified predicate, and returns the zero-based index of the last occurrence within the range of elements in the <see cref="VecSync{T}"/> that contains the specified number of elements and ends at the specified index.</summary>
    /// <param name="startIndex">The zero-based starting index of the backward search.</param>
    /// <param name="count">The number of elements in the section to search.</param>
    /// <param name="match">The <see cref="Predicate{T}"/> delegate that defines the conditions of the element to search for.</param>
    /// <returns>The zero-based index of the last occurrence of an element that matches the conditions defined by match, if found; otherwise, –1.</returns>
    public int FindLastIndex(int startIndex, int count, Predicate<T> match)
    {
        if (count == 0) return -1;
        var items = _items;
        int endIndex = startIndex - count;
        for (int i = startIndex; i > endIndex; i--)
        {
            if (match(items[i]))
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>Searches for an element that matches the conditions defined by the specified predicate, and returns the zero-based index of the last occurrence within the entire <see cref="VecSync{T}"/>.</summary>
    /// <param name="match">The <see cref="Predicate{T}"/> delegate that defines the conditions of the element to search for.</param>
    /// <returns>The zero-based index of the last occurrence of an element that matches the conditions defined by match, if found; otherwise, –1.</returns>
    public int FindLastIndex(Predicate<T> match)
    {
        var count = _count;
        return FindLastIndex(count - 1, count, match);
    }

    /// <summary>Searches for an element that matches the conditions defined by the specified predicate, and returns the zero-based index of the last occurrence within the range of elements in the <see cref="VecSync{T}"/> that extends from the first element to the specified index.</summary>
    /// <param name="startIndex">The zero-based starting index of the backward search.</param>
    /// <param name="match">The <see cref="Predicate{T}"/> delegate that defines the conditions of the element to search for.</param>
    /// <returns>The zero-based index of the last occurrence of an element that matches the conditions defined by match, if found; otherwise, –1.</returns>
    public int FindLastIndex(int startIndex, Predicate<T> match)
        => FindLastIndex(startIndex, startIndex + 1, match);

    /// <summary>Performs the specified action on each element of the <see cref="VecSync{T}"/>.</summary>
    /// <param name="action">The <see cref="Action{T}"/> delegate to perform on each element of the <see cref="VecSync{T}"/>.</param>
    public void ForEach(Action<T> action)
    {
        var count = _count;
        var items = _items;
        for (int i = 0; i < count; i++)
        {
            action(items[i]);
        }
    }

    /// <summary>Determines whether every element in the <see cref="VecSync{T}"/> matches the conditions defined by the specified predicate.</summary>
    /// <param name="match">The <see cref="Predicate{T}"/> delegate that defines the conditions to check against the elements.</param>
    /// <returns>true if every element in the <see cref="VecSync{T}"/> matches the conditions defined by the specified predicate; otherwise, false. If the list has no elements, the return value is true.</returns>
    public bool TrueForAll(Predicate<T> match)
    {
        var count = _count;
        var items = _items;
        for (int i = 0; i < count; i++)
        {
            if (!match(items[i]))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>Creates a shallow copy of a range of elements in the source <see cref="VecSync{T}"/>.</summary>
    /// <param name="index">The zero-based <see cref="VecSync{T}"/> index at which the range starts.</param>
    /// <param name="count">The number of elements in the range.</param>
    /// <returns>A shallow copy of a range of elements in the source <see cref="VecSync{T}"/>.</returns>
    public VecSync<T> GetRange(int index, int count)
    {
        var vec = new VecSync<T>(count);
        Array.Copy(_items, index, vec._items, 0, count);
        vec._count = count;
        return vec;
    }

    /// <summary>Copies the elements of the <see cref="VecSync{T}"/> to a new array.</summary>
    /// <returns>An array containing copies of the elements of the <see cref="VecSync{T}"/>.</returns>
    public T[] ToArray()
    {
        int count = _count;
        var array = new T[count];
        Array.Copy(_items, array, count);
        return array;
    }

    /// <summary>Copies the elements of the <see cref="VecSync{T}"/> to a new <see cref="List{T}"/>.</summary>
    /// <returns>An <see cref="List{T}"/> containing copies of the elements of the <see cref="VecSync{T}"/>.</returns>
    public List<T> ToList()
    {
        int count = _count;
        var list = new List<T>(count);
        for (int i = 0; i < count; i++)
            list.Add(_items[i]);
        return list;
    }

    /// <summary>Returns an enumerator that iterates through the <see cref="VecSync{T}"/>.</summary>
    /// <returns>An enumerator for the <see cref="VecSync{T}"/>.</returns>
    public IEnumerator<T> GetEnumerator()
    {
        var count = _count;
        var items = _items;
        for (int i = 0; i < count; i++)
            yield return items[i];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}