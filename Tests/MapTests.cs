using System;
using System.Linq;
using System.Collections.Generic;
using Optimized.Collections;
using CsCheck;
using Xunit;

namespace Tests;

public class MapTests
{
    readonly Action<string> writeLine;
    public MapTests(Xunit.Abstractions.ITestOutputHelper output) => writeLine = output.WriteLine;

    static bool ModelEqual<K, V>(Map<K, V> m, Dictionary<K, V> d) where K : IEquatable<K>
    {
        if (m.Count != d.Count) return false;
        for (int i = 0; i < m.Count; i++)
        {
            var key = m.Key(i);
            var value = m.Value(i);
            if (!d.TryGetValue(key, out var dvalue) || !value!.Equals(dvalue))
                return false;
        }
        return true;
    }

    [Fact]
    public void Map_ModelBased()
    {
        Gen.Dictionary(Gen.Int, Gen.Byte)
        .Select(d => (new Map<int, byte>(d), new Dictionary<int, byte>(d)))
        .SampleModelBased(
            Gen.Select(Gen.Int[0, 100], Gen.Byte).Operation<Map<int, byte>, Dictionary<int, byte>>((m, d, t) =>
            {
                m[t.V0] = t.V1;
                d[t.V0] = t.V1;
            })
            , ModelEqual
            , time: 10
        );
    }

    [Fact]
    public void Map_Performance_Add()
    {
        Gen.Int.Select(Gen.Byte).Array
        .Faster(
            items =>
            {
                var m = new Map<int, byte>();
                foreach (var (k, v) in items) m[k] = v;
            },
            items =>
            {
                var m = new Dictionary<int, byte>();
                foreach (var (k, v) in items) m[k] = v;
            },
            repeat: 100, raiseexception: false, sigma: 100
        ).Output(writeLine);
    }

    [Fact]
    public void Map_Performance_IndexOf()
    {
        Gen.Dictionary(Gen.Int, Gen.Byte)
        .Select(a => (a, new Map<int, byte>(a), new Dictionary<int, byte>(a)))
        .Faster(
            (items, map, _) =>
            {
                foreach (var (k, _) in items) map.IndexOf(k);
            },
            (items, _, dict) =>
            {
                foreach (var (k, _) in items) dict.ContainsKey(k);
            },
            repeat: 100, raiseexception: false, sigma: 100
        ).Output(writeLine);
    }

    [Fact]
    public void Map_Performance_Memoize()
    {
        static Func<K, V> Memoize<K, V>(Func<K, V> f) where K : notnull
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
        .Select(a => (a, Map.Memoize(f), Memoize(f)))
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