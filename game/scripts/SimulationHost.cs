using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Godot;
using Rts.Simulation;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Hashing;
using Rts.Simulation.Replays;
using Rts.Simulation.World;

namespace Rts.Client;

/// <summary>
/// Owns the simulation on the client and runs it with a fixed time step
/// (docs/implementation/M0_foundation.md, 4.11). Everything the player does goes through commands.
/// Two modes: a live session that records a replay, and playback of the last saved replay.
/// </summary>
public partial class SimulationHost : Node
{
    public static readonly PlayerId LocalPlayer = new(1);

    private const ulong MatchSeed = 42;
    private const int MaxTicksPerFrame = 5;
    private const string ReplayDirectory = "user://replays";

    private static readonly double[] SpeedMultipliers = [0.25, 0.5, 1, 2, 4];

    private readonly Dictionary<EntityId, WorldPos> _previousPositions = new();
    private double _accumulator;
    private int _speedIndex = 2;
    private uint _nextSequence;
    private ReplayRecorder? _recorder;
    private Replay? _playback;
    private int _nextPlaybackChecksum;
    private int _ticksThisSecond;
    private double _secondTimer;

    public GameSimulation Simulation { get; private set; } = null!;

    public bool IsPlayback => _playback is not null;

    public bool Paused { get; set; }

    /// <summary>Fraction of the next tick already elapsed, for interpolating positions.</summary>
    public float Alpha { get; private set; }

    public double SpeedMultiplier => SpeedMultipliers[_speedIndex];

    public int MeasuredTicksPerSecond { get; private set; }

    public string? LastReplayPath { get; private set; }

    /// <summary>Human-readable state of the replay check, shown on the debug panel.</summary>
    public string ReplayStatus { get; private set; } = "";

    public bool PlaybackFinished => _playback is not null && Simulation.CurrentTick >= _playback.TickCount;

    /// <summary>True once playback reached the end with every recorded checksum reproduced.</summary>
    public bool PlaybackVerified { get; private set; }

    public override void _Ready() => StartLiveSession();

    public void StartLiveSession()
    {
        Simulation = new GameSimulation(new SimulationSettings(MatchSeed));
        _recorder = new ReplayRecorder(Simulation);
        _playback = null;
        _nextSequence = 0;
        ResetClock();
        ReplayStatus = "";

        // Starting units are commands too, so they end up in the replay.
        SpawnGroup(LocalPlayer, new CellPos(20, 20), count: 6);
        SpawnGroup(new PlayerId(2), new CellPos(44, 44), count: 4);
        GD.Print("[host] live session started");
    }

    public void StartPlayback()
    {
        if (LastReplayPath is null)
        {
            ReplayStatus = "no replay saved yet (F5)";
            return;
        }

        var replay = ReplayFormat.Read(File.ReadAllBytes(ProjectSettings.GlobalizePath(LastReplayPath)));
        if (replay.SimVersion != SimulationVersion.Current)
        {
            ReplayStatus = $"replay is for rules v{replay.SimVersion}, current v{SimulationVersion.Current}";
            return;
        }

        Simulation = ReplayPlayer.CreateSimulation(replay);
        _recorder = null;
        _playback = replay;
        _nextPlaybackChecksum = 0;
        PlaybackVerified = false;
        ResetClock();
        ReplayStatus = $"playing {replay.TickCount} ticks, {replay.Checksums.Count} checksums to compare";
        GD.Print($"[host] playback of {LastReplayPath}");
    }

    public override void _Process(double delta)
    {
        MeasureTicksPerSecond(delta);
        if (Paused || PlaybackFinished)
        {
            Alpha = 1f;
            return;
        }

        var tickDuration = 1.0 / Simulation.Settings.TicksPerSecond;
        _accumulator += delta * SpeedMultiplier;
        var ticks = 0;
        while (_accumulator >= tickDuration && ticks < MaxTicksPerFrame && !PlaybackFinished)
        {
            StepSimulation();
            _accumulator -= tickDuration;
            ticks++;
        }

        if (_accumulator >= tickDuration)
        {
            // The frame took too long: drop the backlog instead of spiralling.
            _accumulator = 0;
        }

        Alpha = (float)(_accumulator / tickDuration);
    }

    /// <summary>One tick by hand (paused mode, smoke test).</summary>
    public void StepOnce()
    {
        if (!PlaybackFinished)
        {
            StepSimulation();
        }

        Alpha = 1f;
    }

