using CsCheck;
using Optimized.Collections;
using Xunit;

namespace Tests;

public class MemoizeTests
{
    readonly Action<string> writeLine;
    public MemoizeTests(Xunit.Abstractions.ITestOutputHelper output) => writeLine = output.WriteLine;
    
    [Fact]
    public void Memoize_SingleThreaded_Performance()
    {
        static Func<K, V> MemoizeStandard<K, V>(Func<K, V> f) where K : notnull
        {
            var d = new Dictionary<K, V>();
            return i =>
            {
                if (!d.TryGetValue(i, out var r))
                    d.Add(i, r = f(i));
                return r;
            };
        }

        var f = (int i) => i;

        Gen.Int.Array
        .Select(a => (a, Memoize.SingleThreaded(f), MemoizeStandard(f)))
        .Faster(
            (items, m, _) =>
            {
                foreach (var i in items) m(i);
            },
            (items, _, d) =>
            {
                foreach (var i in items) d(i);
            },
            repeat: 100, raiseexception: false, sigma: 100
        ).Output(writeLine);
    }
}