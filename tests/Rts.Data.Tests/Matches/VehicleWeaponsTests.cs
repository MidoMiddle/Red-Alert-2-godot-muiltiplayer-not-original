using Rts.Simulation;
using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Matches;
using Rts.Simulation.Systems;
using Rts.Simulation.World;

namespace Rts.Data.Tests.Matches;

/// <summary>
/// The weapons of five vehicles (stage 3c of filling the game): the super heavy tank's twin cannons and its missiles at the air, the tank
/// hunter's sabot, the rocket artillery's rocket that anti-air can bring down, the beam tank's splitting beam, the holo tank that passes
/// for a tree while it stands. By the game data, without the fog of war; two people play, no bot.
/// </summary>
public sealed class VehicleWeaponsTests
{
    private static readonly PlayerId Allied = new(1);
    private static readonly PlayerId Union = new(2);

    private static GameSimulation Match()
    {
        var simulation = new GameSimulation(
            SimulationSettings.ForMatch(
                new MatchSetup(
                    "vehicles",
                    23,
                    new MatchOptions(StartingCredits: 10_000, FogOfWar: false),
                    [
                        new MatchPlayer(1, PlayerKind.Human, AiDifficulty.Medium, "alliance", Color: 0, Team: 1, StartIndex: 1, new CellPos(4, 4)),
                        new MatchPlayer(2, PlayerKind.Human, AiDifficulty.Medium, "union", Color: 1, Team: 2, StartIndex: 2, new CellPos(60, 60)),
                    ],
                    [new CellPos(4, 4), new CellPos(60, 60)]),
                new MapGrid(64, 64)),
            GameDataFiles.Rules);
        simulation.World.VisionOf(Allied).ExploreAll();
        simulation.World.VisionOf(Union).ExploreAll();
        return simulation;
    }

    private static Unit Spawn(GameWorld world, PlayerId player, UnitType type, int x, int y) =>
        world.SpawnUnit(player, type, new CellPos(x, y).Center, snapToSubcell: true);

    private static Building Build(GameWorld world, PlayerId player, string id, int x, int y)
    {
        world.Rules.Database.TryGetBuilding(id, out var def);
        return world.CreateBuilding(player, def, new CellPos(x, y), true, null);
    }

    private static List<SimEvent> Run(GameSimulation simulation, int ticks, Func<bool>? until = null)
    {
        var events = new List<SimEvent>();
        for (var tick = 0; tick < ticks && until?.Invoke() != true; tick++)
        {
            simulation.Step();
            events.AddRange(simulation.LastTickEvents);
        }

        return events;
    }

    [Fact]
    public void The_super_heavy_tank_fires_two_shells_at_the_ground_and_its_missiles_only_at_the_air()
    {
        var simulation = Match();
        var world = simulation.World;
        var tank = Spawn(world, Union, UnitType.ApocalypseTank, 30, 30);
        var mcv = Spawn(world, Allied, UnitType.AllianceMcv, 34, 30);
        var events = Run(simulation, 40);
        var cannons = world.Rules.Database.IndexOfWeapon("apocalypse_cannons");

        Assert.Equal(2, events.OfType<WeaponFired>().Count(fired => fired.Shooter == tank.Id));
        Assert.True(mcv.Hp < mcv.MaxHp);

        var jet = Spawn(world, Allied, UnitType.AllianceHarrier, 30, 34);
        var full = jet.Hp;
        Run(simulation, 120);
        Assert.True(!jet.IsAlive || jet.Hp < full, "the missiles hit the jet");
        Assert.True(TargetingSystem.CanHurt(world.Rules, UnitType.ApocalypseTank, jet));
        Assert.Equal(Rts.Simulation.Rules.WeaponTargets.Air, world.Rules.Database.Weapons[world.Rules.Unit(UnitType.ApocalypseTank).Secondary].Targets);
        Assert.Equal(Rts.Simulation.Rules.WeaponTargets.Ground, world.Rules.Database.Weapons[cannons].Targets);
    }

