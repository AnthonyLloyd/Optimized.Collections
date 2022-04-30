namespace Optimized.Collections;

using System.Collections;
using System.Diagnostics;

[DebuggerTypeProxy(typeof(IReadOnlyListDebugView<>))]
[DebuggerDisplay("Count = {Count}")]
internal class VecLink<T> : IReadOnlyList<T>
{
    public VecLink(T value) => Value = value;
    public VecLink<T>? Next;
    public T Value;

    public void Add(T t)
    {
        var node = this;
        while (node.Next is not null) node = node.Next;
        node.Next = new VecLink<T>(t);
    }
    
    public int Count
    {
        get
        {
            var count = 1;
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
                node = node!.Next;
            }
            return node!.Value;
        }
        set
        {
            var node = this;
            while (index != 0)
            {
                node = node!.Next;
            }
            node!.Value = value;
        }
    }
    

    public IEnumerator<T> GetEnumerator()
    {
        yield return Value;
        var node = Next;
        while (node is not null)
        {
            yield return node.Value;
            node = node.Next;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
