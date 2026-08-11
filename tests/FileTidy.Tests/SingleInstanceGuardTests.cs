using FileTidy.App;

namespace FileTidy.Tests;

public class SingleInstanceGuardTests
{
    private const string Name = "FileTidy.Test.SingleInstance";

    [Fact]
    public void FirstInstance_Wins_SecondIsRejected_ReleasedAllowsNext()
    {
        var first = new SingleInstanceGuard(Name);
        try
        {
            Assert.True(first.IsFirstInstance);
            using var second = new SingleInstanceGuard(Name);
            Assert.False(second.IsFirstInstance);
        }
        finally { first.Dispose(); }

        using var third = new SingleInstanceGuard(Name);
        Assert.True(third.IsFirstInstance);
    }
}