    [Fact]
    public void The_tank_hunter_hits_a_heavy_vehicle_hard_and_a_soldier_barely()
    {
        var simulation = Match();
        var world = simulation.World;
        Spawn(world, Allied, UnitType.AllianceTankDestroyer, 30, 30);
        var mcv = Spawn(world, Union, UnitType.Mcv, 34, 30);
        Run(simulation, 60, () => mcv.Hp < mcv.MaxHp);
        Assert.Equal(150, mcv.MaxHp - mcv.Hp);

        var share = TargetingSystem.ShareAgainst(world.Rules, UnitType.AllianceTankDestroyer, Spawn(world, Union, UnitType.Engineer, 40, 40));
        Assert.Equal(2, share);
    }

    [Fact]
    public void The_rocket_artillery_fires_only_when_ordered_and_not_too_near()
    {
        var simulation = Match();
        var world = simulation.World;
        var launcher = Spawn(world, Union, UnitType.RocketArtillery, 20, 30);
        var mcv = Spawn(world, Allied, UnitType.AllianceMcv, 35, 30);
        Run(simulation, 60);
        Assert.Null(launcher.Target);
        Assert.Equal(mcv.MaxHp, mcv.Hp);

        simulation.Enqueue(new AttackCommand(Union, simulation.CurrentTick, 0, [launcher.Id], mcv.Id));
        var events = Run(simulation, 300, () => mcv.Hp < mcv.MaxHp);
        Assert.True(mcv.Hp < mcv.MaxHp);
        Assert.Contains(events, e => e is WeaponFired fired && fired.Shooter == launcher.Id);
    }

    [Fact]
    public void Anti_air_brings_the_rocket_down_before_it_lands()
    {
        var simulation = Match();
        var world = simulation.World;
        Build(world, Allied, "alliance_power_plant", 44, 26);
        var guard = Build(world, Allied, "alliance_patriot_missile_system", 37, 33);
        var launcher = Spawn(world, Union, UnitType.RocketArtillery, 20, 30);
        var mcv = Spawn(world, Allied, UnitType.AllianceMcv, 36, 30);

        simulation.Enqueue(new AttackCommand(Union, simulation.CurrentTick, 0, [launcher.Id], mcv.Id));
        var events = Run(simulation, 300);

        var rockets = events.OfType<EntityDestroyed>().Where(destroyed => destroyed.Killer == guard.Id).ToList();
        Assert.NotEmpty(rockets);
        Assert.Equal(mcv.MaxHp, mcv.Hp);
    }

    [Fact]
    public void The_beam_splits_to_the_enemies_round_its_target()
    {
        var simulation = Match();
        var world = simulation.World;
        var tank = Spawn(world, Allied, UnitType.AlliancePrismTank, 20, 30);
        var first = Spawn(world, Union, UnitType.Mcv, 29, 30);
        var near = Spawn(world, Union, UnitType.Mcv, 30, 32);
        var far = Spawn(world, Union, UnitType.Mcv, 36, 30);
        simulation.Enqueue(new AttackCommand(Allied, simulation.CurrentTick, 0, [tank.Id], first.Id));

        Run(simulation, 60, () => first.Hp < first.MaxHp);

        Assert.Equal(100, first.MaxHp - first.Hp);
        Assert.Equal(30, near.MaxHp - near.Hp);
        Assert.Equal(far.MaxHp, far.Hp);
    }

    [Fact]
    public void The_holo_tank_standing_still_passes_for_a_tree_and_is_picked_once_it_moves()
    {
        var simulation = Match();
        var world = simulation.World;
        var holo = Spawn(world, Allied, UnitType.AllianceMirageTank, 30, 30);
        var tank = Spawn(world, Union, UnitType.HeavyTank, 34, 30);
        Run(simulation, 40);

        Assert.True(TargetingSystem.IsTree(holo));
        Assert.Null(tank.Target);
        Assert.True(tank.Hp < tank.MaxHp, "the holo tank fires from its cover");

        simulation.Enqueue(new MoveCommand(Allied, simulation.CurrentTick, 0, [holo.Id], new CellPos(30, 40).Center));
        Run(simulation, 12);
        Assert.False(TargetingSystem.IsTree(holo));
        Assert.Equal(holo.Id, tank.Target);
    }
}
