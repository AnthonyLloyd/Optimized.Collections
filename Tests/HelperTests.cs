namespace Tests;

using System.Numerics;
using CsCheck;
using Optimized.Collections;
using Xunit;

public class HelperTests
{
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
