using Rts.Simulation.Map;

namespace Rts.Simulation.Tests.Map;

/// <summary>The speed table of docs/implementation/M7_maps.md, 4.3 and docs/map_elements/02_land_types.</summary>
public sealed class LocomotorRulesTests
{
    [Theory]
    [InlineData(LandType.Clear, 100, 100, 100, 100, 0, 100)]
    [InlineData(LandType.Rough, 90, 90, 75, 75, 0, 75)]
    [InlineData(LandType.Road, 100, 100, 120, 100, 0, 100)]
    [InlineData(LandType.Beach, 90, 80, 70, 100, 0, 100)]
    [InlineData(LandType.Shallow, 0, 0, 0, 100, 0, 100)]
    [InlineData(LandType.Deep, 0, 0, 0, 100, 100, 100)]
    [InlineData(LandType.Rock, 0, 0, 0, 0, 0, 0)]
    [InlineData(LandType.Ice, 80, 90, 90, 100, 0, 100)]
    public void Table_matches_the_documentation(LandType land, int foot, int track, int wheel, int amphibious, int naval, int hover)
    {
        Assert.Equal(
            [foot, track, wheel, amphibious, naval, hover],
            Enum.GetValues<Locomotor>().Select(locomotor => LocomotorRules.SpeedPercent(locomotor, land)).ToArray());
        Assert.Equal(foot > 0, LocomotorRules.IsPassable(Locomotor.Foot, land));
    }

    [Theory]
    [InlineData(Locomotor.Foot, 80)]
    [InlineData(Locomotor.Track, 75)]
    [InlineData(Locomotor.Wheel, 65)]
    [InlineData(Locomotor.Amphibious, 65)]
    [InlineData(Locomotor.Naval, 100)]
    [InlineData(Locomotor.Hover, 65)]
    public void Slope_multipliers_match_the_plan(Locomotor locomotor, int percent)
    {
        Assert.Equal(percent, LocomotorRules.SlopeMultiplier(locomotor));
    }

    [Fact]
    public void A_cell_is_land_on_flat_ground_land_times_slope_on_a_ramp_and_closed_on_a_cliff()
    {
        foreach (var land in Enum.GetValues<LandType>())
        {
            foreach (var locomotor in Enum.GetValues<Locomotor>())
            {
                var flat = LocomotorRules.SpeedPercent(locomotor, land);
                var slope = (int)Rts.Simulation.Math.IntMath.DivRound((long)flat * LocomotorRules.SlopeMultiplier(locomotor), 100);

                Assert.Equal(flat, LocomotorRules.CellPercent(locomotor, land, CellShape.Flat));
                Assert.Equal(slope, LocomotorRules.CellPercent(locomotor, land, CellShape.Slope));
                Assert.Equal(0, LocomotorRules.CellPercent(locomotor, land, CellShape.Cliff));
            }
        }

        // Rough ground on a ramp for wheels: 75 % of the way at 65 %, rounded.
        Assert.Equal(49, LocomotorRules.CellPercent(Locomotor.Wheel, LandType.Rough, CellShape.Slope));
        Assert.Equal(78, LocomotorRules.CellPercent(Locomotor.Wheel, LandType.Road, CellShape.Slope));
    }

    [Fact]
    public void Every_land_type_and_locomotor_has_a_value()
    {
        foreach (var land in Enum.GetValues<LandType>())
        {
            Assert.True(LocomotorRules.IsKnown(land));
            foreach (var locomotor in Enum.GetValues<Locomotor>())
            {
                Assert.True(LocomotorRules.IsKnown(locomotor));
                Assert.InRange(LocomotorRules.SpeedPercent(locomotor, land), 0, 200);
            }
        }

        Assert.Equal(LocomotorRules.Count, Enum.GetValues<Locomotor>().Length);
    }

    [Fact]
    public void Unknown_values_are_rejected()
    {
        Assert.False(LocomotorRules.IsKnown((LandType)0));
        Assert.False(LocomotorRules.IsKnown((LandType)9));
        Assert.False(LocomotorRules.IsKnown((Locomotor)0));
        Assert.False(LocomotorRules.IsKnown((Locomotor)7));
        Assert.Throws<ArgumentOutOfRangeException>(() => LocomotorRules.SpeedPercent(Locomotor.Foot, (LandType)9));
        Assert.Throws<ArgumentOutOfRangeException>(() => LocomotorRules.SpeedPercent((Locomotor)0, LandType.Clear));
        Assert.Throws<ArgumentOutOfRangeException>(() => LocomotorRules.SpeedPercent((Locomotor)7, LandType.Clear));
        Assert.Throws<ArgumentOutOfRangeException>(() => LocomotorRules.SlopeMultiplier((Locomotor)7));
    }
}
