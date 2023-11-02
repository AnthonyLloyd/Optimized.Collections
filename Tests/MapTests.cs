using CsCheck;
using Optimized.Collections;
using Xunit;

namespace Tests;

public class MapTests
{
    readonly Action<string> writeLine;
    public MapTests(Xunit.Abstractions.ITestOutputHelper output) => writeLine = output.WriteLine;

    [Fact]
    public void Map_Set_ModelBased()
    {
        Gen.Dictionary(Gen.Int, Gen.Byte)
        .Select(d => (new Map<int, byte>(d), new Dictionary<int, byte>(d)))
        .SampleModelBased(
            Gen.Select(Gen.Int[0, 100], Gen.Byte).Operation<Map<int, byte>, Dictionary<int, byte>>((map, dictionary, t) =>
            {
                map[t.Item1] = t.Item2;
                dictionary[t.Item1] = t.Item2;
            })
        );
    }

    [Fact]
    public void Map_Set_Performance()
    {
        Gen.Int.Select(Gen.Byte).Array
        .Faster(
            items =>
            {
                var map = new Map<int, byte>();
                foreach (var (k, v) in items) map[k] = v;
            },
            items =>
            {
                var dictionary = new Dictionary<int, byte>();
                foreach (var (k, v) in items) dictionary[k] = v;
            }
        ).Output(writeLine);
    }

    [Fact]
    public void Map_ContainsKey_Performance()
    {
        Gen.Select(Gen.Int[0, 1000], Gen.Dictionary(Gen.Int[0, 1000], Gen.Int))
        .Select((i, d) => (i, new Map<int, int>(d), new Dictionary<int, int>(d)))
        .Faster(
            (i, map, _) => map.ContainsKey(i),
            (i, _, dictionary) => dictionary.ContainsKey(i)
        , repeat: 100).Output(writeLine);
    }

    [Fact]
    public void Map_TryGetValue_Add()
    {
        Gen.Int.Array
        .Sample(ints =>
        {
            var map = new Map<int, int>();
            foreach (var i in ints)
            {
                if (!map.TryGetValue(i, out _))
                    map.Add(i, i);
            }
        });
    }
}