namespace Tests;

using System.Linq;
using CsCheck;
using Optimized.Collections;
using Xunit;

public class MapSpanTests(Xunit.Abstractions.ITestOutputHelper output)
{
    [Fact]
    public void MapSpan_TryGetValue_Performance()
    {
        Gen.Select(Gen.Int[0, 1000].Array[1000], Gen.Dictionary(Gen.Int[0, 1000], Gen.Int))
        .Select((i, d) => (i, new Map<int, int>(d), new Dictionary<int, int>(d)))
        .Faster(
            (ints, map, __) =>
            {
                var span = map.AsSpan();
                var count = 0;
                foreach (var i in ints)
                    if (span.TryGetValue(i, out _))
                        count++;
                return count;
            },
            (ints, __, dic) =>
            {
                var count = 0;
                foreach (var i in ints)
                    if (dic.TryGetValue(i, out _))
                        count++;
                return count;
            },
            writeLine: output.WriteLine
        );
    }
}
