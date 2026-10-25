using System;
using System.Collections.Generic;
using Rts.Simulation.Ai;
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
    private readonly List<Command> _refusedCommands = new();

    // The bots of the match by player, thinking in that order; a replay is played without them (M5, 4.4).
    private readonly SortedList<int, (IAiController Bot, AiTurn Turn)> _bots = new();

    /// <summary>A match with <paramref name="settings"/>, played by <paramref name="rules"/> (from the data files, or built in tests).</summary>
    public GameSimulation(SimulationSettings settings, GameRules rules)
    {
        settings.Validate(rules.Database);
        Settings = settings;
        World = new GameWorld(settings.Seed, settings.CreateMap(rules.Land), rules, settings.StartingCredits, settings.Match);
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

    /// <summary>
    /// The commands that changed nothing — refused by the checks or with nothing left to do — in execution order (M5 step 4). For the
    /// tests and the tools, not state: a bot must never give one (docs/implementation/M5_first_bot.md, 6, «protection from wrong
    /// commands»).
    /// </summary>
    public IReadOnlyList<Command> RefusedCommands => _refusedCommands;

    /// <summary>Schedules a command. Its tick must not have been executed yet.</summary>
    public void Enqueue(Command command) => _commands.Enqueue(command);

    /// <summary>
    /// Lets <paramref name="bot"/> play its player in this match (docs/implementation/M5_first_bot.md, 4.2): it thinks at the end of every
    /// tick. Only in a live match: a replay keeps the commands of the bots and is played without them. One bot a player, players 1–8.
    /// </summary>
    public void AttachAi(IAiController bot)
    {
        var player = bot.Player;
        if (player.Value < 1 || player.Value > PlayerId.MaxPlayers)
        {
            throw new ArgumentException($"A bot plays one of the players 1–{PlayerId.MaxPlayers}, not {player}.", nameof(bot));
        }

        if (_bots.ContainsKey(player.Value))
        {
            throw new InvalidOperationException($"{player} has a bot already.");
        }

        _bots.Add(player.Value, (bot, new AiTurn(this, player)));
    }

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
            if (!CommandProcessor.Apply(World, commands[i], _events))
            {
                _refusedCommands.Add(commands[i]);
            }
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
        TechBuildingSystem.Update(World, _events);
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
        ErasureSystem.Update(World, _events);
        RadiationSystem.Update(World, _events);
        SuperweaponSystem.Update(World, _events);
        ParasiteSystem.Update(World, _events);
        GarrisonSystem.Update(World, _events);
        Probe?.Invoke("damage");

        // 4. Movement.
        MovementSystem.Update(World, _events);
        Probe?.Invoke("movement");
        World.Spatial.Sync(World);
        Probe?.Invoke("index");
        EntrySystem.Update(World, _events);
        ParadropSystem.Update(World, _events);
        BombSystem.Update(World, _events);
        AmmoSystem.Update(World, _events);
        // The crates (M11 a6): taken where a unit stands after moving, new ones on the map.
        CrateSystem.Update(World, _events);

        // 5. Ore grows and spreads (by its interval), the sources erupt (M3 step 16), then technologies, when the buildings changed.
        OreGrowthSystem.Update(World);
        OreSourceSystem.Update(World, _events);
        TechTree.Update(World, _events);
        Probe?.Invoke("ore and tech");

        // 5a. Victory and defeat, in a match (M5 step 10): what the defeated leave falls before the fallen leave the world.
        Matches.VictorySystem.Update(World, _events);
        Probe?.Invoke("victory");

        // 6. The cells of the buildings that fell open (M4 step 9), the players see anew (step 11); entities that died this tick leave the world.
        DeathSystem.Update(World, _events);
        VisibilitySystem.Update(World);
        Probe?.Invoke("death and vision");

        // The statistics of the tick (M5 step 11), while the fallen are still in the world: whose they were.
        World.Stats.Record(World, _events);
        Probe?.Invoke("statistics");
        World.RemoveDead();
        Probe?.Invoke("remove dead");

        // The bots think: they read the world after tick T and give commands for T + 1 (M5 step 1). By index: no enumerator to box.
        var bots = _bots.Values;
        for (var i = 0; i < bots.Count; i++)
        {
            // A defeated player's bot no longer thinks (M5 step 10).
            if (World.Player(bots[i].Bot.Player).DefeatedTick is not null)
            {
                continue;
            }

            bots[i].Turn.Tick = tick;
            bots[i].Bot.Think(bots[i].Turn);
        }

        Probe?.Invoke("bots");

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
