using CsCheck;
using Optimized.Collections;
using Xunit;

namespace Tests;

public class SetTests
{
    readonly Action<string> writeLine;
    public SetTests(Xunit.Abstractions.ITestOutputHelper output) => writeLine = output.WriteLine;

    [Fact]
    public void Add_ModelBased()
    {
        Gen.Int.HashSet
        .Select(d => (new Set<int>(d), new HashSet<int>(d)))
        .SampleModelBased(
            Gen.Int[0, 100].Operation<Set<int>, HashSet<int>>((set, hashset, i) =>
            {
                set.Add(i);
                hashset.Add(i);
            })
        );
    }

    [Fact]
    public void Add_Performance()
    {
        Gen.Int.Array
        .Faster(
            items =>
            {
                var set = new Set<int>();
                foreach (var i in items) set.Add(i);
            },
            items =>
            {
                var hashset = new HashSet<int>();
                foreach (var i in items) hashset.Add(i);
            }
        ).Output(writeLine);
    }

    [Fact]
    public void Contains_Performance()
    {
        Gen.Int.HashSet
        .Select(a => (a, new Set<int>(a), new HashSet<int>(a)))
        .Faster(
            (items, set, _) =>
            {
                foreach (var i in items) set.Contains(i);
            },
            (items, _, hashset) =>
            {
                foreach (var i in items) hashset.Contains(i);
            }
        ).Output(writeLine);
    }
}