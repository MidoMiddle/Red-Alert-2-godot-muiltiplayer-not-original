using Rts.Simulation.Map;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.World;

/// <summary>
/// The M2 unit roster: ids of docs/implementation/M3_base_economy.md, 4.6, and movement numbers of the RA2
/// counterparts in docs/ra2_units (docs/implementation/M2_unit_control.md, 4.11).
/// </summary>
public sealed class UnitArchetypesTests
{
    [Theory]
    [InlineData(UnitType.Conscript, "conscript", UnitCategory.Infantry, Locomotor.Foot, 24, 0, false)]
    [InlineData(UnitType.Rocketeer, "rocketeer", UnitCategory.Infantry, Locomotor.Foot, 24, 0, false)]
    [InlineData(UnitType.Engineer, "engineer", UnitCategory.Infantry, Locomotor.Foot, 24, 0, false)]
    [InlineData(UnitType.Mcv, "mcv", UnitCategory.Vehicle, Locomotor.Track, 24, 2048, false)]
    [InlineData(UnitType.Harvester, "harvester", UnitCategory.Vehicle, Locomotor.Track, 24, 2048, true)]
    [InlineData(UnitType.SupportVehicle, "support_vehicle", UnitCategory.Vehicle, Locomotor.Wheel, 48, 2048, true)]
    [InlineData(UnitType.HeavyTank, "heavy_tank", UnitCategory.Vehicle, Locomotor.Track, 36, 2048, true)]
    [InlineData(UnitType.TeslaTank, "tesla_tank", UnitCategory.Vehicle, Locomotor.Track, 36, 2048, true)]
    public void Roster_matches_the_documentation(
        UnitType type, string id, UnitCategory category, Locomotor locomotor, int speed, int turnRate, bool hasTurret)
    {
        var archetype = UnitArchetypes.Get(type);

        Assert.Equal(type, archetype.Type);
        Assert.Equal(id, archetype.Id);
        Assert.Equal(category, archetype.Category);
        Assert.Equal(locomotor, archetype.Locomotor);
        Assert.Equal(speed, archetype.Speed);
        Assert.Equal(turnRate, archetype.TurnRate);
        Assert.Equal(hasTurret, archetype.HasTurret);
        Assert.Equal(category == UnitCategory.Infantry ? UnitPlacement.Subcell : UnitPlacement.Cell, archetype.Placement);
    }

    [Fact]
    public void Every_unit_type_has_data_in_order()
    {
        var types = Enum.GetValues<UnitType>();

        Assert.Equal(types.Length, UnitArchetypes.All.Count);
        Assert.Equal(types, UnitArchetypes.All.Select(a => a.Type));
        Assert.All(types, type => Assert.True(UnitArchetypes.IsKnown(type)));
        Assert.Equal(UnitArchetypes.All.Count, UnitArchetypes.All.Select(a => a.Id).Distinct().Count());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    [InlineData(255)]
    public void Unknown_types_are_not_known(int type)
    {
        Assert.False(UnitArchetypes.IsKnown((UnitType)type));
        Assert.Throws<ArgumentOutOfRangeException>(() => UnitArchetypes.Get((UnitType)type));
    }

    [Theory]
    [InlineData(4, 24)]
    [InlineData(6, 36)]
    [InlineData(8, 48)]
    public void Ra2_speed_converts_to_leptons_per_tick(int ra2Speed, int leptons)
    {
        Assert.Equal(leptons, UnitArchetypes.SpeedFromRa2(ra2Speed));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 410)]
    [InlineData(5, 2048)]
    [InlineData(40, 16384)]
    public void Ra2_rot_converts_to_turn_rate(int rot, int turnRate)
    {
        Assert.Equal(turnRate, UnitArchetypes.TurnRateFromRa2(rot));
    }
}
