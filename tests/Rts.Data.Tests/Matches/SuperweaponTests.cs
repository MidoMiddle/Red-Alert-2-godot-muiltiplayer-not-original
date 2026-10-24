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
/// The superweapons (stage 2d of filling the game): the charge, held by low power; the Bastion Field Projector's cover; the missile; the
/// Phase Gate; the storm; the limit of one; the infiltrator starting a charge over. By the game data, without the fog of war; two people
/// play, no bot.
/// </summary>
public sealed class SuperweaponTests
{
    private static readonly PlayerId Union = new(1);
    private static readonly PlayerId Allied = new(2);

    private static GameSimulation Match()
    {
        var simulation = new GameSimulation(
            SimulationSettings.ForMatch(
                new MatchSetup(
                    "super",
                    43,
                    new MatchOptions(StartingCredits: 30_000, FogOfWar: false),
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

    /// <summary>A charged superweapon of the player with power for it.</summary>
    private static Building Ready(GameWorld world, PlayerId player, string id, int x, int y, string plant)
    {
        Build(world, player, plant, x, y + 5);
        Build(world, player, plant, x + 5, y + 5);
        var building = Build(world, player, id, x, y);
        building.ChargeLeft = 0;
        return building;
    }

    [Fact]
    public void A_superweapon_charges_only_with_power_and_tells_when_it_is_ready()
    {
        var simulation = Match();
        var world = simulation.World;
        var curtain = Build(world, Union, "iron_curtain", 10, 10);
        Assert.Equal(300 * 15, curtain.ChargeLeft);

        Run(simulation, 10);
        Assert.True(world.Player(Union).LowPower);
        Assert.Equal(300 * 15, curtain.ChargeLeft);

        Build(world, Union, "nuclear_reactor", 20, 10);
        Run(simulation, 10);
        Assert.True(curtain.ChargeLeft < 300 * 15);

        curtain.ChargeLeft = 1;
        var events = Run(simulation, 1);
        Assert.Contains(events, e => e is SuperweaponReady ready && ready.Building == curtain.Id);

        var refused = new SuperweaponCommand(Union, simulation.CurrentTick, 0, curtain.Id, new CellPos(30, 30), new CellPos(30, 30));
        simulation.Enqueue(new SuperweaponCommand(Union, simulation.CurrentTick, 1, curtain.Id, new CellPos(30, 30), new CellPos(30, 30)));
        Run(simulation, 1);
        simulation.Enqueue(refused with { ExecuteTick = simulation.CurrentTick, Sequence = 2 });
        Run(simulation, 1);
        Assert.Contains(simulation.RefusedCommands, command => command is SuperweaponCommand { Sequence: 2 });
    }

    [Fact]
    public void The_bastion_field_covers_a_tank_for_fifty_seconds_and_kills_the_soldier_beside_it()
    {
        var simulation = Match();
        var world = simulation.World;
        var projector = Ready(world, Union, "iron_curtain", 10, 10, "nuclear_reactor");
        var tank = Spawn(world, Union, UnitType.HeavyTank, 30, 30);
        var soldier = Spawn(world, Union, UnitType.Conscript, 31, 30);
        simulation.Enqueue(new SuperweaponCommand(Union, simulation.CurrentTick, 0, projector.Id, new CellPos(30, 30), new CellPos(30, 30)));
        Run(simulation, 1);

        Assert.False(soldier.IsAlive);
        Assert.True(tank.IsInvulnerable(world.CurrentTick));
        var cannon = world.Rules.Database.IndexOfWeapon("alliance_light_cannon");
        world.Hits.Add(new Rts.Simulation.Combat.Hit(tank.Id, tank.Id, Allied, cannon, tank.Position, tank.Id));
        Run(simulation, 1);
        Assert.Equal(tank.MaxHp, tank.Hp);

        Run(simulation, SuperweaponSystem.CurtainTicks);
        Assert.False(tank.IsInvulnerable(world.CurrentTick));
        // It charges anew from the tick it fired.
        Assert.InRange(projector.ChargeLeft, 300 * 15 - SuperweaponSystem.CurtainTicks - 3, 300 * 15 - SuperweaponSystem.CurtainTicks);
    }

    [Fact]
    public void The_missile_falls_on_its_target_and_concrete_holds()
    {
        var simulation = Match();
        var world = simulation.World;
        var silo = Ready(world, Union, "nuclear_missile_silo", 6, 20, "nuclear_reactor");
        var tank = Spawn(world, Allied, UnitType.AllianceLightTank, 40, 40);
        var yard = Build(world, Allied, "alliance_construction_yard", 42, 42);
        var events = new List<SimEvent>();
        simulation.Enqueue(new SuperweaponCommand(Union, simulation.CurrentTick, 0, silo.Id, new CellPos(40, 40), new CellPos(40, 40)));
        events.AddRange(Run(simulation, 600, () => !tank.IsAlive));

        Assert.Contains(events, e => e is SuperweaponFired { Kind: Rts.Simulation.Rules.SuperweaponKind.Missile });
        Assert.False(tank.IsAlive);
        Assert.True(yard.IsAlive, "concrete takes 8%");
        Run(simulation, 2);
        Assert.True(world.RadiationAt(new CellPos(40, 40)) > 0);
    }

    [Fact]
    public void The_phase_gate_moves_a_tank_and_kills_a_soldier_and_a_tank_sent_into_the_water()
    {
        var simulation = Match();
        var world = simulation.World;
        var gate = Ready(world, Allied, "alliance_chronosphere", 50, 10, "alliance_power_plant");
        var tank = Spawn(world, Allied, UnitType.AllianceLightTank, 20, 20);
        var other = Spawn(world, Allied, UnitType.AllianceLightTank, 21, 21);
        var soldier = Spawn(world, Allied, UnitType.AllianceRifleman, 19, 20);
        world.Map.SetLand(new CellPos(41, 41), LandType.Deep);
        simulation.Enqueue(new SuperweaponCommand(Allied, simulation.CurrentTick, 0, gate.Id, new CellPos(20, 20), new CellPos(40, 40)));
        Run(simulation, 1);

        Assert.Equal(new CellPos(40, 40), tank.Position.ToCell());
        Assert.False(other.IsAlive, "it would land on water");
        Assert.False(soldier.IsAlive);

        var at = tank.Position;
        simulation.Enqueue(new MoveCommand(Allied, simulation.CurrentTick, 1, [tank.Id], new CellPos(30, 30).Center));
        Run(simulation, SuperweaponSystem.GateFreeze - 5);
        Assert.Equal(at, tank.Position);
    }

    [Fact]
    public void The_storm_strikes_after_its_delay_and_hits_its_target()
    {
        var simulation = Match();
        var world = simulation.World;
        var storm = Ready(world, Allied, "alliance_weather_control_device", 50, 10, "alliance_power_plant");
        var target = Build(world, Union, "reactor", 30, 30);
        simulation.Enqueue(new SuperweaponCommand(Allied, simulation.CurrentTick, 0, storm.Id, new CellPos(30, 30), new CellPos(30, 30)));
        var early = Run(simulation, SuperweaponSystem.StormDelay);
        Assert.DoesNotContain(early, e => e is LightningStrike);
        Assert.Equal(target.MaxHp, target.Hp);

        var events = Run(simulation, SuperweaponSystem.StormTicks + 2);
        Assert.True(events.OfType<LightningStrike>().Count() >= SuperweaponSystem.StormTicks / SuperweaponSystem.StrikeEvery);
        Assert.True(!target.IsAlive || target.Hp < target.MaxHp);
        Assert.Empty(world.Storms);
    }

    [Fact]
    public void A_player_has_one_of_each_and_an_infiltrator_starts_the_charge_over()
    {
        var simulation = Match();
        var world = simulation.World;
        Build(world, Union, "construction_yard", 10, 10);
        Build(world, Union, "radar", 14, 10);
        Build(world, Union, "tech_center", 14, 14);
        var curtain = Ready(world, Union, "iron_curtain", 20, 20, "nuclear_reactor");
        TechTree.Update(world);
        var index = world.Rules.Database.IndexOfBuilding("iron_curtain");
        Assert.True(world.Player(Union).CanBuildBuilding(index));
        Assert.False(SuperweaponSystem.UnderLimit(world, Union, index));

        var spy = Spawn(world, Allied, UnitType.AllianceSpy, 18, 21);
        simulation.Enqueue(new EnterCommand(Allied, simulation.CurrentTick, 0, [spy.Id], curtain.Id));
        Run(simulation, 300, () => !spy.IsAlive);
        Assert.False(spy.IsAlive);
        Assert.True(curtain.ChargeLeft > 300 * 15 - 300);
    }
}
