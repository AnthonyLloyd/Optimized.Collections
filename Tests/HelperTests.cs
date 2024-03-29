﻿namespace Tests;

using System.Numerics;
using CsCheck;
using Optimized.Collections;
using Xunit;

public class HelperTests
{
    const uint FIBONACCI_HASH_U = 2654435769;
    const int FIBONACCI_HASH = -1640531527;

    readonly Action<string> writeLine;
    public HelperTests(Xunit.Abstractions.ITestOutputHelper output) => writeLine = output.WriteLine;

    [Fact]
    public void Golden()
    {
        var g = (1.0 + Math.Sqrt(5)) * 0.5;
        var f = (uint)Math.Round(uint.MaxValue / g);
        Assert.Equal(FIBONACCI_HASH_U, f);
        var f2 = (int)(uint)Math.Round(uint.MaxValue / g);
        Assert.Equal(FIBONACCI_HASH, f2);
    }

    [Fact]
    public void GoldenCheck()
    {
        Gen.Int
        .Sample(i =>
        {
            var w1 = (int)(FIBONACCI_HASH_U * (uint)i);
            var w2 = FIBONACCI_HASH * i;
            return w1 == w2;
        });
    }

    [Fact]
    public void GoldenDistribution()
    {
        Gen.Int[0, 1000]
        .Sample(offset =>
        {
            for (int n = 2; n < 8192; n *= 2)
            {
                var set = new Set<int>(n);
                for (int i = offset; i < n + offset; i++)
                {
                    set.Add((i * FIBONACCI_HASH) & (n - 1));
                }
                Assert.Equal(n, set.Count);
            }
        });
    }

    [Fact]
    public void PowerOf2_012345()
    {
        Assert.Equal(0, Helper.PowerOf2(0));
        Assert.Equal(1, Helper.PowerOf2(1));
        Assert.Equal(2, Helper.PowerOf2(2));
        Assert.Equal(4, Helper.PowerOf2(3));
        Assert.Equal(4, Helper.PowerOf2(4));
        Assert.Equal(8, Helper.PowerOf2(5));
    }

    [Fact]
    public void PowerOf2Fallback_012345()
    {
        Assert.Equal(0, PowerOf2Fallback(0));
        Assert.Equal(1, PowerOf2Fallback(1));
        Assert.Equal(2, PowerOf2Fallback(2));
        Assert.Equal(4, PowerOf2Fallback(3));
        Assert.Equal(4, PowerOf2Fallback(4));
        Assert.Equal(8, PowerOf2Fallback(5));
    }

    [Fact]
    public void PowerOf2_To_Fallback()
    {
        Gen.Int[1, int.MaxValue >> 1].Sample(i =>
        {
            int actual = Helper.PowerOf2(i);
            int expected = PowerOf2Fallback(i);
            return actual == expected && BitOperations.IsPow2(actual)
                && actual >= i && actual < i * 2;
        });
    }

    static int PowerOf2Fallback(int value)
    {
        --value;
        value |= value >> 1;
        value |= value >> 2;
        value |= value >> 4;
        value |= value >> 8;
        value |= value >> 16;
        return value + 1;
    }
}
