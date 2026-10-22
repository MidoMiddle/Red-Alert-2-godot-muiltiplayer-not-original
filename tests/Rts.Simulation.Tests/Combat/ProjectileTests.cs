using Rts.Simulation.Combat;
using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Math;
using Rts.Simulation.Systems;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Combat;

/// <summary>
/// Shots in flight (docs/implementation/M4_combat.md, 4.8): an instant one lands at once, a ballistic one after its flight time at its
/// scattered aim point, a homing one on its target or where it fell; a shot at a target that died still lands.
/// </summary>
public sealed class ProjectileTests
{
    private static readonly PlayerId Player1 = new(1);
    private static readonly PlayerId Player2 = new(2);

    private static GameSimulation Match(ulong seed = 1) =>
        new(new SimulationSettings(seed, new MapGrid(40, 40, TestRules.Standard.Land)), TestRules.Standard);

    private static Unit Spawn(GameSimulation simulation, PlayerId owner, UnitType type, WorldPos position) =>
        simulation.World.SpawnUnit(owner, type, position);

    private static List<ProjectileImpact> Play(GameSimulation simulation, int ticks, System.Action? each = null)
    {
        var impacts = new List<ProjectileImpact>();
        for (var i = 0; i < ticks; i++)
        {
            simulation.Step();
            impacts.AddRange(simulation.LastTickEvents.OfType<ProjectileImpact>());
            each?.Invoke();
        }

        return impacts;
    }

    [Fact]
    public void An_instant_shot_lands_on_the_tick_it_is_fired_and_never_flies()
    {
        var simulation = Match();
        var tank = Spawn(simulation, Player1, UnitType.TeslaTank, new CellPos(10, 10).Center);
        var target = Spawn(simulation, Player2, UnitType.Conscript, new CellPos(10, 7).Center);
        tank.Target = target.Id;

        var impacts = Play(simulation, 1);

        Assert.Equal([new ProjectileImpact(0, null, tank.Id, TestRules.Standard.Database.IndexOfWeapon("tesla_tank_bolt"), target.Position, target.Id)], impacts);
        Assert.DoesNotContain(simulation.World.Entities, entity => entity is Projectile);
    }

    [Fact]
    public void A_shell_flies_its_distance_over_its_speed_rounded_up_and_lands_at_its_aim_point()
    {
        var simulation = Match();
        var tank = Spawn(simulation, Player1, UnitType.HeavyTank, new CellPos(10, 10).Center);
        var target = Spawn(simulation, Player2, UnitType.Conscript, new CellPos(10, 10).Center + new WorldPos(1024, 0));
        tank.TurretFacing = BAngle.East;
        tank.Target = target.Id;
        simulation.Step();
        var shell = simulation.World.Entities.OfType<Projectile>().Single();
        Assert.Equal(26, shell.FlightTicks);    // 1024 / 40 = 25.6

        var impacts = Play(simulation, 13);
        Assert.Empty(impacts);
        Assert.Equal(tank.Position + new WorldPos(512, 0), shell.Position);    // halfway after 13 of 26 ticks

        impacts = Play(simulation, 13);
        var impact = Assert.Single(impacts);
        Assert.Equal((26, shell.Id, target.Position, target.Id), (impact.Tick, impact.Projectile!.Value, impact.Point, impact.Target!.Value));
        simulation.Step();
        Assert.False(simulation.World.TryGet(shell.Id, out _));
    }

    [Fact]
    public void Flak_scatters_within_its_inaccuracy_the_same_way_every_time()
    {
        List<WorldPos> Landings(ulong seed)
        {
            var simulation = Match(seed);
            // A tank takes 2 of a flak hit (10 % of 25): it outlives the 420 ticks.
            var flakTrack = Spawn(simulation, Player1, UnitType.SupportVehicle, new CellPos(10, 10).Center);
            var target = Spawn(simulation, Player2, UnitType.HeavyTank, new CellPos(10, 7).Center);
            flakTrack.Target = target.Id;
            return Play(simulation, 420).Select(impact => impact.Point).ToList();
        }

        var landings = Landings(1);
        var aim = new CellPos(10, 7).Center;

        Assert.True(landings.Count >= 8);
        Assert.All(landings, point => Assert.InRange(DamageCalculator.Distance(point, aim), 0, 128));
        Assert.True(landings.Distinct().Count() > 1);
        Assert.Equal(landings, Landings(1));
        Assert.NotEqual(landings, Landings(2));
    }

