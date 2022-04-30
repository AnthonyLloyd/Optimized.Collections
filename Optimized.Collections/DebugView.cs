namespace Optimized.Collections;

using System.Diagnostics;

internal sealed class IReadOnlyListDebugView<T>
{
    private readonly IReadOnlyList<T> _list;

    public IReadOnlyListDebugView(IReadOnlyList<T> list)
    {
        _list = list;
    }

    [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
    public IReadOnlyList<T> Items
    {
        get
        {
            var array = new T[_list.Count];
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = _list[i];
            }
            return array;
        }
    }
}

internal sealed class MapDebugView<K, V> where K : IEquatable<K>
{
    private readonly IReadOnlyList<KeyValuePair<K, V>> _map;

    public MapDebugView(Map<K, V> map)
    {
        _map = map;
    }

    [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
    public IReadOnlyList<KeyValuePair<K, V>> Items
    {
        get
        {
            var array = new KeyValuePair<K, V>[_map.Count];
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = _map[i];
            }
            return array;
        }
    }
}