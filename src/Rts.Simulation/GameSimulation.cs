using System.Collections.Generic;
using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Hashing;
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
    private readonly List<Command> _executedCommands = new();

    public GameSimulation(SimulationSettings settings)
    {
        settings.Validate();
        Settings = settings;
        World = new GameWorld(settings.Seed, settings.CreateMap());
        _commands = new CommandQueue(firstTick: World.CurrentTick);
    }

    public SimulationSettings Settings { get; }

    public GameWorld World { get; }

    /// <summary>Tick that the next <see cref="Step"/> will execute.</summary>
    public int CurrentTick => World.CurrentTick;

    /// <summary>
    /// Checksum computed at the end of the last step whose new tick is divisible by
    /// <see cref="SimulationSettings.HashInterval"/>; null before the first one.
    /// </summary>
    public StateChecksum? LastChecksum { get; private set; }

    /// <summary>Events of the last executed tick; replaced on every <see cref="Step"/>.</summary>
    public IReadOnlyList<SimEvent> LastTickEvents => _events;

    /// <summary>
    /// Every command taken from the queue so far, in execution order. This input log is what a replay
    /// stores; the state can always be rebuilt from settings plus this list.
    /// </summary>
    public IReadOnlyList<Command> ExecutedCommands => _executedCommands;

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
            _executedCommands.Add(command);
            CommandProcessor.Apply(World, command, _events);
        }

        // 2. Systems in fixed order.
        MovementSystem.Update(World, _events);

        // 3. Entities that died this tick leave the world.
        World.RemoveDead();

        // 4. Next tick.
        World.CurrentTick = tick + 1;

        // 5. State checksum: describes the state at the start of the new tick.
        if (World.CurrentTick % Settings.HashInterval == 0)
        {
            LastChecksum = new StateChecksum(World.CurrentTick, StateHasher.Compute(World));
        }
    }
}
