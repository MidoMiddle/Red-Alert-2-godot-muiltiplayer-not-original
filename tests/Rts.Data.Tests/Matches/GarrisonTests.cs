using System.Collections.Immutable;
using Rts.Simulation;
using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Matches;
using Rts.Simulation.World;

namespace Rts.Data.Tests.Matches;

/// <summary>
/// Soldiers in a building and the lens towers (stage 2e of filling the game): soldiers go into a civilian office, six at most, fire from
/// inside with their range counted from its wall, and come out when told and when it falls; an idle lens tower sends its charge to the
/// one that fires and makes its shot stronger. By the game data; two people play, no bot. (The fortified bunker of Yuri's Revenge these
/// tests began with was taken out on 2026-10-25.)
/// </summary>
public sealed class GarrisonTests
{
    private static readonly PlayerId Union = new(1);
    private static readonly PlayerId Allied = new(2);

    // An office of 2 x 2 cells: its centre is the corner of the cells (21, 21).
    private static readonly CellPos Office = new(20, 20);

    private static GameSimulation Match() =>
        new(SimulationSettings.ForMatch(
            new MatchSetup(
                "office",
                13,
                new MatchOptions(StartingCredits: 50_000, FogOfWar: false),
                [
                    new MatchPlayer(1, PlayerKind.Human, AiDifficulty.Medium, "union", Color: 0, Team: 1, StartIndex: 1, new CellPos(6, 6)),
                    new MatchPlayer(2, PlayerKind.Human, AiDifficulty.Medium, "alliance", Color: 1, Team: 2, StartIndex: 2, new CellPos(58, 58)),
                ],
                [new CellPos(6, 6), new CellPos(58, 58)]),
            Map()),
            GameDataFiles.Rules);

    private static MapGrid Map()
    {
        var land = new byte[64 * 64];
        System.Array.Fill(land, (byte)LandType.Clear);
        return MapGrid.FromLayers(64, 64, land, new byte[land.Length], default, null, [],
            [new BuildingState(BuildingKind.NeutralOffice, Office, PlayerId.Neutral, 0)]);
    }

    private static Building OfficeOf(GameWorld world) =>
        world.Entities.OfType<Building>().Single(building => building.Kind == BuildingKind.NeutralOffice);

    private static Building Build(GameWorld world, PlayerId player, string id, int x, int y)
    {
        world.Rules.Database.TryGetBuilding(id, out var def);
        return world.CreateBuilding(player, def, new CellPos(x, y), true, null);
    }

    private static void Step(GameSimulation simulation, int ticks)
    {
        for (var tick = 0; tick < ticks; tick++)
        {
            simulation.Step();
        }
    }

    private static List<Unit> Soldiers(GameWorld world, int count, int x, int y)
    {
        var soldiers = new List<Unit>();
        for (var i = 0; i < count; i++)
        {
            soldiers.Add(world.SpawnUnit(Union, UnitType.Conscript, new CellPos(x + i % 3, y + i / 3).Center, snapToSubcell: true));
        }

        return soldiers;
    }

    private static ImmutableArray<EntityId> Ids(IEnumerable<Unit> units) => [.. units.Select(unit => unit.Id)];

    /// <summary>Steps until the building holds <paramref name="count"/> soldiers, at most 600 ticks.</summary>
    private static void Fill(GameSimulation simulation, Building building, int count)
    {
        for (var tick = 0; tick < 600 && building.Occupants.Count < count; tick++)
        {
            simulation.Step();
        }

        Assert.Equal(count, building.Occupants.Count);
    }

    [Fact]
    public void Six_soldiers_go_in_the_seventh_stays_out_and_a_tank_is_not_sent()
    {
        var simulation = Match();
        var world = simulation.World;
        var office = OfficeOf(world);
        var soldiers = Soldiers(world, 7, 14, 26);
        var tank = world.SpawnUnit(Union, UnitType.HeavyTank, new CellPos(12, 26).Center);
        var at = tank.Position;

        simulation.Enqueue(new EnterCommand(Union, simulation.CurrentTick, 0, Ids([.. soldiers, tank]), office.Id));
        Fill(simulation, office, 6);
        Step(simulation, 60);

        Assert.Equal(6, office.Occupants.Count);
        Assert.All(office.Occupants, occupant => Assert.Equal(UnitType.Conscript, occupant.Type));
        var outside = Assert.Single(soldiers, soldier => soldier.IsAlive);
        Assert.Null(outside.EnterTarget);
        Assert.Equal(at, tank.Position);
        Assert.Null(tank.EnterTarget);
    }

    [Fact]
    public void Another_order_on_the_way_forgets_the_office()
    {
        var simulation = Match();
        var world = simulation.World;
        var office = OfficeOf(world);
        var soldier = Soldiers(world, 1, 10, 30)[0];

        simulation.Enqueue(new EnterCommand(Union, simulation.CurrentTick, 0, [soldier.Id], office.Id));
        Step(simulation, 2);
        Assert.Equal(office.Id, soldier.EnterTarget);
        simulation.Enqueue(new StopCommand(Union, simulation.CurrentTick, 1, [soldier.Id]));
        Step(simulation, 200);

        Assert.Null(soldier.EnterTarget);
        Assert.True(soldier.IsAlive);
        Assert.Empty(office.Occupants);
    }

