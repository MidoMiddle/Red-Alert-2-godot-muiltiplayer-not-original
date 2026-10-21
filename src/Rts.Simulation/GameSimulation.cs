using System.Collections.Generic;
using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Systems;
using Rts.Simulation.World;

namespace Rts.Simulation;

/// <summary>
/// The deterministic simulation: owns the world and the command queue and advances one tick at a time.
/// The order inside <see cref="Step"/> is fixed and changes only deliberately
/// (docs/implementation/conventions.md, section 5).
/// </summary>
public sealed class GameSimulation
{
    private readonly CommandQueue _commands;
    private readonly List<SimEvent> _events = new();

    public GameSimulation(SimulationSettings settings)
    {
        settings.Validate();
        Settings = settings;
        World = new GameWorld(settings.Seed, settings.MapWidthCells, settings.MapHeightCells);
        _commands = new CommandQueue(firstTick: World.CurrentTick);
    }

    public SimulationSettings Settings { get; }

    public GameWorld World { get; }

    /// <summary>Tick that the next <see cref="Step"/> will execute.</summary>
    public int CurrentTick => World.CurrentTick;

    /// <summary>Events of the last executed tick; replaced on every <see cref="Step"/>.</summary>
    public IReadOnlyList<SimEvent> LastTickEvents => _events;

    /// <summary>Schedules a command. Its tick must not have been executed yet.</summary>
    public void Enqueue(Command command) => _commands.Enqueue(command);

    /// <summary>Executes tick <see cref="CurrentTick"/>.</summary>
    public void Step()
    {
        var tick = World.CurrentTick;
        _events.Clear();

        // 1. Commands of tick T, ordered by (Player, Sequence), validated and applied.
        foreach (var command in _commands.TakeForTick(tick))
        {
            CommandProcessor.Apply(World, command, _events);
        }

        // 2. Systems in fixed order.
        MovementSystem.Update(World, _events);

        // 3. Entities that died this tick leave the world.
        World.RemoveDead();

        // 4. Next tick. The state checksum (step 7) is computed here.
        World.CurrentTick = tick + 1;
    }
}
