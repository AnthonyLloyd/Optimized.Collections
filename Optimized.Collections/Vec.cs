using System.Collections;
using System.Runtime.CompilerServices;

namespace Optimized.Collections;

/// <summary>Represents a strongly typed grow only list of objects that can be accessed by index.</summary>
/// <remarks>
/// - Lock free for reads during modification for reference types and value types that are set atomically.<br/>
/// - More control of memory use and excess capacity.<br/>
/// </remarks>
/// <typeparam name="T">The type of elements in the list.</typeparam>
public class Vec<T> : IReadOnlyList<T>
{
    static readonly T[] emptyArray = new T[0];
    T[] _items;
    int _count;

    /// <summary>Initializes a new instance of the <see cref="Vec{T}"/> class that is empty with no initial capacity.</summary>
    public Vec() => _items = emptyArray;

    /// <summary>Initializes a new instance of the <see cref="Vec{T}"/> that is empty and has the specified initial capacity.</summary>
    /// <param name="capacity">The number of elements that the new vec can initially store.</param>
    public Vec(int capacity) => _items = new T[capacity];

    /// <summary>Initializes a new instance of the <see cref="Vec{T}"/> class that contains elements copied from the specified collection and has sufficient capacity to accommodate the number of elements copied.</summary>
    /// <param name="collection">The collection whose elements are copied to the new list.</param>
    public Vec(IEnumerable<T> collection)
    {
        if (collection is ICollection<T> ts)
        {
            _items = new T[ts.Count];
            ts.CopyTo(_items, 0);
        }
        else _items = collection.ToArray();
        _count = _items.Length;
    }

    /// <summary>Gets the number of elements contained in the <see cref="Vec{T}"/>.</summary>
    /// <returns>The number of elements contained in the <see cref="Vec{T}"/>.</returns>
    public int Count => _count;

