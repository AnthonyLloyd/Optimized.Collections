using System.Diagnostics;

namespace Optimized.Collections;

internal static class ThrowHelper
{
    [DebuggerHidden]
    [DebuggerStepThrough]
    internal static void ThrowArgumentOutOfRange()
    {
        throw new ArgumentOutOfRangeException();
    }

    [DebuggerHidden]
    [DebuggerStepThrough]
    internal static void CannotReduceCapacityBelowCount()
    {
        throw new Exception("Cannot reduce capacity below count.");
    }
}