using Rts.Client.App;

namespace Rts.Simulation.Tests.Client;

/// <summary>The 15-second rule for a new window size (docs/implementation/M6_menus_skirmish.md, 4.5).</summary>
public sealed class ResolutionChangeTests
{
    private static readonly WindowSize Small = new(1280, 720);
    private static readonly WindowSize Large = new(1920, 1080);

    [Fact]
    public void KeepingReturnsTheNewSizeAndEndsTheChange()
    {
        var change = new ResolutionChange();
        change.Begin(Small, Large);

        Assert.Equal(Large, change.Keep());
        Assert.False(change.IsPending);
    }

    [Fact]
    public void RevertingReturnsThePreviousSizeAndEndsTheChange()
    {
        var change = new ResolutionChange();
        change.Begin(Small, Large);

        Assert.Equal(Small, change.Revert());
        Assert.False(change.IsPending);
    }

    [Fact]
    public void TheTimeRunningOutReturnsThePreviousSize()
    {
        var change = new ResolutionChange();
        change.Begin(Small, Large);

        Assert.Null(change.Tick(14.9));
        Assert.True(change.IsPending);
        Assert.Equal(Small, change.Tick(0.2));
        Assert.False(change.IsPending);
    }

    [Fact]
    public void TheDialogCountsDownFromFifteen()
    {
        var change = new ResolutionChange();
        change.Begin(Small, Large);
        Assert.Equal(15, change.SecondsLeft);

        change.Tick(0.5);
        Assert.Equal(15, change.SecondsLeft);

        change.Tick(0.6);
        Assert.Equal(14, change.SecondsLeft);
    }

    [Fact]
    public void ASecondChangeWaitsForTheFirstOne()
    {
        var change = new ResolutionChange();
        var third = new WindowSize(2560, 1440);

        Assert.True(change.Begin(Small, Large));
        Assert.False(change.Begin(Large, third));
        Assert.Equal(Large, change.Keep());
    }

    [Fact]
    public void NothingHappensWithoutAPendingChange()
    {
        var change = new ResolutionChange();

        Assert.Null(change.Keep());
        Assert.Null(change.Revert());
        Assert.Null(change.Tick(1));
        Assert.Equal(0, change.SecondsLeft);
    }
}
