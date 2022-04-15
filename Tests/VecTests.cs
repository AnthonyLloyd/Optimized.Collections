using CsCheck;
using Optimized.Collections;
using Xunit;

namespace Tests;

public class VecTests
{
    readonly Action<string> writeLine;
    public VecTests(Xunit.Abstractions.ITestOutputHelper output) => writeLine = output.WriteLine;

    [Fact]
    public void Add_ModelBased()
    {
        Gen.Int.Array.Select(a => (new Vec<int>(a), new List<int>(a)))
        .SampleModelBased(
            Gen.Int.Operation<Vec<int>, List<int>>((ls, l, i) =>
            {
                ls.Add(i);
                l.Add(i);
            })
        );
    }

    [Fact(Skip = "Close")]
    public void Add_Performance()
    {
        Gen.Int.Array
        .Faster(
            items =>
            {
                var vec = new Vec<int>();
                foreach (var i in items) vec.Add(i);
            },
            items =>
            {
                var list = new List<int>();
                foreach (var i in items) list.Add(i);
            }
        ).Output(writeLine);
    }

    [Fact]
    public void Concurrency()
    {
        Gen.Byte.Array.Select(a => new Vec<byte>(a))
        .SampleConcurrent(
            Gen.Byte.Operation<Vec<byte>>((l, i) => { lock (l) l.Add(i); }),
            Gen.Int.NonNegative.Operation<Vec<byte>>((l, i) => { if (i < l.Count) { byte _ = l[i]; } }),
            Gen.Int.NonNegative.Select(Gen.Byte).Operation<Vec<byte>>((l, t) => { if (t.V0 < l.Count) l[t.V0] = t.V1; }),
            Gen.Operation<Vec<byte>>(l => l.ToArray())
        );
    }
}