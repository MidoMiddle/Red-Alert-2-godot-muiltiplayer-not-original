using Rts.Simulation.Rules;
using Rts.Simulation.World;

namespace Rts.Data.Tests.Rules;

/// <summary>The rules built in code (docs/implementation/M3_base_economy.md, 4.5): the order, the uniqueness of ids, the lookups.</summary>
public sealed class RulesBuilderTests
{
    [Fact]
    public void Each_kind_comes_out_sorted_by_id_whatever_the_order_of_the_calls()
    {
        var rules = new RulesBuilder()
            .Add(Unit("tesla_tank"))
            .Add(Unit("conscript"))
            .Add(Unit("heavy_tank"))
            .Build();

        Assert.Equal(new[] { "conscript", "heavy_tank", "tesla_tank" }, rules.Units.Select(unit => unit.Id));
    }

    [Fact]
    public void An_id_used_twice_in_any_kinds_is_an_error()
    {
        var builder = new RulesBuilder().Add(Unit("reactor")).Add(Building("reactor"));

        var error = Assert.Throws<ArgumentException>(() => builder.Build());

        Assert.Contains("'reactor'", error.Message);
    }

    [Fact]
    public void Every_definition_is_found_by_its_id_and_nothing_else_is()
    {
        var builder = new RulesBuilder();
        for (var i = 0; i < 50; i++)
        {
            builder.Add(Unit($"unit_{i:D2}"));
        }

        var rules = builder.Build();

        for (var i = 0; i < 50; i++)
        {
            Assert.True(rules.TryGetUnit($"unit_{i:D2}", out var unit));
            Assert.Equal($"unit_{i:D2}", unit.Id);
        }

        Assert.False(rules.TryGetUnit("unit_50", out _));
        Assert.False(rules.TryGetUnit("unit_", out _));
        Assert.False(rules.TryGetBuilding("unit_00", out _));
    }

    private static UnitDef Unit(string id) => new()
    {
        Id = id,
        Name = id,
        Faction = "union",
        Category = UnitCategory.Vehicle,
        Cost = 900,
        BuildTime = 180,
        Hp = 400,
        Speed = 40,
        TurnRate = 2048,
        Sight = 8,
        Locomotor = "track",
        Placement = UnitPlacement.Cell,
    };

    private static BuildingDef Building(string id) => new()
    {
        Id = id,
        Name = id,
        Faction = "union",
        Category = ProductionCategory.Building,
        Cost = 600,
        BuildTime = 150,
        Hp = 750,
        Footprint = new CellSize(2, 2),
    };
}
