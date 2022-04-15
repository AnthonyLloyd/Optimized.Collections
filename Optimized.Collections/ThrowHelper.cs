using System.Diagnostics;

namespace Optimized.Collections;

internal static class ThrowHelper
{
    [DebuggerHidden]
    [DebuggerStepThrough]
    public static void ThrowArgumentOutOfRange()
    {
        throw new ArgumentOutOfRangeException();
    }

    [DebuggerHidden]
    [DebuggerStepThrough]
    public static void CannotReduceCapacityBelowCount()
    {
        throw new Exception("Cannot reduce capacity below count.");
    }
}