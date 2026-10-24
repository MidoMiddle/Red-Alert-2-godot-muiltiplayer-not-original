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
/// The fortified bunker and the lens towers (stage 2e of filling the game): soldiers go into the bunker, five at most, fire from inside
/// with their range counted from its wall, and come out when told, when it is sold and when it falls; an idle lens tower sends its
/// charge to the one that fires and makes its shot stronger. By the game data; two people play, no bot.
/// </summary>
public sealed class GarrisonTests
{
    private static readonly PlayerId Union = new(1);
    private static readonly PlayerId Allied = new(2);

    // A bunker of 2 x 2 cells: its centre is the corner of the cells (21, 21).
    private static readonly CellPos Bunker = new(20, 20);

    private static GameSimulation Match() =>
        new(SimulationSettings.ForMatch(
            new MatchSetup(
                "bunker",
                13,
                new MatchOptions(StartingCredits: 50_000),
                [
                    new MatchPlayer(1, PlayerKind.Human, AiDifficulty.Medium, "union", Color: 0, Team: 1, StartIndex: 1, new CellPos(6, 6)),
                    new MatchPlayer(2, PlayerKind.Human, AiDifficulty.Medium, "alliance", Color: 1, Team: 2, StartIndex: 2, new CellPos(58, 58)),
                ],
                [new CellPos(6, 6), new CellPos(58, 58)]),
            new MapGrid(64, 64)),
            GameDataFiles.Rules);

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

    /// <summary>Steps until the bunker holds <paramref name="count"/> soldiers, at most 600 ticks.</summary>
    private static void Fill(GameSimulation simulation, Building bunker, int count)
    {
        for (var tick = 0; tick < 600 && bunker.Occupants.Count < count; tick++)
        {
            simulation.Step();
        }

        Assert.Equal(count, bunker.Occupants.Count);
    }

    [Fact]
    public void Five_soldiers_go_in_the_sixth_stays_out_and_a_tank_is_not_sent()
    {
        var simulation = Match();
        var world = simulation.World;
        var bunker = Build(world, Union, "battle_bunker", Bunker.X, Bunker.Y);
        var soldiers = Soldiers(world, 6, 14, 26);
        var tank = world.SpawnUnit(Union, UnitType.HeavyTank, new CellPos(12, 26).Center);
        var at = tank.Position;

        simulation.Enqueue(new EnterCommand(Union, simulation.CurrentTick, 0, Ids([.. soldiers, tank]), bunker.Id));
        Fill(simulation, bunker, 5);
        Step(simulation, 60);

        Assert.Equal(5, bunker.Occupants.Count);
        Assert.All(bunker.Occupants, occupant => Assert.Equal(UnitType.Conscript, occupant.Type));
        var outside = Assert.Single(soldiers, soldier => soldier.IsAlive);
        Assert.Null(outside.EnterTarget);
        Assert.Equal(at, tank.Position);
        Assert.Null(tank.EnterTarget);
    }

    [Fact]
    public void Another_order_on_the_way_forgets_the_bunker_and_an_enemy_cannot_go_in()
    {
        var simulation = Match();
        var world = simulation.World;
        var bunker = Build(world, Union, "battle_bunker", Bunker.X, Bunker.Y);
        var soldier = Soldiers(world, 1, 10, 30)[0];
        var rifleman = world.SpawnUnit(Allied, UnitType.AllianceRifleman, new CellPos(26, 20).Center);

        simulation.Enqueue(new EnterCommand(Union, simulation.CurrentTick, 0, [soldier.Id], bunker.Id));
        Step(simulation, 2);
        Assert.Equal(bunker.Id, soldier.EnterTarget);
        simulation.Enqueue(new StopCommand(Union, simulation.CurrentTick, 1, [soldier.Id]));
        var enemy = new EnterCommand(Allied, simulation.CurrentTick, 0, [rifleman.Id], bunker.Id);
        simulation.Enqueue(enemy);
        Step(simulation, 200);

        Assert.Null(soldier.EnterTarget);
        Assert.True(soldier.IsAlive);
        Assert.Empty(bunker.Occupants);
        Assert.Contains(enemy, simulation.RefusedCommands);
    }

    [Fact]
    public void The_soldiers_inside_fire_with_their_range_counted_from_the_wall()
    {
        // The rifle reaches 4 cells: from the centre of the bunker 4.5 cells is out of reach, from its wall 3.5 is in.
        var simulation = Match();
        var world = simulation.World;
        var bunker = Build(world, Union, "battle_bunker", Bunker.X, Bunker.Y);
        simulation.Enqueue(new EnterCommand(Union, simulation.CurrentTick, 0, Ids(Soldiers(world, 2, 16, 23)), bunker.Id));
        Fill(simulation, bunker, 2);

        var near = world.SpawnUnit(Allied, UnitType.AllianceEngineer, new CellPos(25, 20).Center);
        var far = world.SpawnUnit(Allied, UnitType.AllianceEngineer, new CellPos(21, 27).Center);
        var full = far.Hp;
        Step(simulation, 120);

        Assert.False(near.IsAlive && near.Hp == full, "hit from inside the bunker");
        Assert.Equal(full, far.Hp);
    }

    [Fact]
    public void Told_to_they_come_out_with_their_hit_points()
    {
        var simulation = Match();
        var world = simulation.World;
        var bunker = Build(world, Union, "battle_bunker", Bunker.X, Bunker.Y);
        var soldiers = Soldiers(world, 2, 16, 23);
        world.SetHp(soldiers[0], 60, null);
        simulation.Enqueue(new EnterCommand(Union, simulation.CurrentTick, 0, Ids(soldiers), bunker.Id));
        Fill(simulation, bunker, 2);

        simulation.Enqueue(new EvacuateCommand(Union, simulation.CurrentTick, 0, bunker.Id));
        simulation.Step();
        var left = simulation.LastTickEvents.OfType<UnitLeftGarrison>().ToList();

        Assert.Empty(bunker.Occupants);
        Assert.Equal(2, left.Count);
        var hps = left.Select(evt => ((Unit)world.Entities.First(entity => entity.Id == evt.Unit)).Hp).Order().ToList();
        Assert.Equal([60, 125], hps);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void They_come_out_when_the_bunker_falls_to_a_shot_or_is_sold(bool sold)
    {
        var simulation = Match();
        var world = simulation.World;
        var bunker = Build(world, Union, "battle_bunker", Bunker.X, Bunker.Y);
        simulation.Enqueue(new EnterCommand(Union, simulation.CurrentTick, 0, Ids(Soldiers(world, 3, 16, 23)), bunker.Id));
        Fill(simulation, bunker, 3);

        if (sold)
        {
            simulation.Enqueue(new SellCommand(Union, simulation.CurrentTick, 0, bunker.Id));
        }
        else
        {
            world.SetHp(bunker, 1, null);
            var cannon = world.Rules.Database.IndexOfWeapon("alliance_light_cannon");
            world.Hits.Add(new Rts.Simulation.Combat.Hit(bunker.Id, bunker.Id, Allied, cannon, Rts.Simulation.Combat.Aim.CentreOf(bunker), bunker.Id));
        }

        simulation.Step();

        Assert.False(bunker.IsAlive);
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
