using Rts.Simulation.Map;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.World;

/// <summary>
/// The M2 unit roster: ids of docs/implementation/M3_base_economy.md, 4.6, and the movement numbers the simulation tests play by
/// (<see cref="TestRules"/>, the same as game/data): 6 leptons per tick for each point of RA2 <c>Speed</c>, 2048 per tick for
/// <c>ROT</c> 5 (docs/implementation/M2_unit_control.md, 4.11).
/// </summary>
public sealed class UnitTypesTests
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
        var archetype = TestRules.Unit(type);

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

        Assert.Equal(types.Length, TestRules.Standard.Units.Count);
        Assert.Equal(types, TestRules.Standard.Units.Select(a => a.Type));
        Assert.All(types, type => Assert.True(UnitTypes.IsKnown(type)));
        Assert.Equal(TestRules.Standard.Units.Count, TestRules.Standard.Units.Select(a => a.Id).Distinct().Count());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(49)]
    [InlineData(255)]
    public void Unknown_types_are_not_known(int type)
    {
        Assert.False(UnitTypes.IsKnown((UnitType)type));
        Assert.Throws<ArgumentOutOfRangeException>(() => TestRules.Unit((UnitType)type));
    }

    [Fact]
    public void Types_are_found_by_data_id()
    {
        foreach (var archetype in TestRules.Standard.Units)
        {
            Assert.True(UnitTypes.TryGetById(archetype.Id, out var found));
            Assert.Equal(archetype.Type, found);
            Assert.Equal(archetype.Id, UnitTypes.IdOf(found));
        }

        Assert.False(UnitTypes.TryGetById("Heavy_Tank", out _));
        Assert.False(UnitTypes.TryGetById("", out _));
    }
}
