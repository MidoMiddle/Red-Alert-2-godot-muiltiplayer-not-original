using System.Collections.Generic;
using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Hashing;
using Rts.Simulation.Rules;
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

    /// <summary>A match with <paramref name="settings"/>, played by <paramref name="rules"/> (from the data files, or built in tests).</summary>
    public GameSimulation(SimulationSettings settings, GameRules rules)
    {
        settings.Validate();
        Settings = settings;
        World = new GameWorld(settings.Seed, settings.CreateMap(rules.Land), rules, settings.StartingCredits);
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

    /// <summary>
    /// Measuring only (M4 step 15, the benchmarks): called after each phase of <see cref="Step"/> with its name, so its time and memory
    /// can be taken. Null in a match.
    /// </summary>
    internal System.Action<string>? Probe { get; set; }

    /// <summary>Executes tick <see cref="CurrentTick"/>.</summary>
    public void Step()
    {
        var tick = World.CurrentTick;
        _events.Clear();

        // 0. What appeared between ticks — the buildings of the map, the units of a test — is seen before the commands (M4 step 11).
        VisibilitySystem.Update(World);
        Probe?.Invoke("vision before");

        // 1. Commands of tick T, ordered by (Player, Sequence), validated and applied.
        // By index: a foreach over the interface would box its enumerator every tick (M4 step 15).
        var commands = _commands.TakeForTick(tick);
        for (var i = 0; i < commands.Count; i++)
        {
            _executedCommands.Add(commands[i]);
            CommandProcessor.Apply(World, commands[i], _events);
        }

        Probe?.Invoke("commands");

        // 2. Path searches within the node budget (docs/implementation/M2_unit_control.md, 4.12).
        World.PathRequests.Process(World, _events);
        Probe?.Invoke("paths");

        // 3. Power, when the buildings changed (docs/implementation/M3_base_economy.md, 4.15), production, new buildings grow, harvesters work, repairs.
        PowerSystem.Update(World, _events);
        ProductionSystem.Update(World, _events);
        BuildUpSystem.Update(World, _events);
        HarvesterSystem.Update(World, _events);
        RepairSystem.Update(World, _events);
        Probe?.Invoke("economy");
        TargetingSystem.Update(World);
        Probe?.Invoke("targeting");
        PursuitSystem.Update(World);
        Probe?.Invoke("pursuit");
        WeaponSystem.Update(World, _events);
        Probe?.Invoke("weapons");
        ProjectileSystem.Update(World, _events);
        Probe?.Invoke("projectiles");
        DamageSystem.Update(World, _events);
        Probe?.Invoke("damage");

        // 4. Movement.
        MovementSystem.Update(World, _events);
        Probe?.Invoke("movement");
        World.Spatial.Sync(World);
        Probe?.Invoke("index");

        // 5. Ore grows and spreads (by its interval), then technologies, when the buildings changed.
        OreGrowthSystem.Update(World);
        TechTree.Update(World, _events);
        Probe?.Invoke("ore and tech");

        // 6. The cells of the buildings that fell open (M4 step 9), the players see anew (step 11); entities that died this tick leave the world.
        DeathSystem.Update(World, _events);
        VisibilitySystem.Update(World);
        Probe?.Invoke("death and vision");
        World.RemoveDead();
        Probe?.Invoke("remove dead");

        // 7. Next tick.
        World.CurrentTick = tick + 1;

        // 8. State checksum: describes the state at the start of the new tick.
        if (World.CurrentTick % Settings.HashInterval == 0)
        {
            LastChecksum = new StateChecksum(World.CurrentTick, StateHasher.Compute(World));
        }

        Probe?.Invoke("hash");
    }
}
