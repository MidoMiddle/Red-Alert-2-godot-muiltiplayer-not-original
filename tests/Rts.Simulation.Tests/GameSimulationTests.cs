using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.World;
using static Rts.Simulation.Tests.Commands.CommandFactory;

namespace Rts.Simulation.Tests;

public sealed class GameSimulationTests
{
    private static GameSimulation NewSimulation(ulong seed = 1) => new(new SimulationSettings(seed));

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
        simulation.Enqueue(Move(player: 1, tick: 1, sequence: 1, units: [1], x: 128 + 64, y: 128));

        simulation.Step();
        Assert.Equal([new UnitSpawned(0, new EntityId(1), new PlayerId(1), new WorldPos(128, 128))], simulation.LastTickEvents);

        // Tick 1: the order is applied, then the unit moves 32 of 64 leptons.
        simulation.Step();
        Assert.Equal([new UnitMoveStarted(1, new EntityId(1), new WorldPos(192, 128))], simulation.LastTickEvents);

        simulation.Step();
        Assert.Equal([new UnitArrived(2, new EntityId(1), new WorldPos(192, 128))], simulation.LastTickEvents);

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

        var unit = Assert.IsType<TestUnit>(Assert.Single(simulation.World.Entities));
        Assert.Equal(new WorldPos(128 + TestUnit.DefaultSpeed, 128), unit.Position);
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
        }

        Assert.NotEmpty(a.World.Entities);
    }

    [Fact]
    public void Invalid_settings_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => new GameSimulation(new SimulationSettings(1) { TicksPerSecond = 0 }));
        Assert.Throws<ArgumentException>(() => new GameSimulation(new SimulationSettings(1) { MapWidthCells = -1 }));
    }

    private static List<string> Describe(GameSimulation simulation) => simulation.World.Entities
        .Cast<TestUnit>()
        .Select(u => $"{u.Id} {u.Owner} {u.Position} {u.MoveTarget}")
        .ToList();
}
