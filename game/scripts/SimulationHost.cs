using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Godot;
using Rts.Client.App;
using Rts.Client.Controls;
using Rts.Client.Debug;
using Rts.Client.Units;
using Rts.Data;
using Rts.Data.Maps;
using Rts.Simulation;
using Rts.Simulation.Commands;
using Rts.Simulation.Events;
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

    /// <summary>The load map of 300 units (docs/implementation/M2_unit_control.md, 4.15), played with <c>--perf</c>.</summary>
    public const string LoadMapPath = "res://maps/test/load128.txt";

    /// <summary>
    /// The map of a live session: the <c>map</c> argument of the scene router (the map editor's «Play», M7 step 12); else
    /// <c>--map NAME</c> plays res://data/maps/NAME.rtsmap when there is one (M7: heights in the rules), otherwise
    /// res://maps/test/NAME.txt (for example <c>squad</c>, the 60-unit squad of the M2 definition of done); <c>--perf</c> plays
    /// the load map, otherwise the sandbox.
    /// </summary>
    /// <summary>The scene router argument with the path of the map to play.</summary>
    public const string MapArgument = "map";

    public static string MapPath
    {
        get
        {
            if (SceneRouter.Instance?.Args.TryGetValue(MapArgument, out var routed) == true && routed is string path)
            {
                return path;
            }

            var args = OS.GetCmdlineUserArgs();
            var index = Array.IndexOf(args, "--map");
            if (index >= 0 && index + 1 < args.Length)
            {
                var rtsmap = $"res://data/maps/{args[index + 1]}.rtsmap";
                return Godot.FileAccess.FileExists(rtsmap) ? rtsmap : $"res://maps/test/{args[index + 1]}.txt";
            }

            return args.Contains("--perf") ? LoadMapPath : SandboxMapPath;
        }
    }

    private readonly Dictionary<EntityId, UnitPose> _previousPoses = new();
    private double _accumulator;
    private int _speedIndex = 2;
    private uint _nextSequence;
    private ReplayRecorder? _recorder;
    private Replay? _playback;
    private int _nextPlaybackChecksum;

    // "; <warning>" when the replay in playback was recorded with other data, else empty; it ends every playback status.
    private string _dataWarning = "";

    private int _ticksThisSecond;
    private double _secondTimer;

    public GameSimulation Simulation { get; private set; } = null!;

    public bool IsPlayback => _playback is not null;

    public bool Paused { get; set; }

    /// <summary>The path queue of the last ticks, for the debug graph (F8).</summary>
    public TickHistory QueueHistory { get; } = new();

    /// <summary>Fraction of the next tick already elapsed, for interpolating positions.</summary>
    public float Alpha { get; private set; }

    public double SpeedMultiplier => SpeedMultipliers[_speedIndex];

    public int MeasuredTicksPerSecond { get; private set; }

    public string? LastReplayPath { get; private set; }

    /// <summary>Human-readable state of the replay check, shown on the debug panel.</summary>
    public string ReplayStatus { get; private set; } = "";

    /// <summary>The data files the match is played by (<c>game/data</c>, docs/implementation/M3_base_economy.md, 4.5).</summary>
    public GameData Data => DataFiles.Current;

    /// <summary>Hash of the data files, kept in the replay (4.4): a replay of other data plays with a warning.</summary>
    public ulong DataHash => Data.Hash;

    /// <summary>Type that Shift + click spawns; the debug key cycles through all types.</summary>
    public UnitType SpawnType { get; private set; } = UnitType.HeavyTank;

    /// <summary>The building the debug build (V) places, an index of the rules' buildings; V again takes the next one.</summary>
    public int BuildIndex { get; private set; } = -1;

    /// <summary>The id of <see cref="BuildIndex"/>, such as <c>reactor</c>.</summary>
    public string BuildId => Data.Database.Buildings[BuildIndex].Id;

    /// <summary>
    /// The last thing that happened to the local player's base, shown on the debug panel (M3 step 6): a building placed, refused or
    /// grown, an MCV deployed or not, power lost or back.
    /// </summary>
    public string Notice { get; private set; } = "";

    public bool PlaybackFinished => _playback is not null && Simulation.CurrentTick >= _playback.TickCount;

    /// <summary>True once playback reached the end with every recorded checksum reproduced.</summary>
    public bool PlaybackVerified { get; private set; }

    public override void _Ready()
    {
        // The game cannot be played without its rules: the errors are in the console already.
        if (!DataFiles.TryLoad())
        {
            GetTree().Quit(1);
            return;
        }

        StartLiveSession();
    }

    public void StartLiveSession()
    {
        QueueHistory.Clear();
        var map = LoadMap(MapPath);
        Simulation = new GameSimulation(new SimulationSettings(MatchSeed, map.Grid), Data.Rules);
        _recorder = new ReplayRecorder(Simulation, dataHash: DataHash);
        _playback = null;
        _nextSequence = 0;
        Notice = "";
        ResetClock();
        ReplayStatus = "";

        // Starting units are commands too, so they end up in the replay.
        SpawnMapUnits(map.Units);
        GD.Print("[host] live session started");
    }

    public void StartPlayback()
    {
        QueueHistory.Clear();
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

        Simulation = ReplayPlayer.CreateSimulation(replay, Data.Rules);
        _recorder = null;
        _playback = replay;
        _nextPlaybackChecksum = 0;
        PlaybackVerified = false;
        ResetClock();
        _dataWarning = ReplayPlayer.DataWarning(replay, DataHash) is { } warning ? $"; {warning}" : "";
        ReplayStatus = $"playing {replay.TickCount} ticks, {replay.Checksums.Count} checksums to compare{_dataWarning}";
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
            .Take(MoveCommand.MaxUnits)
            .ToImmutableArray();
        if (!units.IsEmpty)
        {
            Issue(new MoveCommand(LocalPlayer, Simulation.CurrentTick, _nextSequence++, units, target));
        }
    }

    /// <summary>
    /// Orders the given units to move; <paramref name="queue"/> adds the point to the end of their routes. Units that are dead
    /// or not the player's are left out, here and in the other orders.
    /// </summary>
    public void IssueMove(IReadOnlyList<EntityId> units, WorldPos target, bool queue = false) =>
        IssueTo(units, ordered => new MoveCommand(LocalPlayer, Simulation.CurrentTick, _nextSequence++, ordered, target, queue));

    public void IssueAttackMove(IReadOnlyList<EntityId> units, WorldPos target) =>
        IssueTo(units, ordered => new AttackMoveCommand(LocalPlayer, Simulation.CurrentTick, _nextSequence++, ordered, target));

    /// <summary>Sends a route of 1…32 points; <paramref name="loop"/> makes it a patrol.</summary>
    public void IssueWaypoints(IReadOnlyList<EntityId> units, ImmutableArray<WorldPos> points, bool loop)
    {
        if (!points.IsEmpty)
        {
            IssueTo(units, ordered => new WaypointsCommand(LocalPlayer, Simulation.CurrentTick, _nextSequence++, ordered, points, loop));
        }
    }

    public void IssueStop(IReadOnlyList<EntityId> units) =>
        IssueTo(units, ordered => new StopCommand(LocalPlayer, Simulation.CurrentTick, _nextSequence++, ordered));

    public void IssueScatter(IReadOnlyList<EntityId> units) =>
        IssueTo(units, ordered => new ScatterCommand(LocalPlayer, Simulation.CurrentTick, _nextSequence++, ordered));

    /// <summary>Debug (M7 steps 5–7): takes <paramref name="amount"/> hit points off the map object, bridge segment or building on the cell.</summary>
    public void IssueDamage(CellPos cell, int amount) =>
        Issue(new DamageMapObjectCommand(LocalPlayer, Simulation.CurrentTick, _nextSequence++, cell, amount));

    /// <summary>The selected units deploy; only the player's MCVs do (M3 step 7).</summary>
    public void IssueDeploy(IReadOnlyList<EntityId> units)
    {
        foreach (var unit in units)
        {
            if (Simulation.World.TryGet(unit, out var entity) && entity is Unit { IsAlive: true } mcv && mcv.Owner == LocalPlayer
                && mcv.Archetype.Id == Simulation.World.Player(LocalPlayer).Faction.Mcv)
            {
                Issue(new DeployCommand(LocalPlayer, Simulation.CurrentTick, _nextSequence++, unit));
            }
        }
    }

    /// <summary>Debug (M3 step 6): places the <see cref="BuildIndex"/> building with the centre of its footprint at <paramref name="position"/>.</summary>
    public void IssuePlaceBuilding(WorldPos position)
    {
        var footprint = Data.Database.Buildings[BuildIndex].Footprint;
        var centre = position.ToCell();
        var cell = new CellPos(centre.X - (footprint.Width - 1) / 2, centre.Y - (footprint.Height - 1) / 2);
        Issue(new PlaceBuildingCommand(LocalPlayer, Simulation.CurrentTick, _nextSequence++, BuildIndex, cell));
    }

    /// <summary>
    /// The debug build (V, M3 step 8): takes the next building type (<see cref="NextBuildType"/>) and orders it when its queue is empty.
    /// The previous choice, if still unfinished, and an unfinished building of another type in the new queue are cancelled first, their
    /// credits come back; a finished one stays to be placed.
    /// </summary>
    public void ChooseBuild()
    {
        var rules = Data.Database;
        var player = Simulation.World.Player(LocalPlayer);
        if (BuildIndex >= 0 && player.Queue(rules.Buildings[BuildIndex].Category) is [{ Ready: false } previous, ..] && previous.DefIndex == BuildIndex)
        {
            Issue(new CancelProductionCommand(LocalPlayer, Simulation.CurrentTick, _nextSequence++, ProductionKind.Building, BuildIndex));
        }

        var previousIndex = BuildIndex;
        NextBuildType();
        var queue = player.Queue(rules.Buildings[BuildIndex].Category);
        if (queue.Count > 0 && queue[0].DefIndex == previousIndex && !queue[0].Ready)
        {
            // Cancelled just above, on this tick.
            Issue(new ProduceCommand(LocalPlayer, Simulation.CurrentTick, _nextSequence++, ProductionKind.Building, BuildIndex, 1));
            return;
        }

        if (queue.Count > 0 && queue[0].DefIndex != BuildIndex && !queue[0].Ready)
        {
            Issue(new CancelProductionCommand(LocalPlayer, Simulation.CurrentTick, _nextSequence++, ProductionKind.Building, queue[0].DefIndex));
        }
        else if (queue.Count > 0)
        {
            return;
        }

        Issue(new ProduceCommand(LocalPlayer, Simulation.CurrentTick, _nextSequence++, ProductionKind.Building, BuildIndex, 1));
    }

    /// <summary>Debug (U, M3 step 8): orders <paramref name="count"/> units of the <see cref="SpawnType"/>, from the barracks or the factory.</summary>
    public void IssueProduceUnits(int count) =>
        Issue(new ProduceCommand(
            LocalPlayer, Simulation.CurrentTick, _nextSequence++, ProductionKind.Unit, Data.Database.IndexOfUnit(UnitTypes.IdOf(SpawnType)), count));

    /// <summary>
    /// The debug build (V): the first press takes the reactor, every next one the next building of the player's faction, wrapping
    /// around.
    /// </summary>
    public void NextBuildType()
    {
        var buildings = Data.Database.Buildings;
        var faction = Simulation.World.Player(LocalPlayer).Faction.Id;
        if (BuildIndex < 0)
        {
            BuildIndex = Data.Database.IndexOfBuilding("reactor");
        }
        else
        {
            do
            {
                BuildIndex = (BuildIndex + 1) % buildings.Length;
            }
            while (buildings[BuildIndex].Faction != faction);
        }

        GD.Print($"[host] build type {BuildId}");
    }

    public void IssueSpawn(WorldPos position) =>
        Issue(new SpawnTestUnitCommand(LocalPlayer, Simulation.CurrentTick, _nextSequence++, SpawnType, position));

    /// <summary>Switches the Shift + click type to the next one, wrapping around.</summary>
    public void NextSpawnType()
    {
        var all = UnitTypes.All;
        SpawnType = all[((int)SpawnType) % all.Count];
        GD.Print($"[host] spawn type {UnitTypes.IdOf(SpawnType)}");
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

    /// <summary>Issues the command made for the living own units among <paramref name="units"/>, when there are any.</summary>
    private void IssueTo(IReadOnlyList<EntityId> units, Func<ImmutableArray<EntityId>, Command> make)
    {
        var ordered = OrderTargets.Pick(units, IsOwnLiving).ToImmutableArray();
        if (!ordered.IsEmpty)
        {
            Issue(make(ordered));
        }
    }

    private bool IsOwnLiving(EntityId id) =>
        Simulation.World.TryGet(id, out var entity) && entity is Unit && entity.IsAlive && entity.Owner == LocalPlayer;

    private void Issue(Command command)
    {
        if (!IsPlayback)
        {
            Simulation.Enqueue(command);
        }
    }

    private TestMap LoadMap(string path)
    {
        var text = Godot.FileAccess.GetFileAsString(path);
        if (string.IsNullOrEmpty(text))
        {
            throw new InvalidOperationException($"Cannot read {path}: {Godot.FileAccess.GetOpenError()}.");
        }

        if (!path.EndsWith(".rtsmap", StringComparison.Ordinal))
        {
            return TestMapParser.Parse(text, Data.Rules);
        }

        // A map of the M7 format: trees are the obstacles for now (map objects come in M7 step 5), units are debug squads
        // at the starts until skirmish starts exist.
        var definition = RtsMapReader.Read(text);
        var grid = definition.ToMapGrid();
        return new TestMap(grid, StartSquads.Place(definition, grid, Data.Rules));
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
        QueueHistory.Add(Simulation.World.PathRequests.ExpandedLastTick, Simulation.World.PathRequests.Requests.Count);

        foreach (var simEvent in Simulation.LastTickEvents)
        {
            GD.Print($"[sim] {simEvent}");
            if (NoticeOf(simEvent) is { } notice)
            {
                Notice = notice;
            }
        }

        if (_playback is not null)
        {
            CheckPlaybackChecksums(_playback);
        }
    }

    /// <summary>What an event of the local player's base reads like on the debug panel, or null.</summary>
    private string? NoticeOf(SimEvent simEvent)
    {
        var buildings = Data.Database.Buildings;
        string NameOf(EntityId id) =>
            Simulation.World.TryGet(id, out var entity) && entity is Building { Def: { } def } ? def.Id : "building";
        return simEvent switch
        {
            BuildingPlaced placed when placed.Player == LocalPlayer => $"placed {buildings[placed.DefIndex].Id}, growing",
            BuildUpComplete complete when Simulation.World.TryGet(complete.Building, out var entity) && entity.Owner == LocalPlayer =>
                $"{NameOf(complete.Building)} ready",
            CannotPlace refused when refused.Player == LocalPlayer =>
                $"cannot place {buildings[refused.DefIndex].Id}: {ProblemText(refused.Problem)}",
            CannotDeploy refused when refused.Player == LocalPlayer => $"cannot deploy: {ProblemText(refused.Problem)}",
            ProductionReady ready when ready.Player == LocalPlayer => $"{buildings[ready.DefIndex].Id} ready to place",
            UnitProduced produced when produced.Player == LocalPlayer && Simulation.World.TryGet(produced.Unit, out var unit) =>
                $"{((Unit)unit).Archetype.Id} out",
            InsufficientFunds funds when funds.Player == LocalPlayer => "insufficient funds",
            ProductionCancelled cancelled when cancelled.Player == LocalPlayer =>
                $"cancelled {ProductionRules.IdOf(Data.Database, cancelled.Kind, cancelled.DefIndex)}, {cancelled.Refund} back",
            LowPower low when low.Player == LocalPlayer => "LOW POWER",
            PowerRestored restored when restored.Player == LocalPlayer => "power restored",
            _ => null,
        };
    }

    private static string ProblemText(PlacementProblem problem) => problem switch
    {
        PlacementProblem.OutsideMap => "off the map",
        PlacementProblem.NotBuildable => "the ground takes no building",
        PlacementProblem.NotFlat => "the ground is not flat",
        PlacementProblem.Taken => "the place is taken",
        PlacementProblem.Units => "units in the way",
        PlacementProblem.TooFar => "too far from the base",
        PlacementProblem.NotAvailable => "its prerequisites are missing",
        PlacementProblem.NotReady => "it is not produced yet",
        _ => problem.ToString(),
    };

    private void CheckPlaybackChecksums(Replay replay)
    {
        while (_nextPlaybackChecksum < replay.Checksums.Count
            && replay.Checksums[_nextPlaybackChecksum].Tick == Simulation.CurrentTick)
        {
            var expected = replay.Checksums[_nextPlaybackChecksum++];
            var actual = new StateChecksum(Simulation.CurrentTick, StateHasher.Compute(Simulation.World));
            if (actual != expected)
            {
                ReplayStatus = $"DESYNC on tick {expected.Tick}: expected {expected}, got {actual}{_dataWarning}";
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
            ReplayStatus += _dataWarning;
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
