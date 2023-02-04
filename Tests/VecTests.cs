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
            Gen.Int.Operation<Vec<int>, List<int>>((vec, list, i) =>
            {
                vec.Add(i);
                list.Add(i);
            })
        );
    }

    [Fact]
    public void AddNoExcess_ModelBased()
    {
        Gen.Int.Array.Select(a => (new Vec<int>(a), new List<int>(a)))
        .SampleModelBased(
            Gen.Int.Operation<Vec<int>, List<int>>((vec, list, i) =>
            {
                vec.AddNoExcess(i);
                list.Add(i);
            })
        );
    }

    [Fact]
    public void Set_ModelBased()
    {
        Gen.Int.Array.Select(a => (new Vec<int>(a), new List<int>(a)))
        .SampleModelBased(
            Gen.Int[0, 100].Operation<Vec<int>, List<int>>((vec, list, i) =>
            {
                if (i < vec.Count)
                    vec[i] = i;
                else
                    vec.Add(i);

                if (i < list.Count)
                    list[i] = i;
                else
                    list.Add(i);
            })
        );
    }

    [Fact]
    public void AddRange_ModelBased()
    {
        Gen.Int.Array.Select(a => (new Vec<int>(a), new List<int>(a)))
        .SampleModelBased(
            Gen.Int.HashSet.Operation<Vec<int>, List<int>>((vec, list, i) =>
            {
                vec.AddRange(i);
                list.AddRange(i);
            })
        );
    }

    [Fact]
    public void IndexOf()
    {
        Gen.Int.Array.Select(Gen.Int, (a, i) => (new Vec<int>(a), new List<int>(a), i))
        .Sample((vec, list, i) => vec.IndexOf(i) == list.IndexOf(i));
    }

    [Fact]
    public void LastIndexOf()
    {
        Gen.Int.Array.Select(Gen.Int, (a, i) => (new Vec<int>(a), new List<int>(a), i))
        .Sample((vec, list, i) => vec.LastIndexOf(i) == list.LastIndexOf(i));
    }

    [Fact(Skip ="Close")]
    public void Add_Performance()
    {
        Gen.Int.Array[5, 50]
        .Faster(
            items =>
            {
                var vec = new Vec<int>();
                for (int i = 0; i < items.Length; i++) vec.Add(items[i]);
            },
            items =>
            {
                var list = new List<int>();
                for (int i = 0; i < items.Length; i++) list.Add(items[i]);
            }
        ).Output(writeLine);
    }

    [Fact]
    public void Concurrency()
    {
        Gen.Byte.Array.Select(a => new Vec<byte>(a))
        .SampleConcurrent(
            Gen.Byte.Operation<Vec<byte>>((v, i) => { lock (v) v.Add(i); }),
            Gen.Int.NonNegative.Operation<Vec<byte>>((v, i) => { if (i < v.Count) { _ = v[i]; } }),
            Gen.Int.NonNegative.Select(Gen.Byte).Operation<Vec<byte>>((v, t) => { if (t.V0 < v.Count) v[t.V0] = t.V1; }),
            Gen.Operation<Vec<byte>>(v => v.ToArray()),
            Gen.Operation<Vec<byte>>(v => _ = v.Capacity)
        );
    }
}