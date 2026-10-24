using Rts.Simulation;
using Rts.Simulation.Combat;
using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Matches;
using Rts.Simulation.Systems;
using Rts.Simulation.World;

namespace Rts.Data.Tests.Matches;

/// <summary>
/// Veterancy (loose ends, stage 3; numbers of RA2): experience from the worth of the enemies killed, the ranks at 3 and 6 times the own
/// cost, what a veteran and an elite gain, who trains not, and the spy in a barracks. By the game data, without the fog of war; two
/// people play, no bot.
/// </summary>
public sealed class VeterancyTests
{
    private static readonly PlayerId Union = new(1);
    private static readonly PlayerId Allied = new(2);

    private static GameSimulation Match()
    {
        var simulation = new GameSimulation(
            SimulationSettings.ForMatch(
                new MatchSetup(
                    "veterans",
                    59,
                    new MatchOptions(StartingCredits: 10_000, FogOfWar: false),
                    [
                        new MatchPlayer(1, PlayerKind.Human, AiDifficulty.Medium, "union", Color: 0, Team: 1, StartIndex: 1, new CellPos(4, 4)),
                        new MatchPlayer(2, PlayerKind.Human, AiDifficulty.Medium, "alliance", Color: 1, Team: 2, StartIndex: 2, new CellPos(60, 60)),
                    ],
                    [new CellPos(4, 4), new CellPos(60, 60)]),
                new MapGrid(64, 64)),
            GameDataFiles.Rules);
        simulation.World.VisionOf(Union).ExploreAll();
        simulation.World.VisionOf(Allied).ExploreAll();
        return simulation;
    }

    private static Unit Spawn(GameWorld world, PlayerId player, UnitType type, int x, int y) =>
        world.SpawnUnit(player, type, new CellPos(x, y).Center, snapToSubcell: true);

    /// <summary>Kills enemies of <paramref name="killer"/> worth at least <paramref name="worth"/>; the events of the kills.</summary>
    private static List<SimEvent> KillFor(GameWorld world, Unit killer, int worth)
    {
        var events = new List<SimEvent>();
        for (var killed = 0; killed < worth;)
        {
            var victim = Spawn(world, Allied, UnitType.AllianceLightTank, 40, 40);
            DeathSystem.Kill(world, victim, killer.Id, killer.Owner, DeathCause.Weapon, -1, events);
            killed += victim.Archetype.Cost;
            world.RemoveDead();
        }

        return events;
    }

    [Fact]
    public void A_tank_is_a_veteran_at_three_times_its_cost_killed_and_elite_at_six()
    {
        var world = Match().World;
        var tank = Spawn(world, Union, UnitType.HeavyTank, 10, 10);
        var cost = tank.Archetype.Cost;
        Assert.True(cost > 0);

        tank.Experience = (cost * Veterancy.VeteranRatio) - 1;
        Assert.Equal(Rank.Rookie, Veterancy.RankOf(tank));
        var events = KillFor(world, tank, 1);
        Assert.Equal(Rank.Veteran, Veterancy.RankOf(tank));
        Assert.Contains(events, e => e is UnitPromoted { Rank: Rank.Veteran } promoted && promoted.Unit == tank.Id);

        events = KillFor(world, tank, (cost * Veterancy.VeteranRatio * 2) - tank.Experience);
        Assert.Equal(Rank.Elite, Veterancy.RankOf(tank));
        Assert.Contains(events, e => e is UnitPromoted { Rank: Rank.Elite });
        Assert.Equal(cost * Veterancy.VeteranRatio * 2, tank.Experience);
    }

    [Fact]
    public void A_veteran_takes_two_thirds_hits_a_tenth_harder_and_moves_a_fifth_faster()
    {
        var world = Match().World;
        var rookie = Spawn(world, Union, UnitType.HeavyTank, 10, 10);
        var veteran = Spawn(world, Union, UnitType.HeavyTank, 12, 10);
        var enemy = Spawn(world, Allied, UnitType.AllianceLightTank, 14, 10);
        veteran.Experience = veteran.Archetype.Cost * Veterancy.VeteranRatio;

        Assert.Equal(90, Veterancy.Damage(world, rookie, enemy.Id, 90));
        Assert.Equal(60, Veterancy.Damage(world, veteran, enemy.Id, 90));
        Assert.Equal(99, Veterancy.Damage(world, enemy, veteran.Id, 90));
        Assert.Equal(rookie.Speed * 6 / 5, Veterancy.Speed(veteran, veteran.Speed));
        Assert.Equal(rookie.Speed, Veterancy.Speed(rookie, rookie.Speed));
    }

    [Fact]
    public void An_elite_heals_itself_and_a_veteran_soldier_reloads_faster()
    {
        var simulation = Match();
        var world = simulation.World;
        var tank = Spawn(world, Union, UnitType.HeavyTank, 10, 10);
        tank.Experience = tank.Archetype.Cost * Veterancy.VeteranRatio * 2;
        world.SetHp(tank, tank.MaxHp - 50, null);
        for (var tick = 0; tick < 3 * SimulationSettings.DefaultTicksPerSecond; tick++)
        {
            simulation.Step();
        }

        Assert.InRange(tank.Hp, tank.MaxHp - 48, tank.MaxHp - 46);

        var soldier = Spawn(world, Union, UnitType.Conscript, 20, 20);
        soldier.Experience = soldier.Archetype.Cost * Veterancy.VeteranRatio;
        Assert.Equal(60, Veterancy.Reload(world, soldier.Id, 100));
        Assert.Equal(100, Veterancy.Reload(world, tank.Id, 100));
    }

    [Fact]
    public void An_engineer_and_a_unit_killing_its_own_side_gain_nothing()
    {
        var world = Match().World;
        var engineer = Spawn(world, Union, UnitType.Engineer, 10, 10);
        KillFor(world, engineer, 5000);
        Assert.Equal(0, engineer.Experience);

        var tank = Spawn(world, Union, UnitType.HeavyTank, 12, 10);
        var own = Spawn(world, Union, UnitType.Conscript, 13, 10);
        DeathSystem.Kill(world, own, tank.Id, Union, DeathCause.Weapon, -1, null);
        Assert.Equal(0, tank.Experience);
    }

    [Fact]
    public void A_spy_in_the_enemy_barracks_makes_its_player_s_infantry_veteran()
    {
        var simulation = Match();
        var world = simulation.World;
        world.Rules.Database.TryGetBuilding("barracks", out var def);
        var barracks = world.CreateBuilding(Union, def, new CellPos(20, 20), true, null);
        var spy = Spawn(world, Allied, UnitType.AllianceSpy, 18, 24);
        simulation.Enqueue(new EnterCommand(Allied, simulation.CurrentTick, 0, [spy.Id], barracks.Id));
        for (var tick = 0; tick < 300 && spy.IsAlive; tick++)
        {
            simulation.Step();
        }

        Assert.False(spy.IsAlive);
        Assert.True(world.Player(Allied).VeteranInfantry);
        Assert.False(world.Player(Allied).VeteranVehicles);

        var events = new List<SimEvent>();
        var soldier = Spawn(world, Allied, UnitType.AllianceRifleman, 50, 50);
        Veterancy.Produced(world, soldier, events);
        Assert.Equal(Rank.Veteran, Veterancy.RankOf(soldier));
        var tank = Spawn(world, Allied, UnitType.AllianceLightTank, 52, 50);
        Veterancy.Produced(world, tank, events);
        Assert.Equal(Rank.Rookie, Veterancy.RankOf(tank));
    }
}
