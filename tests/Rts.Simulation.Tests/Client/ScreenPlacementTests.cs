using Rts.Client.Audio;

namespace Rts.Simulation.Tests.Client;

/// <summary>
/// Where a positional sound stands on the screen (docs/implementation/M6_menus_skirmish.md, 4.8): full volume on the screen, quieter
/// outside it, silent beyond 1,5 screens, and the panning that puts the edge of the screen fully to one side.
/// </summary>
public sealed class ScreenPlacementTests
{
    private const float Width = 1920f;
    private const float Height = 1080f;

    [Fact]
    public void A_point_on_the_screen_plays_at_full_volume()
    {
        var placement = ScreenPlacement.Place(960f, 540f, Width, Height);

        Assert.True(placement.Audible);
        Assert.Equal(0f, placement.GainDb, 3);
    }

    [Fact]
    public void A_point_on_the_edge_is_still_at_full_volume()
    {
        Assert.Equal(0f, ScreenPlacement.Place(Width, 540f, Width, Height).GainDb, 3);
    }

    [Fact]
    public void Half_a_screen_outside_is_a_third_of_the_way_to_silence()
    {
        var placement = ScreenPlacement.Place(Width + Width / 2f, 540f, Width, Height);

        Assert.True(placement.Audible);
        Assert.Equal(ScreenPlacement.OffScreenDb / 3f, placement.GainDb, 3);
    }

    [Fact]
    public void A_point_one_and_a_half_screens_outside_is_played_at_the_last_level()
    {
        var placement = ScreenPlacement.Place(Width + 1.5f * Width, 540f, Width, Height);

        Assert.True(placement.Audible);
        Assert.Equal(ScreenPlacement.OffScreenDb, placement.GainDb, 3);
    }

    [Fact]
    public void A_point_beyond_one_and_a_half_screens_is_not_played()
    {
        Assert.False(ScreenPlacement.Place(Width + 1.6f * Width, 540f, Width, Height).Audible);
    }

    [Fact]
    public void Off_the_top_the_distance_is_measured_in_screen_heights()
    {
        var placement = ScreenPlacement.Place(960f, -Height, Width, Height);

        Assert.True(placement.Audible);
        Assert.Equal(ScreenPlacement.OffScreenDb * 1f / 1.5f, placement.GainDb, 3);
    }

    [Fact]
    public void The_panning_strength_pans_the_edge_fully_under_the_project_setting()
    {
        // Godot's default 2d_panning_strength is 0.5: the strength of 4 makes half a screen the whole pan (see ScreenPlacement).
        Assert.Equal(4f, ScreenPlacement.PanningStrength(0.5f), 3);
        Assert.Equal(1f, ScreenPlacement.PanningStrength(2f), 3);
        Assert.Equal(0f, ScreenPlacement.PanningStrength(0f), 3);
    }
}
