using CsCheck;
using Optimized.Collections;
using Xunit;

namespace Tests;

public class SetTests(Xunit.Abstractions.ITestOutputHelper output)
{
    [Fact]
    public void Set_Add_ModelBased()
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
    public void Set_Add_Performance()
    {
        Gen.Int.Array
        .Faster(
            items =>
            {
                var set = new Set<int>();
                for(int i = 0; i < items.Length; i++) set.Add(items[i]);
            },
            items =>
            {
                var hashset = new HashSet<int>();
                for (int i = 0; i < items.Length; i++) hashset.Add(items[i]);
            }
        , threads: 1, writeLine: output.WriteLine);
    }

    [Fact]
    public void Set_Contains_Performance()
    {
        Gen.Select(Gen.Int[0, 1000], Gen.Int[0, 1000].HashSet)
        .Select((i, d) => (i, new Set<int>(d), new HashSet<int>(d)))
        .Faster(
            (i, set, _) => set.Contains(i),
            (i, _, hashset) => hashset.Contains(i)
        , repeat: 100, writeLine: output.WriteLine);
    }

    [Fact]
    public void Set_SetEquals()
    {
        Gen.Select(Gen.Int[1, 10].HashSet[0, 10], Gen.Int[1, 10].Array[0, 10])
        .Sample((hashSet, array) =>
        {
            var expected = hashSet.SetEquals(array);
            var set = new Set<int>(hashSet);
            var actual = set.SetEquals(array);
            if (actual != expected) return false;
            actual = set.SetEquals(new HashSet<int>(array));
            if (actual != expected) return false;
            actual = set.SetEquals(new Set<int>(array));
            return actual == expected;
        });
    }

    [Fact]
    public void Set_Overlaps()
    {
        Gen.Select(Gen.Int[1, 10].HashSet[0, 10], Gen.Int[1, 10].Array[0, 10])
        .Sample((hashSet, array) =>
        {
            var expected = hashSet.Overlaps(array);
            var set = new Set<int>(hashSet);
            var actual = set.Overlaps(array);
            if (actual != expected) return false;
            actual = set.Overlaps(new HashSet<int>(array));
            if (actual != expected) return false;
            actual = set.Overlaps(new Set<int>(array));
            return actual == expected;
        });
    }

    [Fact]
    public void Set_IsSupersetOf()
    {
        Gen.Select(Gen.Int[1, 10].HashSet[0, 10], Gen.Int[1, 10].Array[0, 10])
        .Sample((hashSet, array) =>
        {
            var expected = hashSet.IsSupersetOf(array);
            var set = new Set<int>(hashSet);
            var actual = set.IsSupersetOf(array);
            if (actual != expected) return false;
            actual = set.IsSupersetOf(new HashSet<int>(array));
            if (actual != expected) return false;
            actual = set.IsSupersetOf(new Set<int>(array));
            return actual == expected;
        });
    }

    [Fact]
    public void Set_IsProperSupersetOf()
    {
        Gen.Select(Gen.Int[1, 10].HashSet[0, 10], Gen.Int[1, 10].Array[0, 10])
        .Sample((hashSet, array) =>
        {
            var expected = hashSet.IsProperSupersetOf(array);
            var set = new Set<int>(hashSet);
            var actual = set.IsProperSupersetOf(array);
            if (actual != expected) return false;
            actual = set.IsProperSupersetOf(new HashSet<int>(array));
            if (actual != expected) return false;
            actual = set.IsProperSupersetOf(new Set<int>(array));
            return actual == expected;
        });
    }

    [Fact]
    public void Set_IsSubsetOf()
    {
        Gen.Select(Gen.Int[1, 10].HashSet[0, 10], Gen.Int[1, 10].Array[0, 10])
        .Sample((hashSet, array) =>
        {
            var expected = hashSet.IsSubsetOf(array);
            var set1 = new Set<int>(hashSet);
            var actual = set1.IsSubsetOf(array);
            if (actual != expected) return false;
            actual = set1.IsSubsetOf(new HashSet<int>(array));
            if (actual != expected) return false;
            actual = set1.IsSubsetOf(new Set<int>(array));
            return actual == expected;
        });
    }

    [Fact]
    public void Set_IsProperSubsetOf()
    {
        Gen.Select(Gen.Int[1, 10].HashSet[0, 10], Gen.Int[1, 10].Array[0, 10])
        .Sample((hashSet, array) =>
        {
            var expected = hashSet.IsProperSubsetOf(array);
            var set = new Set<int>(hashSet);
            var actual = set.IsProperSubsetOf(array);
            if (actual != expected) return false;
            actual = set.IsProperSubsetOf(new HashSet<int>(array));
            if (actual != expected) return false;
            actual = set.IsProperSubsetOf(new Set<int>(array));
            return actual == expected;
        });
    }
}