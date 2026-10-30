using Rts.Simulation.Combat;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Hashing;
using Rts.Simulation.Map;
using Rts.Simulation.Math;
using Rts.Simulation.Systems;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Combat;

/// <summary>
/// The damage of the shots that landed (docs/implementation/M4_combat.md, 4.9): an order of its own, the target and the area, walls,
/// thresholds and deaths. Every number is worked out by hand in the comment next to it.
/// </summary>
public sealed class DamageSystemTests
{
    private static readonly PlayerId Player1 = new(1);
    private static readonly PlayerId Player2 = new(2);

    private static GameWorld World() => new(seed: 1, widthCells: 40, heightCells: 40, TestRules.Standard);

    private static int Weapon(string id) => TestRules.Standard.Database.IndexOfWeapon(id);

    private static Hit HitOn(Entity target, string weapon, WorldPos point, int source = 900) =>
        new(new EntityId(source), new EntityId(source), Player1, Weapon(weapon), point, target.Id);

    private static Hit HitAt(WorldPos point, string weapon, int source = 900) =>
        new(new EntityId(source), new EntityId(source), Player1, Weapon(weapon), point, null);

    private static List<SimEvent> Apply(GameWorld world, params Hit[] hits)
    {
        var events = new List<SimEvent>();
        world.Hits.AddRange(hits);
        DamageSystem.Update(world, events);
        Assert.Empty(world.Hits);
        return events;
    }

    [Fact]
    public void The_order_in_which_the_hits_came_changes_nothing()
    {
        (ulong Hash, int[] Hp, List<SimEvent> Events) Run(Func<Hit[], Hit[]> order)
        {
            var world = World();
            var tank = world.SpawnUnit(Player2, UnitType.HeavyTank, new CellPos(10, 10).Center);
            var conscripts = Enumerable.Range(0, 3).Select(i => world.SpawnUnit(Player2, UnitType.Conscript, new CellPos(11 + i, 10).Center)).ToArray();
            Hit[] hits =
            [
                HitOn(tank, "heavy_tank_cannon", tank.Position, source: 901),
                HitAt(conscripts[0].Position, "flak_track_gun", source: 902),
                HitOn(conscripts[1], "tesla_tank_bolt", conscripts[1].Position, source: 903),
                HitOn(conscripts[1], "conscript_rifle", conscripts[1].Position, source: 904),
                HitAt(conscripts[2].Position + new WorldPos(100, 0), "flak_track_gun", source: 905),
                HitOn(tank, "tesla_tank_bolt", tank.Position, source: 906),
            ];
            var events = Apply(world, order(hits));
            return (StateHasher.Compute(world), [tank.Hp, .. conscripts.Select(c => c.Hp)], events);
        }

        var forward = Run(hits => hits);
        var backward = Run(hits => Enumerable.Reverse(hits).ToArray());
        var shuffled = Run(hits => [hits[3], hits[0], hits[5], hits[2], hits[4], hits[1]]);

        Assert.Equal(forward.Hash, backward.Hash);
        Assert.Equal(forward.Hash, shuffled.Hash);
        Assert.Equal(forward.Hp, backward.Hp);
        Assert.Equal(forward.Events, shuffled.Events);
        // The conscript under the bolt died (135 > 125); the rifle shot after it changed nothing.
        Assert.Equal(0, forward.Hp[2]);
    }

    [Fact]
    public void A_tesla_bolt_kills_a_conscript_with_one_shot()
    {
        var simulation = new GameSimulation(new SimulationSettings(1, new MapGrid(40, 40, TestRules.Standard.Land)), TestRules.Standard);
        var tank = simulation.World.SpawnUnit(Player1, UnitType.TeslaTank, new CellPos(10, 10).Center);
        var conscript = simulation.World.SpawnUnit(Player2, UnitType.Conscript, new CellPos(10, 7).Center);
        tank.Target = conscript.Id;

        simulation.Step();

        // 135 × 100 % against flak armor is more than its 125 hit points.
        Assert.Contains(new EntityDestroyed(0, conscript.Id, tank.Id, DeathCause.Weapon, Weapon("tesla_tank_bolt"), Player1), simulation.LastTickEvents);
        Assert.False(simulation.World.TryGet(conscript.Id, out _));
    }

    [Fact]
    public void A_rhino_shell_takes_90_off_a_rhino()
    {
        var world = World();
        var tank = world.SpawnUnit(Player2, UnitType.HeavyTank, new CellPos(10, 10).Center);

        var events = Apply(world, HitOn(tank, "heavy_tank_cannon", tank.Position));

        Assert.Equal(310, tank.Hp);
        Assert.Contains(new EntityDamaged(0, tank.Id, new EntityId(900), 90, 310), events);
    }

    [Fact]
    public void Flak_hurts_everyone_in_its_area_by_the_fall_off_friend_or_foe()
    {
        var world = World();
        var centre = new CellPos(10, 10).Center;
        var enemies = new[] { 0, 128, 256, 300 }.Select(d => world.SpawnUnit(Player2, UnitType.Conscript, centre + new WorldPos(d, 0))).ToArray();
        var friend = world.SpawnUnit(Player1, UnitType.Conscript, centre + new WorldPos(0, 128));

        Apply(world, HitAt(centre, "flak_track_gun"));

        // 25 × 125 % against flak armor (RA2 FlakTWH), the whole of it to the edge of the cell (PercentAtMax 1): 31 within it, nothing
        // beyond.
        Assert.Equal([94, 94, 94, 125], enemies.Select(enemy => enemy.Hp));
        Assert.Equal(94, friend.Hp);
    }