    /// <summary>Gets or sets the element at the specified index.</summary>
    /// <param name="index">The zero-based index of the element to get or set.</param>
    /// <returns>The element at the specified index.</returns>
    public T this[int index]
    {
        get => _items[index];
        set => _items[index] = value;
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

    /// <summary>Adds an object to the end of the <see cref="Vec{T}"/>. If required, the capacity of the list is doubled before adding the new element.</summary>
    /// <param name="item">The object to be added to the end of the <see cref="Vec{T}"/>.</param>
    public void Add(T item)
    {
        int count = _count;
        var items = _items;
        if ((uint)count < (uint)items.Length)
        {
            items[count] = item;
            _count = count + 1;
        }
        else AddWithResize(item, _count);
    }

    /// <summary>Adds an object to the end of the <see cref="Vec{T}"/>. If required, the capacity of the list is increase by one before adding the new element.</summary>
    /// <param name="item">The object to be added to the end of the <see cref="Vec{T}"/>.</param>
    public void AddNoExcess(T item)
    {
        int count = _count;
        var items = _items;
        if ((uint)count < (uint)items.Length)
        {
            items[count] = item;
            _count = count + 1;
        }
        else AddWithResize(item, 1);
    }

    /// <summary>Sets the capacity to the actual number of elements in the <see cref="Vec{T}"/>.</summary>
    public void TrimExcess()
    {
        Array.Resize(ref _items, _count);
    }

    /// <summary>Searches for the specified object and returns the zero-based index of the first occurrence within the entire <see cref="Vec{T}"/>.</summary>
    /// <param name="item">The object to locate in the <see cref="Vec{T}"/>. The value can be null for reference types.</param>
    /// <returns>The zero-based index of the first occurrence of item within the entire <see cref="Vec{T}"/>, if found; otherwise, –1.</returns>
    public int IndexOf(T item)
    {
        var count = _count;
        return Array.IndexOf(_items, item, 0, count);
    }

    /// <summary>Searches for the specified object and returns the zero-based index of the first occurrence within the range of elements in the <see cref="Vec{T}"/> that extends from the specified index to the last element.</summary>
    /// <param name="item">The object to locate in the <see cref="Vec{T}"/>. The value can be null for reference types.</param>
    /// <param name="index">The zero-based starting index of the search. 0 (zero) is valid in an empty list.</param>
    /// <returns>The zero-based index of the first occurrence of item within the range of elements in the <see cref="Vec{T}"/> that extends from index to the last element, if found; otherwise, –1.</returns>
    public int IndexOf(T item, int index)
    {
        var count = _count;
        return Array.IndexOf(_items, item, index, count - index);
    }

    /// <summary>Searches for the specified object and returns the zero-based index of the first occurrence within the range of elements in the <see cref="Vec{T}"/> that starts at the specified index and contains the specified number of elements.</summary>
    /// <param name="item">The object to locate in the <see cref="Vec{T}"/>. The value can be null for reference types.</param>
    /// <param name="index">The zero-based starting index of the search. 0 (zero) is valid in an empty list.</param>
    /// <param name="count">The number of elements in the section to search.</param>
    /// <returns>The zero-based index of the first occurrence of item within the range of elements in the <see cref="Vec{T}"/> that starts at index and contains count number of elements, if found; otherwise, –1.</returns>
    public int IndexOf(T item, int index, int count)
    {
        return Array.IndexOf(_items, item, index, count);
    }

    /// <summary>Searches for the specified object and returns the zero-based index of the last occurrence within the entire <see cref="Vec{T}"/>.</summary>
    /// <param name="item">The object to locate in the <see cref="Vec{T}"/>. The value can be null for reference types.</param>
    /// <returns>The zero-based index of the last occurrence of item within the entire the <see cref="Vec{T}"/>, if found; otherwise, –1.</returns>
    public int LastIndexOf(T item)
    {
        var count = _count;
        return count == 0 ? -1 : Array.LastIndexOf(_items, item, count - 1, count);
    }

    /// <summary>Searches for the specified object and returns the zero-based index of the last occurrence within the range of elements in the <see cref="Vec{T}"/> that extends from the first element to the specified index.</summary>
    /// <param name="item">The object to locate in the <see cref="Vec{T}"/>. The value can be null for reference types.</param>
    /// <param name="index">The zero-based starting index of the backward search.</param>
    /// <returns>The zero-based index of the last occurrence of item within the range of elements in the <see cref="Vec{T}"/> that extends from the first element to index, if found; otherwise, –1.</returns>
    public int LastIndexOf(T item, int index)
    {
        return Array.LastIndexOf(_items, item, index, index + 1);
    }

    /// <summary>Searches for the specified object and returns the zero-based index of the last occurrence within the range of elements in the <see cref="Vec{T}"/> that contains the specified number of elements and ends at the specified index.</summary>
    /// <param name="item">The object to locate in the <see cref="Vec{T}"/>. The value can be null for reference types.</param>
    /// <param name="index">The zero-based starting index of the backward search.</param>
    /// <param name="count">The number of elements in the section to search.</param>
    /// <returns>The zero-based index of the last occurrence of item within the range of elements in the <see cref="Vec{T}"/> that contains count number of elements and ends at index, if found; otherwise, –1.</returns>
    public int LastIndexOf(T item, int index, int count)
    {
        return Array.LastIndexOf(_items, item, index, count);
    }

    /// <summary>Copies the elements of the <see cref="Vec{T}"/> to a new array.</summary>
    /// <returns>An array containing copies of the elements of the <see cref="Vec{T}"/>.</returns>
    public T[] ToArray()
    {
        int count = _count;
        var array = new T[count];
        Array.Copy(_items, array, count);
        return array;
    }

    /// <summary>Copies the elements of the <see cref="Vec{T}"/> to a new <see cref="List{T}"/>.</summary>
    /// <returns>An <see cref="List{T}"/> containing copies of the elements of the <see cref="Vec{T}"/>.</returns>
    public List<T> ToList()
    {
        int count = _count;
        var list = new List<T>(count);
        for (int i = 0; i < count; i++)
            list.Add(_items[i]);
        return list;
    }

    /// <summary>Returns an enumerator that iterates through the <see cref="Vec{T}"/>.</summary>
    /// <returns>An enumerator for the <see cref="Vec{T}"/>.</returns>
    public IEnumerator<T> GetEnumerator()
    {
        var count = _count;
        var items = _items;
        for (int i = 0; i < count; i++)
            yield return items[i];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}