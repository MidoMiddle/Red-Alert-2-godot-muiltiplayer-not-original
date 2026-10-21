using Rts.Simulation.Map;

namespace Rts.Simulation.Tests.Map;

/// <summary>The speed table of docs/implementation/M7_maps.md, 4.3 and docs/map_elements/02_land_types.</summary>
public sealed class LocomotorRulesTests
{
    [Theory]
    [InlineData(LandType.Clear, 100, 100, 100)]
    [InlineData(LandType.Rough, 90, 90, 75)]
    [InlineData(LandType.Road, 100, 100, 120)]
    [InlineData(LandType.Beach, 90, 80, 70)]
    [InlineData(LandType.Shallow, 0, 0, 0)]
    [InlineData(LandType.Deep, 0, 0, 0)]
    [InlineData(LandType.Rock, 0, 0, 0)]
    [InlineData(LandType.Ice, 80, 90, 90)]
    public void Table_matches_the_documentation(LandType land, int foot, int track, int wheel)
    {
        Assert.Equal(foot, LocomotorRules.SpeedPercent(Locomotor.Foot, land));
        Assert.Equal(track, LocomotorRules.SpeedPercent(Locomotor.Track, land));
        Assert.Equal(wheel, LocomotorRules.SpeedPercent(Locomotor.Wheel, land));
        Assert.Equal(foot > 0, LocomotorRules.IsPassable(Locomotor.Foot, land));
    }

    [Fact]
    public void Every_land_type_has_a_row()
    {
        foreach (var land in Enum.GetValues<LandType>())
        {
            Assert.True(LocomotorRules.IsKnown(land));
            foreach (var locomotor in Enum.GetValues<Locomotor>())
            {
                Assert.InRange(LocomotorRules.SpeedPercent(locomotor, land), 0, 200);
            }
        }
    }

    [Fact]
    public void Unknown_values_are_rejected()
    {
        Assert.False(LocomotorRules.IsKnown((LandType)0));
        Assert.False(LocomotorRules.IsKnown((LandType)9));
        Assert.Throws<ArgumentOutOfRangeException>(() => LocomotorRules.SpeedPercent(Locomotor.Foot, (LandType)9));
        Assert.Throws<ArgumentOutOfRangeException>(() => LocomotorRules.SpeedPercent((Locomotor)0, LandType.Clear));
        Assert.Throws<ArgumentOutOfRangeException>(() => LocomotorRules.SpeedPercent((Locomotor)4, LandType.Clear));
    }
}
