using Rts.Client.Terrain;
using Rts.Simulation.Visibility;

namespace Rts.Simulation.Tests.Client;

public sealed class FogShadesTests
{
    [Fact]
    public void Each_state_has_its_shade_and_the_revealed_fog_shows_all()
    {
        Assert.Equal(0, FogShades.Target(CellVisibility.Shroud));
        Assert.Equal(128, FogShades.Target(CellVisibility.Fog));
        Assert.Equal(255, FogShades.Target(CellVisibility.Visible));
        Assert.Equal(255, FogShades.Target(CellVisibility.Shroud, revealed: true));
    }

    [Fact]
    public void A_shade_reaches_its_target_in_a_quarter_second_at_an_even_pace_without_overshooting()
    {
        Assert.Equal(102, FogShades.Step(0, 255, 0.1));                    // 40 % of the way in 0.1 s
        Assert.Equal(255, FogShades.Step(0, 255, 0.25));
        Assert.Equal(128, FogShades.Step(255, 128, 0.25));
        Assert.Equal(128, FogShades.Step(120, 128, 0.1));                  // no overshoot
        Assert.Equal(128, FogShades.Step(128, 128, 0.5));

        byte shade = 0;
        for (var frame = 0; frame < 15; frame++)
        {
            shade = FogShades.Step(shade, 255, 1 / 60.0);
        }

        Assert.Equal(255, shade);                                           // 15 frames at 60 fps
    }

    [Fact]
    public void The_minimap_is_black_on_the_shroud_dim_on_the_fog_and_clear_where_seen()
    {
        Assert.Equal(255, FogShades.MinimapAlpha(FogShades.Shroud, 110));
        Assert.Equal(110, FogShades.MinimapAlpha(FogShades.Fog, 110));
        Assert.Equal(0, FogShades.MinimapAlpha(FogShades.Visible, 110));
        Assert.InRange(FogShades.MinimapAlpha(64, 110), 110, 255);
    }
}