    [Fact]
    public void A_rhino_shell_splashes_the_infantry_next_to_its_target()
    {
        var world = World();
        var tank = world.SpawnUnit(Player2, UnitType.HeavyTank, new CellPos(10, 10).Center);
        var conscript = world.SpawnUnit(Player2, UnitType.Conscript, tank.Position + new WorldPos(50, 0));

        Apply(world, HitOn(tank, "heavy_tank_cannon", tank.Position));

        // 90 × 25 % = 22 against flak; 0.3 cells is 76 leptons, at 50 the share 1 − 0.5 × 50 / 76 = 0.671: 14.
        Assert.Equal((310, 111), (tank.Hp, conscript.Hp));
    }

    [Fact]
    public void A_shell_takes_58_off_a_reactor_and_enough_shells_destroy_it()
    {
        var world = World();
        var reactor = world.CreateBuilding(Player2, TestRules.BuildingDef("reactor"), new CellPos(20, 10), true, null);
        var point = new CellPos(20, 10).Center;

        Apply(world, HitOn(reactor, "heavy_tank_cannon", point));
        Assert.Equal(442, reactor.Hp);    // 90 × 65 % against wood = 58.5, of the 500 of the test rules

        var events = Apply(world, Enumerable.Range(0, 12).Select(i => HitOn(reactor, "heavy_tank_cannon", point, source: 910 + i)).ToArray());

        Assert.False(reactor.IsAlive);
        Assert.Single(events.OfType<EntityDestroyed>());
        // Its cells open at the end of the tick (step 9).
        Assert.True(world.Map.HasBuilding(new CellPos(21, 11)));
        DeathSystem.Update(world, events);
        Assert.False(world.Map.HasBuilding(new CellPos(21, 11)));
    }

    [Fact]
    public void A_wall_takes_damage_only_from_a_warhead_that_damages_walls()
    {
        var world = World();
        var wall = world.CreateBuilding(Player2, TestRules.BuildingDef("wall"), new CellPos(20, 10), true, null);
        var point = new CellPos(20, 10).Center;

        Apply(world, HitOn(wall, "conscript_rifle", point));
        Assert.Equal(500, wall.Hp);

        Apply(world, HitOn(wall, "heavy_tank_cannon", point));
        Assert.Equal(446, wall.Hp);    // 90 × 60 % against concrete (RA2 AP) = 54
    }

    [Fact]
    public void Damage_across_a_threshold_reports_the_new_state()
    {
        var world = World();
        var conscript = world.SpawnUnit(Player2, UnitType.Conscript, new CellPos(10, 10).Center);

        // A Rhino shell does 22 against flak; three of them leave 59 of 125, under a half.
        var events = Apply(world, [.. Enumerable.Range(0, 3).Select(i => HitOn(conscript, "heavy_tank_cannon", conscript.Position, source: 920 + i))]);

        Assert.Equal(59, conscript.Hp);
        Assert.Equal([new DamageStateChanged(0, conscript.Id, DamageState.Damaged)], events.OfType<DamageStateChanged>());
    }

    [Fact]
    public void What_died_takes_no_more_damage_and_fires_no_more()
    {
        var simulation = new GameSimulation(new SimulationSettings(1, new MapGrid(40, 40, TestRules.Standard.Land)), TestRules.Standard);
        var world = simulation.World;
        var conscript = world.SpawnUnit(Player2, UnitType.Conscript, new CellPos(10, 10).Center);
        var victim = world.SpawnUnit(Player1, UnitType.Conscript, new CellPos(10, 12).Center);
        conscript.Target = victim.Id;

        var events = Apply(world, HitOn(conscript, "tesla_tank_bolt", conscript.Position, source: 930), HitOn(conscript, "tesla_tank_bolt", conscript.Position, source: 931));
        simulation.Step();

        Assert.Single(events.OfType<EntityDestroyed>());
        Assert.Single(events.OfType<EntityDamaged>());
        Assert.DoesNotContain(simulation.LastTickEvents, e => e is WeaponFired);
        Assert.Equal(125, victim.Hp);
    }

    [Fact]
    public void Two_rhinos_fight_to_the_same_end_every_time()
    {
        (int Tick, ulong Hash) Fight()
        {
            var simulation = new GameSimulation(new SimulationSettings(1, new MapGrid(40, 40, TestRules.Standard.Land)), TestRules.Standard);
            var first = simulation.World.SpawnUnit(Player1, UnitType.HeavyTank, new CellPos(10, 10).Center);
            var second = simulation.World.SpawnUnit(Player2, UnitType.HeavyTank, new CellPos(14, 10).Center);
            first.Target = second.Id;
            second.Target = first.Id;
            for (var tick = 0; tick < 1000; tick++)
            {
                simulation.Step();
                if (simulation.LastTickEvents.OfType<EntityDestroyed>().Any())
                {
                    return (tick, StateHasher.Compute(simulation.World));
                }
            }

            return (-1, 0);
        }

        var fight = Fight();

        // Five shells of 90 take a Rhino's 400: the fifth lands 4 reloads after the first, plus the flight.
        Assert.InRange(fight.Tick, 4 * 65, 6 * 65);
        Assert.Equal(fight, Fight());
    }
}
