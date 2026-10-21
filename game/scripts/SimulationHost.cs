using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Godot;
using Rts.Client.Units;
using Rts.Simulation;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Hashing;
using Rts.Simulation.Map;
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

    /// <summary>The sandbox map with every land type and its starting units (docs/implementation/M2_unit_control.md, 4.3).</summary>
    public const string SandboxMapPath = "res://maps/test/sandbox.txt";

    private readonly Dictionary<EntityId, UnitPose> _previousPoses = new();
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

    /// <summary>Type that Shift + click spawns; the debug key cycles through all types.</summary>
    public UnitType SpawnType { get; private set; } = UnitType.HeavyTank;

    public bool PlaybackFinished => _playback is not null && Simulation.CurrentTick >= _playback.TickCount;

    /// <summary>True once playback reached the end with every recorded checksum reproduced.</summary>
    public bool PlaybackVerified { get; private set; }

    public override void _Ready() => StartLiveSession();

    public void StartLiveSession()
    {
        var map = LoadSandboxMap();
        Simulation = new GameSimulation(new SimulationSettings(MatchSeed, map.Grid));
        _recorder = new ReplayRecorder(Simulation);
        _playback = null;
        _nextSequence = 0;
        ResetClock();
        ReplayStatus = "";

        // Starting units are commands too, so they end up in the replay.
        SpawnMapUnits(map.Units);
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
            .Where(e => e is Unit && e.IsAlive && e.Owner == LocalPlayer)
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
            .Where(id => Simulation.World.TryGet(id, out var entity) && entity is Unit && entity.IsAlive && entity.Owner == LocalPlayer)
            .Take(MoveUnitsCommand.MaxUnits)
            .ToImmutableArray();
        if (!ordered.IsEmpty)
        {
            Issue(new MoveUnitsCommand(LocalPlayer, Simulation.CurrentTick, _nextSequence++, ordered, target));
        }
    }

    public void IssueSpawn(WorldPos position) =>
        Issue(new SpawnTestUnitCommand(LocalPlayer, Simulation.CurrentTick, _nextSequence++, SpawnType, position));

    /// <summary>Switches the Shift + click type to the next one, wrapping around.</summary>
    public void NextSpawnType()
    {
        var all = UnitArchetypes.All;
        SpawnType = all[((int)SpawnType) % all.Count].Type;
        GD.Print($"[host] spawn type {UnitArchetypes.Get(SpawnType).Id}");
    }

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

    /// <summary>Where the unit was and how it was turned on the previous tick, for interpolation.</summary>
    public bool TryGetPreviousPose(EntityId id, out UnitPose pose) => _previousPoses.TryGetValue(id, out pose);

    private void Issue(Command command)
    {
        if (!IsPlayback)
        {
            Simulation.Enqueue(command);
        }
    }

    private static TestMap LoadSandboxMap()
    {
        var text = Godot.FileAccess.GetFileAsString(SandboxMapPath);
        if (string.IsNullOrEmpty(text))
        {
            throw new InvalidOperationException($"Cannot read {SandboxMapPath}: {Godot.FileAccess.GetOpenError()}.");
        }

        return TestMapParser.Parse(text);
    }

    private void SpawnMapUnits(IReadOnlyList<TestMapUnit> units)
    {
        var otherSequences = new Dictionary<PlayerId, uint>();
        foreach (var unit in units)
        {
            uint sequence;
            if (unit.Owner == LocalPlayer)
            {
                sequence = _nextSequence++;
            }
            else
            {
                otherSequences.TryGetValue(unit.Owner, out sequence);
                otherSequences[unit.Owner] = sequence + 1;
            }

            Simulation.Enqueue(new SpawnTestUnitCommand(unit.Owner, 0, sequence, unit.Type, unit.Cell.Center));
        }
    }

    private void StepSimulation()
    {
        _previousPoses.Clear();
        foreach (var entity in Simulation.World.Entities)
        {
            if (entity is Unit unit)
            {
                _previousPoses[unit.Id] = new UnitPose(unit.Position, unit.Facing, unit.TurretFacing);
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
        _previousPoses.Clear();
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
