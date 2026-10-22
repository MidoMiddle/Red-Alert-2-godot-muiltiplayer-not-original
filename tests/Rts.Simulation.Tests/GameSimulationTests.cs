using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Pathfinding;
using Rts.Simulation.Hashing;
using Rts.Simulation.World;
using static Rts.Simulation.Tests.Commands.CommandFactory;

namespace Rts.Simulation.Tests;

public sealed class GameSimulationTests
{
    private static GameSimulation NewSimulation(ulong seed = 1) => new(new SimulationSettings(seed), TestRules.Standard);

    [Fact]
    public void Step_advances_the_tick()
    {
        var simulation = NewSimulation();

        simulation.Step();
        simulation.Step();

        Assert.Equal(2, simulation.CurrentTick);
        Assert.Equal(2, simulation.World.CurrentTick);
    }

    [Fact]
    public void Commands_apply_on_their_tick_only()
    {
        var simulation = NewSimulation();
        simulation.Enqueue(Spawn(player: 1, tick: 2, sequence: 0));

        simulation.Step();
        simulation.Step();
        Assert.Empty(simulation.World.Entities);

        simulation.Step();
        Assert.Single(simulation.World.Entities);
    }

    [Fact]
    public void Command_for_an_executed_tick_is_rejected()
    {
        var simulation = NewSimulation();
        simulation.Step();

        Assert.Throws<InvalidOperationException>(() => simulation.Enqueue(Spawn(player: 1, tick: 0, sequence: 0)));
    }

    [Fact]
    public void Events_describe_the_last_tick_only()
    {
        var simulation = NewSimulation();
        simulation.Enqueue(Spawn(player: 1, tick: 0, sequence: 0, x: 128, y: 128));
        simulation.Enqueue(Move(player: 1, tick: 1, sequence: 1, units: [1], x: 128 + 48, y: 128 + 48));

        simulation.Step();
        Assert.Equal([new UnitSpawned(0, new EntityId(1), new PlayerId(1), UnitType.Conscript, new WorldPos(128, 128))], simulation.LastTickEvents);

        // Tick 1: the order is applied and the path is found within the same tick (one cell: start and goal share it).
        // A soldier stops on the spot nearest to the click: the south-east corner (192, 192), 91 leptons away.
        simulation.Step();
        Assert.Equal(
            [
                new UnitMoveStarted(1, new EntityId(1), new WorldPos(176, 176)),
                new UnitPathFound(1, new EntityId(1), PathStatus.Found, 1),
            ],
            simulation.LastTickEvents);

        // Ticks 1–3 walk 24 leptons each, tick 4 covers the last 19.
        simulation.Step();
        simulation.Step();
        Assert.Empty(simulation.LastTickEvents);
        simulation.Step();
        Assert.Equal([new UnitArrived(4, new EntityId(1), new WorldPos(192, 192))], simulation.LastTickEvents);

        simulation.Step();
        Assert.Empty(simulation.LastTickEvents);
    }

    [Fact]
    public void Ordered_unit_moves_on_the_same_tick_the_command_applies()
    {
        var simulation = NewSimulation();
        simulation.Enqueue(Spawn(player: 1, tick: 0, sequence: 0, x: 128, y: 128));
        simulation.Enqueue(Move(player: 1, tick: 1, sequence: 1, units: [1], x: 1000, y: 128));

        simulation.Step();
        simulation.Step();

        var unit = Assert.IsType<Unit>(Assert.Single(simulation.World.Entities));
        Assert.Equal(new WorldPos(128 + TestRules.Unit(UnitType.Conscript).Speed, 128), unit.Position);
    }

    [Fact]
    public void Dead_entities_are_removed_at_the_end_of_the_tick()
    {
        var simulation = NewSimulation();
        simulation.Enqueue(Spawn(player: 1, tick: 0, sequence: 0));
        simulation.Step();
        simulation.World.Entities[0].MarkDead();

        simulation.Step();

        Assert.Empty(simulation.World.Entities);
    }

    [Fact]
    public void Same_commands_give_identical_worlds_on_every_tick()
    {
        var a = NewSimulation(seed: 42);
        var b = NewSimulation(seed: 42);
        var random = new TestRandom(99);
        uint sequence = 0;

        for (var tick = 0; tick < 300; tick++)
        {
            if (random.NextInt(3) == 0)
            {
                var player = 1 + random.NextInt(2);
                var command = random.NextInt(2) == 0
                    ? (Rts.Simulation.Commands.Command)Spawn(player, tick, sequence++, random.NextInt(64 * 256), random.NextInt(64 * 256))
                    : Move(player, tick, sequence++, Enumerable.Range(1, 1 + random.NextInt(10)).ToArray(), random.NextInt(64 * 256), random.NextInt(64 * 256));
                a.Enqueue(command);
                b.Enqueue(command);
            }

            a.Step();
            b.Step();
            Assert.Equal(Describe(a), Describe(b));
            Assert.Equal(a.LastTickEvents, b.LastTickEvents);
            Assert.Equal(a.LastChecksum, b.LastChecksum);
        }

        Assert.NotEmpty(a.World.Entities);
    }

    [Fact]
    public void Checksum_is_computed_after_every_step_by_default()
    {
        var simulation = NewSimulation();
        Assert.Null(simulation.LastChecksum);

        simulation.Step();

        Assert.Equal(1, simulation.LastChecksum!.Value.Tick);
        Assert.Equal(StateHasher.Compute(simulation.World), simulation.LastChecksum.Value.Value);
    }

    [Fact]
    public void Checksum_follows_the_hash_interval()
    {
        var simulation = new GameSimulation(new SimulationSettings(1) { HashInterval = 5 }, TestRules.Standard);
        var checksumTicks = new List<int?>();

        for (var i = 0; i < 12; i++)
        {
            simulation.Step();
            checksumTicks.Add(simulation.LastChecksum?.Tick);
        }

        Assert.Equal([null, null, null, null, 5, 5, 5, 5, 5, 10, 10, 10], checksumTicks);
    }

    [Fact]
    public void Different_commands_give_different_checksums()
    {
        var a = NewSimulation();
        var b = NewSimulation();
        a.Enqueue(Spawn(player: 1, tick: 0, sequence: 0, x: 100, y: 100));
        // Another cell: within one cell a soldier is put on the same spot either way.
        b.Enqueue(Spawn(player: 1, tick: 0, sequence: 0, x: 400, y: 100));

        a.Step();
        b.Step();

        Assert.NotEqual(a.LastChecksum, b.LastChecksum);
    }

    [Fact]
    public void Invalid_settings_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => new GameSimulation(new SimulationSettings(1) { TicksPerSecond = 0 }, TestRules.Standard));
        Assert.Throws<ArgumentException>(() => new GameSimulation(new SimulationSettings(1) { MapWidthCells = -1 }, TestRules.Standard));
    }

    private static List<string> Describe(GameSimulation simulation) => simulation.World.Entities
        .Cast<Unit>()
        .Select(u => $"{u.Id} {u.Owner} {u.Position} {u.MoveTarget}")
        .ToList();
}
