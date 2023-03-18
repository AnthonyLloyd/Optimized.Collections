namespace Tests;

using CsCheck;
using Optimized.Collections;
using Xunit;

public class SetSpanTests
{
    readonly Action<string> writeLine;
    public SetSpanTests(Xunit.Abstractions.ITestOutputHelper output) => writeLine = output.WriteLine;

    [Fact]
    public void SetSpan_Contains_Performance()
    {
        Gen.Select(Gen.Int[0, 1000].Array[1000], Gen.Int[0, 1000].HashSet)
        .Select((i, d) => (i, new Set<int>(d), new HashSet<int>(d)))
        .Faster(
            (ints, set, _) =>
            {
                var span = set.AsSpan();
                var count = 0;
                foreach (var i in ints)
                    if (span.Contains(i))
                        count++;
                return count;
            },
            (ints, _, hashset) =>
            {
                var count = 0;
                foreach (var i in ints)
                    if (hashset.Contains(i))
                        count++;
                return count;
            }
        ).Output(writeLine);
    }
}