    public void IssueMove(WorldPos target)
    {
        var units = Simulation.World.Entities
            .Where(e => e is TestUnit && e.IsAlive && e.Owner == LocalPlayer)
            .Select(e => e.Id)
            .Take(MoveUnitsCommand.MaxUnits)
            .ToImmutableArray();
        if (!units.IsEmpty)
        {
            Issue(new MoveUnitsCommand(LocalPlayer, Simulation.CurrentTick, _nextSequence++, units, target));
        }
    }

    /// <summary>Orders the given units to move. Units that are dead or not the player's are left out.</summary>
    public void IssueMove(IReadOnlyList<EntityId> units, WorldPos target)
    {
        var ordered = units
            .Where(id => Simulation.World.TryGet(id, out var entity) && entity is TestUnit && entity.IsAlive && entity.Owner == LocalPlayer)
            .Take(MoveUnitsCommand.MaxUnits)
            .ToImmutableArray();
        if (!ordered.IsEmpty)
        {
            Issue(new MoveUnitsCommand(LocalPlayer, Simulation.CurrentTick, _nextSequence++, ordered, target));
        }
    }

    public void IssueSpawn(WorldPos position) =>
        Issue(new SpawnTestUnitCommand(LocalPlayer, Simulation.CurrentTick, _nextSequence++, position));

    public void ChangeSpeed(int direction) =>
        _speedIndex = Math.Clamp(_speedIndex + direction, 0, SpeedMultipliers.Length - 1);

    /// <summary>Writes the live session to user://replays and remembers the path for playback.</summary>
    public string? SaveReplay()
    {
        if (_recorder is null)
        {
            return null;
        }

        var directory = ProjectSettings.GlobalizePath(ReplayDirectory);
        Directory.CreateDirectory(directory);
        var fileName = $"replay-{Simulation.CurrentTick}{ReplayFormat.FileExtension}";
        File.WriteAllBytes(Path.Combine(directory, fileName), ReplayFormat.Write(_recorder.Build()));
        LastReplayPath = $"{ReplayDirectory}/{fileName}";
        ReplayStatus = $"saved {LastReplayPath}";
        GD.Print($"[host] {ReplayStatus}");
        return LastReplayPath;
    }

    public bool TryGetPreviousPosition(EntityId id, out WorldPos position) =>
        _previousPositions.TryGetValue(id, out position);

    private void Issue(Command command)
    {
        if (!IsPlayback)
        {
            Simulation.Enqueue(command);
        }
    }

    private void SpawnGroup(PlayerId owner, CellPos center, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var cell = new CellPos(center.X + i % 3 * 2, center.Y + i / 3 * 2);
            Simulation.Enqueue(new SpawnTestUnitCommand(owner, 0, owner == LocalPlayer ? _nextSequence++ : (uint)i, cell.Center));
        }
    }

    private void StepSimulation()
    {
        _previousPositions.Clear();
        foreach (var entity in Simulation.World.Entities)
        {
            if (entity is TestUnit unit)
            {
                _previousPositions[unit.Id] = unit.Position;
            }
        }

        Simulation.Step();
        _recorder?.AfterStep();
        _ticksThisSecond++;

        foreach (var simEvent in Simulation.LastTickEvents)
        {
            GD.Print($"[sim] {simEvent}");
        }

        if (_playback is not null)
        {
            CheckPlaybackChecksums(_playback);
        }
    }

    private void CheckPlaybackChecksums(Replay replay)
    {
        while (_nextPlaybackChecksum < replay.Checksums.Count
            && replay.Checksums[_nextPlaybackChecksum].Tick == Simulation.CurrentTick)
        {
            var expected = replay.Checksums[_nextPlaybackChecksum++];
            var actual = new StateChecksum(Simulation.CurrentTick, StateHasher.Compute(Simulation.World));
            if (actual != expected)
            {
                ReplayStatus = $"DESYNC on tick {expected.Tick}: expected {expected}, got {actual}";
                GD.PrintErr($"[host] {ReplayStatus}");
                _playback = replay with { TickCount = Simulation.CurrentTick };
                return;
            }
        }

        if (PlaybackFinished)
        {
            PlaybackVerified = _nextPlaybackChecksum == replay.Checksums.Count;
            ReplayStatus = PlaybackVerified
                ? $"finished: all {replay.Checksums.Count} checksums match"
                : "finished: some checksums were not reached";
            GD.Print($"[host] {ReplayStatus}");
        }
    }

    private void ResetClock()
    {
        _accumulator = 0;
        _previousPositions.Clear();
        Alpha = 1f;
    }

    private void MeasureTicksPerSecond(double delta)
    {
        _secondTimer += delta;
        if (_secondTimer >= 1.0)
        {
            MeasuredTicksPerSecond = _ticksThisSecond;
            _ticksThisSecond = 0;
            _secondTimer -= 1.0;
        }
    }
}
