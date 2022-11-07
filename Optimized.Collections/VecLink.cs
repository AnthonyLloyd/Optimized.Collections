﻿namespace Optimized.Collections;

using System.Collections;
using System.Diagnostics;

[DebuggerTypeProxy(typeof(IReadOnlyListDebugView<>))]
[DebuggerDisplay("Count = {Count}")]
internal class VecLink2<T> : IReadOnlyList<T>
{
    public VecLink2<T>? Next;
    public T? Value;

    public void Add(T t)
    {
        var node = this;
        while (node.Next is not null) node = node.Next;
        //lock (node)
        //{
        //    if (node.Next is null)
        //    {
                node.Value = t;
                node.Next = new();
        //    }
        //    else
        //    {
        //        node.Next.Add(t);
        //    }
        //}
    }

    public int Count
    {
        get
        {
            var count = 0;
            var node = Next;
            while (node is not null)
            {
                count++;
                node = node.Next;
            }
            return count;
        }
    }

    public T this[int index]
    {
        get
        {
            var node = this;
            while(index != 0)
            {
                node = node.Next!;
            }
            return node.Value!;
        }
        set
        {
            var node = this;
            while (index != 0)
            {
                node = node.Next!;
            }
            node.Value = value;
        }
    }

    public IEnumerator<T> GetEnumerator()
    {
        var node = this;
        while (node.Next is not null)
        {
            yield return node.Value!;
            node = node.Next;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
