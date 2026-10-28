using Rts.Client.App;

namespace Rts.Simulation.Tests.Client;

/// <summary>The graphics test of the Graphics tab (the owner, 2026-10-28): its levels and its search for the finest within 60 FPS.</summary>
public sealed class GraphicsLadderTests
{
    [Fact]
    public void The_levels_go_from_the_finest_to_the_lightest_and_the_defaults_are_one_of_them()
    {
        var levels = GraphicsLadder.Levels;

        Assert.Equal(new GraphicsLevel(100, AntiAliasing.Msaa4, ShadowQuality.High, true), levels[0]);
        Assert.Equal(new GraphicsLevel(50, AntiAliasing.Off, ShadowQuality.Off, false), levels[^1]);
        Assert.Contains(GraphicsLevel.Of(SettingsValues.Defaults), levels);
        for (var i = 1; i < levels.Count; i++)
        {
            // Never finer than the level before it in any of the four.
            Assert.True(levels[i].RenderScale <= levels[i - 1].RenderScale);
            Assert.True(levels[i].AntiAliasing <= levels[i - 1].AntiAliasing || levels[i - 1].AntiAliasing == AntiAliasing.Fxaa);
            Assert.True(levels[i].Shadows <= levels[i - 1].Shadows);
            Assert.True(!levels[i].Ssao || levels[i - 1].Ssao);
            Assert.NotEqual(levels[i], levels[i - 1]);
            Assert.Contains(levels[i].RenderScale, SettingsValues.RenderScaleChoices);
        }
    }

    [Fact]
    public void A_level_changes_the_graphics_only()
    {
        var values = SettingsValues.Defaults with { FpsLimit = 144, MasterVolume = 30 };

        var finest = GraphicsLadder.Levels[0].ApplyTo(values);

        Assert.Equal(values with { RenderScale = 100, AntiAliasing = AntiAliasing.Msaa4, Shadows = ShadowQuality.High, Ssao = true }, finest);
        Assert.Equal(GraphicsLadder.Levels[0], GraphicsLevel.Of(finest));
    }

    [Theory]
    // The time of a frame of each level, finest first: the search finds the finest within 12.5 ms.
    [InlineData(new[] { 8.0, 7, 6, 5, 4, 3, 2, 2, 1 }, 0, true)]
    [InlineData(new[] { 30.0, 25, 18, 14, 12, 10, 8, 6, 4 }, 4, true)]
    [InlineData(new[] { 60.0, 50, 40, 35, 30, 25, 20, 13, 12 }, 8, true)]
    [InlineData(new[] { 90.0, 80, 70, 60, 50, 40, 30, 20, 15 }, 8, false)]
    [InlineData(new[] { 14.0, 13, 12.5, 12, 11, 10, 9, 8, 7 }, 2, true)]
    public void The_search_finds_the_finest_level_within_the_target_in_four_measurements(double[] milliseconds, int expected, bool fits)
    {
        var search = new GraphicsSearch();
        var measured = 0;
        while (search.Next is { } level)
        {
            search.Report(milliseconds[level]);
            measured++;
        }

        Assert.Equal((expected, fits), (search.Result, search.Fits));
        Assert.InRange(measured, 3, 4);
    }

    [Fact]
    public void Held_back_by_the_processor_the_finest_level_that_costs_little_more_than_the_lightest_is_kept()
    {
        // Only whole frames known: 20 ms at the lightest level is the processor; up to 23 ms is no worse for the graphics.
        double[] milliseconds = [40, 30, 26, 22, 21, 20.5, 20, 20, 20];
        var search = new GraphicsSearch();
        search.Floor(milliseconds[^1]);
        while (search.Next is { } level)
        {
            search.Report(milliseconds[level]);
        }

        Assert.Equal(23, search.TargetMs, 3);
        Assert.Equal((3, true), (search.Result, search.Fits));

        // A floor below the target leaves it as it was.
        var fast = new GraphicsSearch();
        fast.Floor(5);
        Assert.Equal(GraphicsLadder.TargetMs, fast.TargetMs);
    }

    [Fact]
    public void Its_first_measurement_is_of_the_defaults_of_a_new_game()
    {
        Assert.Equal(GraphicsLevel.Of(SettingsValues.Defaults), GraphicsLadder.Levels[new GraphicsSearch().Next!.Value]);
    }
}
