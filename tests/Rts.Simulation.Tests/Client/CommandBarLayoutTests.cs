using Rts.Client.Hud;

namespace Rts.Simulation.Tests.Client;

/// <summary>The command bar: its place at the bottom of the field, its rules, and where the camera puts the rig point.</summary>
public sealed class CommandBarLayoutTests
{
    private const float ViewWidth = 1920f;
    private const float ViewHeight = 1080f;

    [Fact]
    public void TheFieldIsTheViewWithoutTheSidebarAndTheBar()
    {
        Assert.Equal(1690f, SidebarLayout.FieldWidth(ViewWidth));
        Assert.Equal(1008f, CommandBarLayout.FieldHeight(ViewHeight));
    }

    [Theory]
    [InlineData(17.85f)]
    [InlineData(25.5f)]
    [InlineData(40.8f)]
    public void TheRigPointIsInTheMiddleOfTheFieldAtAnyZoom(float size)
    {
        var (x, y) = CommandBarLayout.CentreOffset(SidebarLayout.Width, CommandBarLayout.Height, size, ViewHeight);
        var pixelsPerUnit = ViewHeight / size;

        Assert.Equal(845f, ViewWidth / 2f - x * pixelsPerUnit, 3);
        Assert.Equal(504f, ViewHeight / 2f + y * pixelsPerUnit, 3);
    }

    [Fact]
    public void TheOrdersNeedASelection()
    {
        Assert.False(CommandBarLayout.Enabled(CommandBarButton.Stop, hasSelection: false));
        Assert.True(CommandBarLayout.Enabled(CommandBarButton.Stop, hasSelection: true));
        Assert.True(CommandBarLayout.Enabled(CommandBarButton.TypeOnMap, hasSelection: true));
        Assert.True(CommandBarLayout.Enabled(CommandBarButton.Waypoints, hasSelection: true));
    }

    [Fact]
    public void TheButtonsThatWaitForAMilestoneStayGreyed()
    {
        Assert.False(CommandBarLayout.Enabled(CommandBarButton.SelectCombat, hasSelection: true));
        Assert.False(CommandBarLayout.Enabled(CommandBarButton.Bug, hasSelection: true));
    }
}