    [Fact]
    public void The_soldiers_inside_fire_with_their_range_counted_from_the_wall()
    {
        // The rifle reaches 4 cells: from the centre of the office 4.5 cells is out of reach, from its wall 3.5 is in.
        var simulation = Match();
        var world = simulation.World;
        var office = OfficeOf(world);
        simulation.Enqueue(new EnterCommand(Union, simulation.CurrentTick, 0, Ids(Soldiers(world, 2, 16, 23)), office.Id));
        Fill(simulation, office, 2);
        // A civilian building gives its soldiers no sight of their own: the map is in sight.
        world.VisionOf(Union).ExploreAll();

        var near = world.SpawnUnit(Allied, UnitType.AllianceEngineer, new CellPos(25, 20).Center);
        var far = world.SpawnUnit(Allied, UnitType.AllianceEngineer, new CellPos(21, 27).Center);
        var full = far.Hp;
        Step(simulation, 120);

        Assert.False(near.IsAlive && near.Hp == full, "hit from inside the office");
        Assert.Equal(full, far.Hp);
    }

    [Fact]
    public void Told_to_they_come_out_with_their_hit_points()
    {
        var simulation = Match();
        var world = simulation.World;
        var office = OfficeOf(world);
        var soldiers = Soldiers(world, 2, 16, 23);
        world.SetHp(soldiers[0], 60, null);
        simulation.Enqueue(new EnterCommand(Union, simulation.CurrentTick, 0, Ids(soldiers), office.Id));
        Fill(simulation, office, 2);

        simulation.Enqueue(new EvacuateCommand(Union, simulation.CurrentTick, 0, office.Id));
        simulation.Step();
        var left = simulation.LastTickEvents.OfType<UnitLeftGarrison>().ToList();

        Assert.Empty(office.Occupants);
        Assert.Equal(2, left.Count);
        var hps = left.Select(evt => ((Unit)world.Entities.First(entity => entity.Id == evt.Unit)).Hp).Order().ToList();
        Assert.Equal([60, 125], hps);
    }

    [Fact]
    public void They_come_out_when_the_office_falls_to_a_shot()
    {
        var simulation = Match();
        var world = simulation.World;
        var office = OfficeOf(world);
        simulation.Enqueue(new EnterCommand(Union, simulation.CurrentTick, 0, Ids(Soldiers(world, 3, 16, 23)), office.Id));
        Fill(simulation, office, 3);

        world.SetHp(office, 1, null);
        var cannon = world.Rules.Database.IndexOfWeapon("alliance_light_cannon");
        world.Hits.Add(new Rts.Simulation.Combat.Hit(office.Id, office.Id, Allied, cannon, Rts.Simulation.Combat.Aim.CentreOf(office), office.Id));

        simulation.Step();

        Assert.False(office.IsAlive);
        Assert.Equal(3, simulation.LastTickEvents.OfType<UnitLeftGarrison>().Count());
        Assert.Equal(3, world.Entities.OfType<Unit>().Count(unit => unit.IsAlive && unit.Owner == Union && unit.Type == UnitType.Conscript
            && System.Math.Abs(unit.Position.ToCell().X - 21) <= 3 && System.Math.Abs(unit.Position.ToCell().Y - 21) <= 3));
    }

    /// <summary>The damage of the first shot of the lens tower at (30, 30) at an MCV 7 cells west, with the towers east of it.</summary>
    private static (int Damage, int Supported) FirstLensShot(params int[] towersEast)
    {
        var simulation = Match();
        var world = simulation.World;
        Build(world, Allied, "alliance_power_plant", 40, 40);
        Build(world, Allied, "alliance_power_plant", 43, 40);
        Build(world, Allied, "alliance_prism_tower", 30, 30);
        foreach (var x in towersEast)
        {
            Build(world, Allied, "alliance_prism_tower", x, 30);
        }

        var mcv = world.SpawnUnit(Union, UnitType.Mcv, new CellPos(23, 30).Center);
        var full = mcv.Hp;
        for (var tick = 0; tick < 120 && mcv.Hp == full; tick++)
        {
            simulation.Step();
            if (mcv.Hp != full)
            {
                return (full - mcv.Hp, simulation.LastTickEvents.OfType<TowerSupported>().Count());
            }
        }

        return (0, 0);
    }

    [Fact]
    public void Idle_lens_towers_nearby_add_their_charge_to_the_shot()
    {
        var alone = FirstLensShot();
        // Two towers 3 and 6 cells east: the MCV is out of their range, the tower that fires is in it.
        var chained = FirstLensShot(33, 36);
        // One 12 cells east: too far to support.
        var distant = FirstLensShot(42);

        Assert.True(alone.Damage > 0);
        Assert.Equal(0, alone.Supported);
        Assert.Equal((alone.Damage * 2, 2), chained);
        Assert.Equal(alone, distant);
    }
}
