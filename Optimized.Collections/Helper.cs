[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Tests, PublicKey=002400000480000094000000060200000024000052534131000400000100010089f5f142bc30ab84c70e4ccd0b09a684c3d822a99d269cac850f155421fced34048c0e3869a38db5cca81cd8ffcb7469a79422c3a2438a234c7534885471c1cc856ae40461a1ec4a4c5b1d897ba50f70ff486801a482505e0ec506c22da4a6ac5a1d8417e47985aa95caffd180dab750815989d43fcf0a7ee06ce8f1825106d0")]
namespace Optimized.Collections;

using System.Diagnostics.CodeAnalysis;
using System.Numerics;

internal static class Helper
{
    internal static int PowerOf2(int capacity)
    {
        return (int)BitOperations.RoundUpToPowerOf2((uint)capacity);
    }

    [DoesNotReturn]
    internal static void ThrowArgumentOutOfRange()
    {
        throw new ArgumentOutOfRangeException();
    }

    [DoesNotReturn]
    internal static void ThrowCannotReduceCapacityBelowCount()
    {
        throw new Exception("Cannot reduce capacity below count.");
    }

    [DoesNotReturn]
    internal static void ThrowElementWithSameKeyAlreadyExistsInTheMap()
    {
        throw new ArgumentException("An element with the same key already exists in the map.");
    }

    [DoesNotReturn]
    internal static void ThrowKeyNotFoundException()
    {
        throw new KeyNotFoundException();
    }
}