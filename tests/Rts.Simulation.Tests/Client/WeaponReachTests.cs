using Rts.Client.Orders;
using Rts.Simulation.Geometry;
using Rts.Simulation.Math;
using Rts.Simulation.Systems;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Client;

/// <summary>The range rings of a defense: the numbers the simulation fires by.</summary>
public sealed class WeaponReachTests
{
    private static readonly PlayerId Player1 = new(1);

    [Fact]
    public void A_bunker_reaches_its_weapon_range_from_its_centre_with_no_least_range()
    {
        var rules = TestRules.Standard;
        var bunker = rules.Database.IndexOfBuilding("sentry_bunker");

        // The sentry machine gun: 5.5 cells.
        Assert.Equal(new WeaponReach.Reach(5 * WorldPos.LeptonsPerCell + WorldPos.LeptonsPerCell / 2, 0), WeaponReach.OfDefinition(rules, bunker));
        Assert.Null(WeaponReach.OfDefinition(rules, rules.Database.IndexOfBuilding("reactor")));
    }

    [Fact]
    public void A_weapon_with_a_least_range_shows_it()
    {
        // The Grand Cannon's sort: a cannon that cannot fire near.
        var rules = TestRules.WithWeapon("sentry_machine_gun", weapon => weapon with { MinRange = Fixed.FromInt(2) });

        Assert.Equal(new WeaponReach.Reach(5 * WorldPos.LeptonsPerCell + WorldPos.LeptonsPerCell / 2, 2 * WorldPos.LeptonsPerCell),
            WeaponReach.OfDefinition(rules, rules.Database.IndexOfBuilding("sentry_bunker")));
    }

    [Fact]
    public void The_ring_is_where_the_simulation_stops_letting_the_defense_fire()
    {
        var simulation = new GameSimulation(new SimulationSettings(1, new Rts.Simulation.Map.MapGrid(40, 40, TestRules.Standard.Land)), TestRules.Standard);
        var world = simulation.World;
        var bunker = world.CreateBuilding(Player1, TestRules.BuildingDef("sentry_bunker"), new CellPos(10, 10), ready: true, null);
        var reach = WeaponReach.Of(world, bunker)!.Value;
        var centre = Rts.Simulation.Combat.Aim.CentreOf(bunker);
        Assert.Equal(WeaponReach.Centre(bunker.Cell, bunker.Width, bunker.Height), centre);

        var inside = world.SpawnUnit(new PlayerId(2), UnitType.HeavyTank, new WorldPos(centre.X + reach.Max, centre.Y));
        var outside = world.SpawnUnit(new PlayerId(2), UnitType.HeavyTank, new WorldPos(centre.X + reach.Max + 1, centre.Y + 3 * WorldPos.LeptonsPerCell));
        Assert.True(TargetingSystem.CanFireAt(world, bunker, inside));
        Assert.False(TargetingSystem.CanFireAt(world, bunker, outside));
    }
}
