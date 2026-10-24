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
/// The abilities of the infantry (stage 3b of filling the game): engineers take buildings of the enemy and mend their own, the infiltrator
/// passes for the enemy's own soldier and goes into its buildings, Ivan plants a bomb, the demolition trooper blows up, the commando's C4
/// brings a building down. By the game data, without the fog of war; two people play, no bot.
/// </summary>
public sealed class InfantryAbilitiesTests
{
    private static readonly PlayerId Allied = new(1);
    private static readonly PlayerId Union = new(2);

    private static GameSimulation Match()
    {
        var simulation = new GameSimulation(
            SimulationSettings.ForMatch(
                new MatchSetup(
                    "abilities",
                    19,
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
    public void An_engineer_takes_a_refinery_of_the_enemy_and_its_sight_with_it_but_not_a_defense()
    {
        var simulation = Match();
        var world = simulation.World;
        var refinery = Build(world, Union, "refinery", 30, 30);
        var coil = Build(world, Union, "tesla_coil", 40, 30);
        var engineer = Spawn(world, Allied, UnitType.AllianceEngineer, 26, 31);
        var other = Spawn(world, Allied, UnitType.AllianceEngineer, 26, 33);

        var refused = new EnterCommand(Allied, simulation.CurrentTick, 1, [other.Id], coil.Id);
        simulation.Enqueue(new EnterCommand(Allied, simulation.CurrentTick, 0, [engineer.Id], refinery.Id));
        simulation.Enqueue(refused);
        var events = Run(simulation, 300, () => refinery.Owner == Allied);
        Run(simulation, 2);

        Assert.Equal(Allied, refinery.Owner);
        Assert.False(engineer.IsAlive);
        Assert.Contains(events, e => e is BuildingCaptured { From.Value: 2, To.Value: 1 });
        Assert.Contains(refused, simulation.RefusedCommands);
        Assert.True(world.VisionOf(Allied).IsVisible(new CellPos(31, 31)));
        Assert.Equal(Union, coil.Owner);
    }

    [Fact]
    public void An_engineer_mends_its_own_damaged_building_whole_and_is_spent()
    {
        var simulation = Match();
        var world = simulation.World;
        var plant = Build(world, Allied, "alliance_power_plant", 20, 20);
        world.SetHp(plant, 100, null);
        var engineer = Spawn(world, Allied, UnitType.AllianceEngineer, 17, 21);

        simulation.Enqueue(new EnterCommand(Allied, simulation.CurrentTick, 0, [engineer.Id], plant.Id));
        Run(simulation, 300, () => !engineer.IsAlive);

        Assert.Equal(plant.MaxHp, plant.Hp);
        Assert.False(engineer.IsAlive);
    }

    [Fact]
    public void The_infiltrator_takes_half_the_credits_from_a_refinery()
    {
        var simulation = Match();
        var world = simulation.World;
        var refinery = Build(world, Union, "refinery", 30, 30);
        var spy = Spawn(world, Allied, UnitType.AllianceSpy, 26, 31);
        var (theirs, mine) = (world.Player(Union).Credits, world.Player(Allied).Credits);

        simulation.Enqueue(new EnterCommand(Allied, simulation.CurrentTick, 0, [spy.Id], refinery.Id));
        Run(simulation, 300, () => !spy.IsAlive);

        Assert.False(spy.IsAlive);
        Assert.Equal(theirs / 2, world.Player(Union).Credits);
        Assert.Equal(mine + theirs - theirs / 2, world.Player(Allied).Credits);
        Assert.Equal(Union, refinery.Owner);
    }

    [Fact]
    public void The_infiltrator_in_a_power_plant_blacks_its_owner_out_for_a_while()
    {
        var simulation = Match();
        var world = simulation.World;
        var reactor = Build(world, Union, "reactor", 30, 30);
        Build(world, Union, "radar", 34, 30);
        var spy = Spawn(world, Allied, UnitType.AllianceSpy, 26, 31);
        Run(simulation, 2);
        Assert.False(world.Player(Union).LowPower);

        simulation.Enqueue(new EnterCommand(Allied, simulation.CurrentTick, 0, [spy.Id], reactor.Id));
        Run(simulation, 300, () => !spy.IsAlive);
        Run(simulation, 1);
        Assert.True(world.Player(Union).LowPower);
        Assert.Equal(0, world.Player(Union).PowerOutput);

        Run(simulation, EntrySystem.BlackoutTicks);
        Assert.False(world.Player(Union).LowPower);
    }

    [Fact]
    public void The_infiltrator_in_a_radar_makes_its_owner_forget_the_map()
    {
        var simulation = Match();
        var world = simulation.World;
        var radar = Build(world, Union, "radar", 30, 30);
        var spy = Spawn(world, Allied, UnitType.AllianceSpy, 26, 31);
        Assert.True(world.VisionOf(Union).IsExplored(new CellPos(2, 2)));

        simulation.Enqueue(new EnterCommand(Allied, simulation.CurrentTick, 0, [spy.Id], radar.Id));
        Run(simulation, 300, () => !spy.IsAlive);

        Assert.False(world.VisionOf(Union).IsExplored(new CellPos(2, 2)));
        Assert.True(world.VisionOf(Union).IsExplored(new CellPos(31, 31)), "what it sees now stays");
    }

    [Fact]
    public void The_disguised_infiltrator_is_left_alone_by_soldiers_but_not_by_a_dog()
    {
        var simulation = Match();
        var world = simulation.World;
        var spy = Spawn(world, Allied, UnitType.AllianceSpy, 30, 30);
        var conscript = Spawn(world, Union, UnitType.Conscript, 32, 30);
        Run(simulation, 60);
        Assert.True(spy.IsAlive);
        Assert.Null(conscript.Target);
        Assert.Equal(spy.MaxHp, spy.Hp);

        Spawn(world, Union, UnitType.AttackDog, 34, 30);
        Run(simulation, 120, () => !spy.IsAlive);
        Assert.False(spy.IsAlive);
    }

    [Fact]
    public void Ivans_bomb_goes_off_after_thirty_seconds_and_he_moves_on()
    {
        var simulation = Match();
        var world = simulation.World;
        var ivan = Spawn(world, Union, UnitType.CrazyIvan, 30, 30);
        var mcv = Spawn(world, Allied, UnitType.AllianceMcv, 31, 30);
        var events = Run(simulation, 60, () => mcv.BombTicks > 0);
        var planted = Assert.Single(events.OfType<BombPlanted>());
        Assert.Equal(ivan.Id, planted.Shooter);
        Assert.Equal(mcv.MaxHp, mcv.Hp);
        Assert.Null(ivan.Target);

        Run(simulation, 449 - 1);
        Assert.Equal(mcv.MaxHp, mcv.Hp);
        Run(simulation, 4);
        Assert.True(mcv.Hp < mcv.MaxHp, "the bomb went off");
        Assert.Equal(0, mcv.BombTicks);
    }

    [Fact]
    public void The_demolition_trooper_ordered_at_a_tank_blows_up_and_picks_nothing_by_itself()
    {
        var simulation = Match();
        var world = simulation.World;
        var trooper = Spawn(world, Union, UnitType.Terrorist, 30, 30);
        var tank = Spawn(world, Allied, UnitType.AllianceMcv, 33, 30);
        Run(simulation, 60);
        Assert.True(trooper.IsAlive);
        Assert.Null(trooper.Target);

        simulation.Enqueue(new AttackCommand(Union, simulation.CurrentTick, 0, [trooper.Id], tank.Id));
        Run(simulation, 120, () => tank.Hp < tank.MaxHp);
        Assert.False(trooper.IsAlive);
        Assert.True(tank.Hp < tank.MaxHp);
    }

    [Fact]
    public void A_demolition_trooper_shot_dead_blows_up_where_it_fell()
    {
        var simulation = Match();
        var world = simulation.World;
        var trooper = Spawn(world, Union, UnitType.Terrorist, 30, 30);
        var mcv = Spawn(world, Allied, UnitType.AllianceMcv, 31, 30);
        var cannon = world.Rules.Database.IndexOfWeapon("alliance_light_cannon");
        world.SetHp(trooper, 1, null);
        world.Hits.Add(new Rts.Simulation.Combat.Hit(trooper.Id, mcv.Id, Allied, cannon, trooper.Position, trooper.Id));
        Run(simulation, 4);

        Assert.False(trooper.IsAlive);
        Assert.True(mcv.Hp < mcv.MaxHp);
    }

    [Fact]
    public void The_commandos_C4_brings_a_building_down_and_her_pistols_leave_it_alone()
    {
        var simulation = Match();
        var world = simulation.World;
        var commando = Spawn(world, Allied, UnitType.AllianceTanya, 26, 31);
        var plant = Build(world, Union, "nuclear_reactor", 30, 30);
        simulation.Enqueue(new AttackCommand(Allied, simulation.CurrentTick, 0, [commando.Id], plant.Id));

        var events = Run(simulation, 300, () => !plant.IsAlive);
        Assert.False(plant.IsAlive);
        Assert.Contains(events, e => e is BombPlanted planted && planted.Target == plant.Id);
        var pistols = world.Rules.Database.IndexOfWeapon("alliance_double_pistols");
        Assert.DoesNotContain(events, e => e is ProjectileImpact impact && impact.Weapon == pistols);
    }

    [Fact]
    public void The_deployed_toxin_trooper_poisons_the_ground_which_hurts_its_own_side_too_but_not_it()
    {
        var simulation = Match();
        var world = simulation.World;
        var trooper = Spawn(world, Union, UnitType.Desolator, 30, 30);
        var own = Spawn(world, Union, UnitType.Engineer, 31, 31);
        var enemy = Spawn(world, Allied, UnitType.AllianceMcv, 29, 31);

        simulation.Enqueue(new DeployCommand(Union, simulation.CurrentTick, 0, trooper.Id));
        Run(simulation, 100);

        Assert.True(trooper.Deployed);
        Assert.True(world.RadiationAt(new CellPos(30, 30)) > 0);
        Assert.Equal(trooper.MaxHp, trooper.Hp);
        Assert.True(!own.IsAlive || own.Hp < own.MaxHp, "its own engineer is hurt");
        Assert.True(enemy.Hp < enemy.MaxHp, "the enemy vehicle is hurt, a little");

        // Sent away, it undeploys; the toxin fades.
        simulation.Enqueue(new MoveCommand(Union, simulation.CurrentTick, 1, [trooper.Id], new CellPos(50, 30).Center));
        Run(simulation, 2);
        Assert.False(trooper.Deployed);
        Run(simulation, 700);
        Assert.Equal(0, world.RadiationAt(new CellPos(30, 30)));
    }

    [Fact]
    public void The_time_trooper_jumps_where_it_is_sent_and_phases_in_for_its_distance()
    {
        var simulation = Match();
        var world = simulation.World;
        var trooper = Spawn(world, Allied, UnitType.AllianceChronoLegionnaire, 10, 20);
        simulation.Enqueue(new MoveCommand(Allied, simulation.CurrentTick, 0, [trooper.Id], new CellPos(30, 20).Center));
        var events = Run(simulation, 3);

        var jump = Assert.Single(events.OfType<UnitTeleported>());
        Assert.Equal(new CellPos(30, 20), trooper.Position.ToCell());
        Assert.Equal(new CellPos(30, 20), jump.To.ToCell());
        // 20 cells: 5120 leptons over 48 a tick, less the ticks run since the jump.
        Assert.InRange(trooper.WarpTicks, 100, 106);
        Assert.Null(trooper.MoveTarget);
    }
}
