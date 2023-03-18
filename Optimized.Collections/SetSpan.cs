namespace Optimized.Collections;

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

/// <summary>Represents a strongly typed grow only set of values that can be accessed by index.</summary>
/// <remarks>
/// - Lock free for reads during modification for reference types (and value types that set atomically).<br/>
/// - 10-20% better performance than <see cref="HashSet{T}"/> in general.<br/>
/// </remarks>
/// <typeparam name="T">The type of elements in the set.</typeparam>
[DebuggerTypeProxy(typeof(IReadOnlyListDebugView<>))]
[DebuggerDisplay("Count = {Count}")]
public ref struct SetSpan<T> where T : IEquatable<T>
{
    const int FIBONACCI_HASH = -1640531527;
    /// <summary>aa</summary>
    readonly ref Set<T>.Entry _entries;
    readonly int _mask;

    /// <summary>ss</summary>
    /// <param name="entries"></param>
    /// <param name="count"></param>
    internal SetSpan(Set<T>.Entry[] entries, int count)
    {
        _entries = ref MemoryMarshal.GetArrayDataReference(entries);
        _mask = entries.Length - 1;
        Count = count;
    }

    /// <summary>Gets the number of elements that are contained in a <see cref="Set{T}"/>.</summary>
    /// <returns>The number of elements contained in the <see cref="Set{T}"/>.</returns>
    public int Count { get; }

    /// <summary>Gets or sets the element at the specified index.</summary>
    /// <param name="index">The zero-based index of the element to get or set.</param>
    /// <returns>The element at the specified index.</returns>
    public ref T this[int index] => ref Unsafe.Add(ref _entries, index).Item;

    /// <summary>Searches for the specified object and returns the zero-based index.</summary>
    /// <param name="item">The object to locate in the <see cref="Set{T}"/>.</param>
    /// <returns>The zero-based index of the item within the <see cref="Set{T}"/>, if found; otherwise, –1.</returns>
    public readonly int IndexOf(T item)
    {
        var i = Unsafe.Add(ref _entries, (item.GetHashCode() * FIBONACCI_HASH) & _mask).Bucket - 1;
        while (i >= 0)
        {
            if (Unsafe.Add(ref _entries, i).Item.Equals(item))
                return i;
            i = Unsafe.Add(ref _entries, i).Next;
        }
        return i;
    }

    /// <summary>Determines whether a <see cref="Set{T}"/> contains the specified item.</summary>
    /// <param name="item">The object to locate in the <see cref="Set{T}"/>.</param>
    /// <returns>true if the item is found in the <see cref="Set{T}"/>; otherwise, false.</returns>
    public readonly bool Contains(T item)
    {
        var i = Unsafe.Add(ref _entries, (item.GetHashCode() * FIBONACCI_HASH) & _mask).Bucket - 1;
        while (i >= 0)
        {
            if (Unsafe.Add(ref _entries, i).Item.Equals(item))
                return true;
            i = Unsafe.Add(ref _entries, i).Next;
        }
        return false;
    }

    /// <summary>Searches the <see cref="Set{T}"/> for a given value and returns the equal value it finds, if any.</summary>
    /// <param name="equalValue">The value to search for.</param>
    /// <param name="actualValue">The value from the <see cref="Set{T}"/> that the search found, or the default value of T when the search yielded no match.</param>
    public readonly bool TryGetValue(T equalValue, [MaybeNullWhen(false)] out T actualValue)
    {
        var i = Unsafe.Add(ref _entries, (equalValue.GetHashCode() * FIBONACCI_HASH) & _mask).Bucket - 1;
        while (i >= 0)
        {
            actualValue = Unsafe.Add(ref _entries, i).Item;
            if (actualValue.Equals(equalValue))
                return true;
            i = Unsafe.Add(ref _entries, i).Next;
        }
        actualValue = default;
        return false;
    }
}