using Optimized.Collections;
using CsCheck;
using Xunit;

namespace Tests;

public class SetTests
{
    readonly Action<string> writeLine;
    public SetTests(Xunit.Abstractions.ITestOutputHelper output) => writeLine = output.WriteLine;

}