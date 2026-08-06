// tests/FileTidy.Tests/InverseBoolConverterTests.cs
using FileTidy.App.Converters;

namespace FileTidy.Tests;

public class InverseBoolConverterTests
{
    private static bool Conv(object? v) => (bool)new InverseBoolConverter().Convert(v!, typeof(bool), null!, System.Globalization.CultureInfo.InvariantCulture);

    [Fact] public void True_BecomesFalse() => Assert.False(Conv(true));
    [Fact] public void False_BecomesTrue() => Assert.True(Conv(false));
    [Fact] public void NonBool_Throws() => Assert.Throws<ArgumentOutOfRangeException>(() => Conv("x"));
    [Fact] public void ConvertBack_NotSupported() => Assert.Throws<NotImplementedException>(
        () => new InverseBoolConverter().ConvertBack(true, typeof(bool), null!, System.Globalization.CultureInfo.InvariantCulture));
}