    [Fact]
    public void A_rocket_turns_at_most_its_turn_rate_and_hits_a_target_that_walks_away()
    {
        var simulation = Match();
        // A rocket of 30 leptons a tick after a conscript of 24 walking across: from 3 cells it closes in about 71 ticks of its 90.
        var rocketeer = Spawn(simulation, Player1, UnitType.Rocketeer, new CellPos(10, 10).Center);
        var target = Spawn(simulation, Player2, UnitType.Conscript, new CellPos(10, 13).Center);
        rocketeer.Target = target.Id;
        simulation.Enqueue(new MoveCommand(Player2, 0, 0, [target.Id], new CellPos(30, 13).Center));
        simulation.Step();
        var rocket = simulation.World.Entities.OfType<Projectile>().Single();
        rocketeer.Target = null;
        var facing = rocket.Facing;
        var turns = new List<int>();
        var targetAt = new Dictionary<int, WorldPos>();

        var impacts = Play(simulation, 90, () =>
        {
            turns.Add(System.Math.Abs((int)BAngle.Difference(facing, rocket.Facing)));
            facing = rocket.Facing;
            targetAt[simulation.CurrentTick - 1] = target.Position;
        });

        var impact = Assert.Single(impacts);
        Assert.Equal(target.Id, impact.Target);
        // The target moves after the shots in the tick, so it is within a step of where the rocket hit it.
        Assert.InRange(DamageCalculator.Distance(impact.Point, targetAt[impact.Tick]), 0, 64 + 24);
        Assert.All(turns, turn => Assert.InRange(turn, 0, 8192));
    }

    [Fact]
    public void A_rocket_that_cannot_catch_its_target_falls_when_its_lifetime_is_up()
    {
        var simulation = Match();
        var rocketeer = Spawn(simulation, Player1, UnitType.Rocketeer, new CellPos(10, 5).Center);
        var target = Spawn(simulation, Player2, UnitType.HeavyTank, new CellPos(10, 10).Center);
        rocketeer.Target = target.Id;
        // The tank drives 36 leptons a tick, the rocket flies 30.
        simulation.Enqueue(new MoveCommand(Player2, 0, 0, [target.Id], new CellPos(10, 39).Center));
        simulation.Step();
        rocketeer.Target = null;

        var impact = Assert.Single(Play(simulation, 100));

        Assert.Equal((90, (EntityId?)null), (impact.Tick, impact.Target));    // lifetime 6 s = 90 ticks
    }

    [Fact]
    public void A_shell_at_a_target_that_died_still_lands_at_its_point()
    {
        var simulation = Match();
        var tank = Spawn(simulation, Player1, UnitType.HeavyTank, new CellPos(10, 10).Center);
        var target = Spawn(simulation, Player2, UnitType.Conscript, new CellPos(14, 10).Center);
        tank.TurretFacing = BAngle.East;
        tank.Target = target.Id;
        simulation.Step();
        target.MarkDead();

        var impact = Assert.Single(Play(simulation, 30));

        Assert.Equal((new CellPos(14, 10).Center, (EntityId?)null), (impact.Point, impact.Target));
    }

    [Fact]
    public void A_rocket_at_a_target_that_died_flies_to_where_it_last_saw_it()
    {
        var simulation = Match();
        var rocketeer = Spawn(simulation, Player1, UnitType.Rocketeer, new CellPos(10, 10).Center);
        var target = Spawn(simulation, Player2, UnitType.Conscript, new CellPos(10, 15).Center);
        rocketeer.Target = target.Id;
        simulation.Enqueue(new MoveCommand(Player2, 0, 0, [target.Id], new CellPos(30, 15).Center));
        Play(simulation, 10);
        rocketeer.Target = null;
        var rocket = simulation.World.Entities.OfType<Projectile>().Single();
        target.MarkDead();
        simulation.Step();
        var lastSeen = rocket.LastTargetPoint;

        var impact = Assert.Single(Play(simulation, 80));

        Assert.Equal((lastSeen, (EntityId?)null), (impact.Point, impact.Target));
    }

    [Fact]
    public void A_shot_takes_the_next_id_of_the_world()
    {
        var simulation = Match();
        var tank = Spawn(simulation, Player1, UnitType.HeavyTank, new CellPos(10, 10).Center);
        var target = Spawn(simulation, Player2, UnitType.Conscript, new CellPos(14, 10).Center);
        tank.TurretFacing = BAngle.East;
        tank.Target = target.Id;
        var next = simulation.World.NextEntityId;

        simulation.Step();

        Assert.Equal(next, simulation.World.Entities.OfType<Projectile>().Single().Id);
        Assert.Equal(next.Value + 1, simulation.World.NextEntityId.Value);
    }
}
