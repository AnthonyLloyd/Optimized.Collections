namespace Optimized.Collections;

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

/// <summary>Represents a strongly typed grow only collection of keys and values.</summary>
/// <remarks>
/// - Lock free for reads during modification for reference types (and value types that set atomically).<br/>
/// - Better performance than <see cref="Dictionary{K, V}"/> in general.<br/>
/// </remarks>
/// <typeparam name="K">The type of the keys in the <see cref="Map{K, V}"/>.</typeparam>
/// <typeparam name="V">The type of the values in the <see cref="Map{K, V}"/>.</typeparam>
[DebuggerTypeProxy(typeof(MapDebugView<,>))]
[DebuggerDisplay("Count = {Count}")]
public ref struct MapSpan<K, V> where K : IEquatable<K>
{
    const int FIBONACCI_HASH = -1640531527;
    readonly ref Map<K, V>.Entry _entries;
    readonly int _mask;

    /// <summary>ss</summary>
    /// <param name="entries"></param>
    /// <param name="count"></param>
    internal MapSpan(Map<K, V>.Entry[] entries, int count)
    {
        _entries = ref MemoryMarshal.GetArrayDataReference(entries);
        _mask = entries.Length - 1;
        Count = count;
    }

    /// <summary>Gets the number of key/value pairs contained in the <see cref="Map{K, V}"/>.</summary>
    /// <returns>The number of key/value pairs contained in the <see cref="Map{K, V}"/>.</returns>
    public int Count { get; }

    /// <summary>Gets or sets the value associated with the specified key.</summary>
    /// <param name="key">The key of the value to get or set.</param>
    /// <returns>The value associated with the specified key. If the specified key is not found, a get operation throws a <see cref="KeyNotFoundException"/>, and a set operation creates a new element with the specified key.</returns>
    public readonly V this[K key]
    {
        get
        {
            var i = Unsafe.Add(ref _entries, (key.GetHashCode() * FIBONACCI_HASH) & _mask).Bucket - 1;
            while (i >= 0)
            {
                ref var entry = ref Unsafe.Add(ref _entries, i);
                if (entry.Key.Equals(key))
                    return entry.Value;
                i = entry.Next;
            }
            throw new KeyNotFoundException();
        }
    }

    /// <summary>Gets the value associated with the specified key.</summary>
    /// <param name="key">The key of the value to get.</param>
    /// <param name="value">When this method returns, contains the value associated with the specified key, if the key is found; otherwise, the default value for the type of the value parameter. This parameter is passed uninitialized.</param>
    /// <returns>true if the <see cref="Map{K, V}"/> contains an element with the specified key; otherwise, false.</returns>
    public readonly bool TryGetValue(K key, [MaybeNullWhen(false)] out V value)
    {
        var i = Unsafe.Add(ref _entries, (key.GetHashCode() * FIBONACCI_HASH) & _mask).Bucket - 1;
        while (i >= 0)
        {
            ref var entry = ref Unsafe.Add(ref _entries, i);
            if (entry.Key.Equals(key))
            {
                value = entry.Value;
                return true;
            }
            i = entry.Next;
        }
        value = default;
        return false;
    }
}