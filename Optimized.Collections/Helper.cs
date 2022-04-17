using System.Diagnostics;

namespace Optimized.Collections;

internal static class Helper
{
    internal static int PowerOf2(int capacity)
    {
        if ((capacity & (capacity - 1)) == 0) return capacity;
        int i = 2;
        while (i < capacity) i <<= 1;
        return i;
    }

    [DebuggerHidden]
    [DebuggerStepThrough]
    internal static void ThrowArgumentOutOfRange()
    {
        throw new ArgumentOutOfRangeException();
    }

    [DebuggerHidden]
    [DebuggerStepThrough]
    internal static void ThrowCannotReduceCapacityBelowCount()
    {
        throw new Exception("Cannot reduce capacity below count.");
    }

    [DebuggerHidden]
    [DebuggerStepThrough]
    internal static void ThrowElementWithSaemKeyAlreadyExistsInTheMap()
    {
        throw new ArgumentException("An element with the same key already exists in the map.");
    }
}