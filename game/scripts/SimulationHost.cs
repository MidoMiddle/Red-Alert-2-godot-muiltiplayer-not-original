using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Godot;
using Rts.AI;
using Rts.Client.App;
using Rts.Client.Controls;
using Rts.Client.Debug;
using Rts.Client.Maps;
using Rts.Client.Matches;
using Rts.Client.Units;
using Rts.Data;
using Rts.Data.Maps;
using Rts.Simulation;
using Rts.Simulation.Benchmarks;
using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Hashing;
using Rts.Simulation.Map;
using Rts.Simulation.Matches;
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
    /// <summary>
    /// The player at the keyboard: player 1, the person of the match setup (M5 step 2), or the one F11 switched to in a debug build
    /// (M4 step 13).
    /// </summary>
    public static PlayerId LocalPlayer { get; private set; } = new(1);

    /// <summary>Debug (M4 step 13): the keyboard goes to the next player with units or buildings.</summary>
    public void SwitchPlayer()
    {
        LocalPlayer = Controls.PlayerSwitch.Next(Simulation.World, LocalPlayer);
        GD.Print($"[debug] playing as {LocalPlayer}");
    }

    private const ulong MatchSeed = 42;
    private const int MaxTicksPerFrame = 5;
    private const string ReplayDirectory = "user://replays";

    private static readonly double[] SpeedMultipliers = [0.25, 0.5, 1, 2, 4];

    /// <summary>The sandbox map with every land type and its starting units (docs/implementation/M2_unit_control.md, 4.3).</summary>
    public const string SandboxMapPath = "res://maps/test/sandbox.txt";

    /// <summary>
    /// The load map of 300 units (docs/implementation/M2_unit_control.md, 4.15), played with <c>--perf</c>; its land carries the
    /// general battle of <c>--perf-battle</c> (M4 step 15).
    /// </summary>
    public const string LoadMapPath = "res://maps/test/load128.txt";

    /// <summary>
    /// The map of a live session: the <c>map</c> argument of the scene router (the map editor's «Play», M7 step 12); else
    /// <c>--map NAME</c> plays res://data/maps/NAME.rtsmap when there is one (M7: heights in the rules), otherwise
    /// res://maps/test/NAME.txt (for example <c>squad</c>, the 60-unit squad of the M2 definition of done); <c>--perf</c> plays
    /// the load map, otherwise the sandbox.
    /// </summary>
    /// <summary>The scene router argument with the path of the map to play.</summary>
    public const string MapArgument = "map";

    /// <summary>
    /// The scene router argument with the <see cref="MatchSetup"/> to play on the map (M5 step 2, the quick skirmish). Without one, a map with
    /// two starts or more is played one against one with a «Medium» bot (<see cref="MatchSetups.OneOnOne"/>).
    /// </summary>
    public const string MatchArgument = "match";

    /// <summary>
    /// <c>--perf-battle</c>: the live session is the general battle of M4 (<see cref="BattleScenario"/>, both armies without their
    /// bases) on the land of the load map, instead of the map's own units.
    /// </summary>
    public static bool IsBattle => OS.GetCmdlineUserArgs().Contains("--perf-battle");

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

            return args.Contains("--perf") || IsBattle ? LoadMapPath : SandboxMapPath;
        }
    }

    private readonly Dictionary<EntityId, UnitPose> _previousPoses = new();

    // The match of this scene, made once: R plays it again from the start with the same seed.
    private MatchSetup? _match;
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

    // A network match (M9 step 9): its game, and once everyone has loaded its lockstep and what goes with it.
    private Net.NetGame? _net;
    private Rts.Net.Lockstep.LockstepEngine? _lockstep;
    private Rts.Net.Desync.ChecksumExchange? _checksums;
    private Rts.Net.Lockstep.LatencyMonitor? _latency;
    private Rts.Net.Lockstep.InputDelayAuthority? _delay;
    private Rts.Net.Session.MatchReferee? _referee;
    private readonly Rts.Net.Session.MatchStatus _netMatch = new();
    private long _netClockMs;
    private bool _desyncSaved;
    private Label? _netStatus;
    private HBoxContainer? _kickButtons;
    private readonly HashSet<PlayerId> _votedOut = new();

    // Why the match is over for this computer (M9 step 10): the host left, or the others put this player out; empty while it goes.
    private string _netEnded = "";

    // A player who left lately, shown for a few seconds: the text and until when on the network clock.
    private string _leftNotice = "";
    private long _leftNoticeUntil;

    public GameSimulation Simulation { get; private set; } = null!;

    /// <summary>The look of the map being played (climate, time of day, texture layer): the simulation map has none of it.</summary>
    public Climates.MapLook Look { get; private set; } = null!;

    /// <summary>
    /// Every event of a tick, right after it, live and in playback (M3 step 13): the views of buildings, the effects and the
    /// notifications listen here instead of reading the events themselves.
    /// </summary>
    public event Action<SimEvent>? EventRaised;

    /// <summary>A live session (re)started: R, or Play Again of the results (M5 step 12) — what showed the end of the last one goes.</summary>
    public event Action? SessionStarted;

    public bool IsPlayback => _playback is not null;

    /// <summary>A match over the network (M9): the tick goes when everyone's commands are here; no pause, speed or replays by hand.</summary>
    public bool IsNetwork => _net is not null;

    /// <summary>The bots of the live match, for the debug overlay of step 13; none in the sandbox and in a replay, which runs no bots.</summary>
    public IReadOnlyList<AiPlayer> Bots { get; private set; } = [];

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

    /// <summary>
    /// The building the placement mode places, an index of the rules' buildings: the one the debug build (V) took, or the ready one
    /// whose square of the sidebar was clicked.
    /// </summary>
    public int BuildIndex { get; private set; } = -1;

    /// <summary>
    /// Whether <see cref="BuildIndex"/> came from the debug build (V): it stays armed after a placement and orders the next wall.
    /// From the sidebar the placement mode ends with the ready building, as in RA2.
    /// </summary>
    public bool DebugBuild { get; private set; }

    /// <summary>
    /// Whether the minimap follows the radar rule of a match (4.17): only the <c>--radar-rule</c> launch option turns it on for now;
    /// the sandbox keeps the minimap open (decision of the owner 2026-10-22). The skirmish of M6 turns it on.
    /// </summary>
    public bool RadarRule { get; } = OS.GetCmdlineUserArgs().Contains("--radar-rule");

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

    // « hard:aggressor» for a bot in the log of the start — a random personality as drawn (M8 step 13); nothing for a person.
    private string BotProfile(MatchPlayer player) =>
        player.Kind == PlayerKind.Ai && _match is not null
            ? $" {MatchBots.LevelId(player.Difficulty)}:{MatchBots.PersonalityOf(_match, player, Data.Rules.Database)}"
            : "";

    public void StartLiveSession()
    {
        // A network match is started once, by everyone together; R and Play Again do not apply.
        if (_net is not null && Simulation is not null)
        {
            return;
        }

        _net ??= SceneRouter.Instance?.Args.TryGetValue(Net.NetGame.MatchArgument, out var routedNet) == true ? routedNet as Net.NetGame : null;
        if (_net?.Start is { } netStart)
        {
            _match = netStart.Setup;
        }

        QueueHistory.Clear();
        var (map, definition) = LoadMap(MapPath);
        Look = definition is not null ? Climates.MapLook.FromDefinition(definition) : Climates.MapLook.FromLand(map.Grid);
        _match ??= RoutedMatch() ?? (!IsBattle && definition is not null && MatchSetups.IsPlayable(definition)
            ? MatchSetups.OneOnOne(definition, Data.Rules.DefaultFaction.Id, MatchSetups.NewSeed())
            : null);
        if (_match is not null)
        {
            // A match (M5 step 2): the starting units come with the world, the bots think inside the simulation.
            Simulation = new GameSimulation(SimulationSettings.ForMatch(_match, map.Grid), Data.Rules);
            // --no-bots: the opponents stand idle (a measure of the frame on a real map without a bot playing).
            Bots = OS.GetCmdlineUserArgs().Contains("--no-bots") ? [] : MatchBots.AttachAll(Simulation);
            Debug.AiJournalFiles.Attach(Bots, _match);
            LocalPlayer = _net?.LocalPlayer ?? _match.Players.First(player => player.Kind == PlayerKind.Human).Id;
            CallDeferred(MethodName.LookAtStart);
            GD.Print($"[host] match on {_match.MapId}, seed {_match.Seed}: " +
                     string.Join(", ", _match.Players.Select(player => $"{player.Id} {player.Kind}{BotProfile(player)} at {player.Start.X},{player.Start.Y}")));
        }
        else
        {
            Bots = [];
            Simulation = IsBattle
                ? BattleScenario.Create(map.Grid, Data.Rules)
                : new GameSimulation(new SimulationSettings(MatchSeed, map.Grid), Data.Rules);
        }

        PlayerColors.Match = _match;
        _recorder = new ReplayRecorder(Simulation, dataHash: DataHash);
        _playback = null;
        _nextSequence = 0;
        Notice = "";
        ResetClock();
        ReplayStatus = "";

        // Starting units are commands too, so they end up in the replay; those of a match come with its setup.
        if (!IsBattle && _match is null)
        {
            SpawnMapUnits(map.Units);
        }

        GD.Print("[host] live session started");
        SessionStarted?.Invoke();
        if (_net is not null)
        {
            // The lockstep goes on while the pause menu is open: the others do not wait for this computer's menu.
            ProcessMode = ProcessModeEnum.Always;
        }
    }

    public void StartPlayback()
    {
        if (_net is not null)
        {
            return;
        }

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

        // Without bots: their commands are in the replay (M5, 4.4).
        Simulation = ReplayPlayer.CreateSimulation(replay, Data.Rules);
        if (Look.Width != Simulation.World.Map.Width || Look.Height != Simulation.World.Map.Height)
        {
            // A replay of another map: its file is not at hand, so it gets the look of its land.
            Look = Climates.MapLook.FromLand(Simulation.World.Map);
        }

        Bots = [];
        PlayerColors.Match = replay.Settings.Match;
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
        if (_net is not null)
        {
            ProcessNetwork(delta);
            return;
        }

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
    /// <summary>
    /// A move order. Onto ore (M3 step 10), the selected harvesters go to work that field instead, and the others move.
    /// </summary>
    public void IssueMove(IReadOnlyList<EntityId> units, WorldPos target, bool queue = false)
    {
        var world = Simulation.World;
        var cell = target.ToCell();
        var harvesters = world.Map.OreAt(cell).IsEmpty
            ? []
            : units.Where(id => world.TryGet(id, out var entity) && entity is Unit { Harvester: not null }).ToList();
        if (harvesters.Count > 0)
        {
            IssueTo(harvesters, ordered => new HarvestCommand(LocalPlayer, Simulation.CurrentTick, _nextSequence++, ordered, cell));
            units = units.Except(harvesters).ToList();
        }

        if (units.Count > 0)
        {
            IssueTo(units, ordered => new MoveCommand(LocalPlayer, Simulation.CurrentTick, _nextSequence++, ordered, target, queue));
        }
    }

    public void IssueAttackMove(IReadOnlyList<EntityId> units, WorldPos target) =>
        IssueTo(units, ordered => new AttackMoveCommand(LocalPlayer, Simulation.CurrentTick, _nextSequence++, ordered, target));

    /// <summary>An attack on an enemy unit or building (M4 step 7).</summary>
    public void IssueAttack(IReadOnlyList<EntityId> units, EntityId target) =>
        IssueTo(units, ordered => new AttackCommand(LocalPlayer, Simulation.CurrentTick, _nextSequence++, ordered, target));

    /// <summary>A forced attack on <paramref name="target"/>, or on <paramref name="point"/> on the ground without one (M4 step 7).</summary>
    public void IssueForceAttack(IReadOnlyList<EntityId> units, EntityId? target, WorldPos point) =>
        IssueTo(units, ordered => new ForceAttackCommand(LocalPlayer, Simulation.CurrentTick, _nextSequence++, ordered, target, point));

    /// <summary>
    /// The units that can do something in the building go into it (stage 2e, 3b): soldiers into the player's bunker, engineers to take or
    /// mend, infiltrators into the enemy's. The others are left to the caller; gives them.
    /// </summary>
    public List<EntityId> IssueEnter(IReadOnlyList<EntityId> units, EntityId building)
    {
        var world = Simulation.World;
        var target = world.TryGet(building, out var entity) && entity is Building or Unit ? entity : null;
        var going = units.Where(id => target is not null && world.TryGet(id, out var unit) && unit is Unit soldier
            && Rts.Simulation.Systems.EntrySystem.ActionOf(world, soldier, target) != Rts.Simulation.Systems.EntryAction.None).ToList();
        if (going.Count > 0)
        {
            IssueTo(going, ordered => new EnterCommand(LocalPlayer, Simulation.CurrentTick, _nextSequence++, ordered, building));
        }

        return units.Except(going).ToList();
    }

    /// <summary>The player's superweapon fires at <paramref name="target"/>; the Phase Gate takes what is there to <paramref name="second"/>.</summary>
    public void IssueSuperweapon(EntityId building, CellPos target, CellPos second) =>
        Issue(new SuperweaponCommand(LocalPlayer, Simulation.CurrentTick, _nextSequence++, building, target, second));

    /// <summary>Where the units of the player's building that produces go once they are out (its rally point).</summary>
    public void IssueRallyPoint(EntityId building, WorldPos point) =>
        Issue(new SetRallyPointCommand(LocalPlayer, Simulation.CurrentTick, _nextSequence++, building, point));

    /// <summary>The soldiers inside the player's building come out (stage 2e).</summary>
    public void IssueEvacuate(EntityId building) =>
        Issue(new EvacuateCommand(LocalPlayer, Simulation.CurrentTick, _nextSequence++, building));

    /// <summary>The stance of the units (M4 step 7).</summary>
    public void IssueStance(IReadOnlyList<EntityId> units, Stance stance) =>
        IssueTo(units, ordered => new SetStanceCommand(LocalPlayer, Simulation.CurrentTick, _nextSequence++, ordered, stance));

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
            // An MCV, a unit with a deploy weapon (stage 3b: the toxin trooper), one that digs in (the Rifleman), a transport with
            // passengers; or the construction yard, which packs up (RA2).
            var faction = Simulation.World.Player(LocalPlayer).Faction;
            if (Simulation.World.TryGet(unit, out var entity) && entity.IsAlive && entity.Owner == LocalPlayer
                && entity switch
                {
                    Unit mcv => mcv.Archetype.Id == faction.Mcv || mcv.Archetype.DeployWeapon >= 0 || mcv.Archetype.DeployedWeapon >= 0 || mcv.Passengers.Count > 0,
                    Building yard => yard.Def?.Id == faction.ConstructionYard,
                    _ => false,
                })
            {
                Issue(new DeployCommand(LocalPlayer, Simulation.CurrentTick, _nextSequence++, unit));
            }
        }
    }

    /// <summary>
    /// Debug (M3 steps 6, 11): places the <see cref="BuildIndex"/> building with the centre of its footprint at <paramref name="position"/>.
    /// A wall with <paramref name="line"/> (Shift) is a line of walls from the last one placed this way.
    /// </summary>
    public void IssuePlaceBuilding(WorldPos position, bool line = false)
    {
        var footprint = Data.Database.Buildings[BuildIndex].Footprint;
        var cell = PlacementPreview.CornerOf(footprint, position);
        var wall = Data.Database.Buildings[BuildIndex].Wall;
        if (wall && line && _lastWall is { } from)
        {
            Issue(new PlaceWallsCommand(LocalPlayer, Simulation.CurrentTick, _nextSequence++, BuildIndex, PlacementPreview.WallLine(from, cell)));
        }
        else
        {
            Issue(new PlaceBuildingCommand(LocalPlayer, Simulation.CurrentTick, _nextSequence++, BuildIndex, cell));
        }

        _lastWall = wall ? cell : null;
    }

    /// <summary>
    /// Debug (M3 step 11): after a wall of the debug build is placed, the next one is ordered, so that a Shift + click
    /// soon has a ready one to start the line with, as a wall is dragged in RA2.
    /// </summary>
    private void OrderNextWall()
    {
        var def = Data.Database.Buildings[BuildIndex];
        if (def.Wall && Simulation.World.Player(LocalPlayer).Queue(def.Category).Count == 0)
        {
            Issue(new ProduceCommand(LocalPlayer, Simulation.CurrentTick, _nextSequence++, ProductionKind.Building, BuildIndex, 1));
        }
    }

    // The cell of the last wall placed with the debug build: a Shift + click draws a wall line from it.
    private CellPos? _lastWall;

    /// <summary>The cell of the last wall placed by a click, where a Shift + click line starts; the preview draws the line from it.</summary>
    public CellPos? LastWall => _lastWall;

    /// <summary>The sidebar (M3 step 12): orders <paramref name="count"/> of the item, or resumes it when it is on hold.</summary>
    public void IssueProduce(ProductionKind kind, int defIndex, int count) =>
        Issue(new ProduceCommand(LocalPlayer, Simulation.CurrentTick, _nextSequence++, kind, defIndex, count));

    /// <summary>The sidebar: puts the item in work on hold.</summary>
    public void IssuePause(ProductionKind kind, int defIndex) =>
        Issue(new PauseProductionCommand(LocalPlayer, Simulation.CurrentTick, _nextSequence++, kind, defIndex));

    /// <summary>The sidebar: cancels the last item of the definition, its credits come back.</summary>
    public void IssueCancel(ProductionKind kind, int defIndex) =>
        Issue(new CancelProductionCommand(LocalPlayer, Simulation.CurrentTick, _nextSequence++, kind, defIndex));

    /// <summary>The sidebar: the ready building goes into the placement mode.</summary>
    public void ArmPlacement(int defIndex)
    {
        BuildIndex = defIndex;
        DebugBuild = false;
    }

    /// <summary>V while nothing is armed: the debug build takes up its last building again.</summary>
    public void ResumeDebugBuild() => DebugBuild = true;

    /// <summary>
    /// Whether the building of the placement mode is still ready to place: once it is placed, cancelled or lost, the mode of the
    /// sidebar ends.
    /// </summary>
    public bool PlacementReady =>
        BuildIndex >= 0
        && Simulation.World.Player(LocalPlayer).Queue(Data.Database.Buildings[BuildIndex].Category) is [{ Ready: true } first, ..]
        && first.DefIndex == BuildIndex;

    /// <summary>Debug (L, M3 step 11): sells the player's building on the cell.</summary>
    public void IssueSell(CellPos cell)
    {
        if (Simulation.World.BuildingAt(cell) is { Def: not null } building && building.Owner == LocalPlayer)
        {
            Issue(new SellCommand(LocalPlayer, Simulation.CurrentTick, _nextSequence++, building.Id));
        }
    }

    /// <summary>Debug (J, M3 step 11): turns the repair of the player's building on the cell on or off.</summary>
    public void IssueToggleRepair(CellPos cell)
    {
        if (Simulation.World.BuildingAt(cell) is { } building && building.Owner == LocalPlayer
            && (building.Def is not null || Rts.Simulation.Systems.RepairSystem.Repairable(building)))
        {
            Issue(new ToggleRepairCommand(LocalPlayer, Simulation.CurrentTick, _nextSequence++, building.Id));
        }
    }

    /// <summary>
    /// The debug build (V, M3 step 8): takes the next building type (<see cref="NextBuildType"/>) and orders it when its queue is empty.
    /// The previous choice, if still unfinished, and an unfinished building of another type in the new queue are cancelled first, their
    /// credits come back; a finished one stays to be placed.
    /// </summary>
    public void ChooseBuild()
    {
        DebugBuild = true;
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

    /// <summary>The step of <see cref="SpeedMultipliers"/> for the game speed 1–6 of the skirmish screen.</summary>
    public static int SpeedIndexOf(int gameSpeed) => gameSpeed switch
    {
        <= 1 => 0,
        2 => 1,
        3 or 4 => 2,
        5 => 3,
        _ => 4,
    };

    public void ChangeSpeed(int direction) =>
        _speedIndex = Math.Clamp(_speedIndex + direction, 0, SpeedMultipliers.Length - 1);

    /// <summary>Writes the live session to user://replays and remembers the path for playback.</summary>
    public string? SaveReplay() => SaveReplay(ReplayDirectory, $"replay-{Simulation.CurrentTick}");

    /// <summary>Writes the live session as <paramref name="name"/> into <paramref name="folder"/> (M8 step 14: next to a playtest record).</summary>
    public string? SaveReplay(string folder, string name)
    {
        if (_recorder is null)
        {
            return null;
        }

        var directory = ProjectSettings.GlobalizePath(folder);
        Directory.CreateDirectory(directory);
        var fileName = $"{name}{ReplayFormat.FileExtension}";
        File.WriteAllBytes(Path.Combine(directory, fileName), ReplayFormat.Write(_recorder.Build()));
        LastReplayPath = $"{folder}/{fileName}";
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

    /// <summary>The same match once more with a new seed, on the spot (M5 step 12, the results screen).</summary>
    public void PlayAgain()
    {
        if (_match is null)
        {
            return;
        }

        _match = _match with { Seed = MatchSetups.NewSeed() };
        StartLiveSession();
    }

    /// <summary>Whether the person at the keyboard is in a match that goes on and has not lost it: then they may surrender (M5 step 10).</summary>
    public bool CanSurrender =>
        Simulation.World.Match?.PlayerOf(LocalPlayer) is not null && Simulation.World.Player(LocalPlayer).DefeatedTick is null
        && Simulation.World.Result is null;

    /// <summary>The person at the keyboard gives up the match.</summary>
    public void Surrender()
    {
        if (CanSurrender)
        {
            Issue(new SurrenderCommand(LocalPlayer, Simulation.CurrentTick, _nextSequence++));
        }
    }

    private void Issue(Command command)
    {
        if (_lockstep is not null)
        {
            // To everyone, on the tick D ticks ahead; the lockstep numbers it.
            _lockstep.Issue(command);
        }
        else if (_net is not null)
        {
            // Before tick 0 nobody gives orders.
        }
        else if (!IsPlayback)
        {
            Simulation.Enqueue(command);
        }
    }

    /// <summary>
    /// A frame of a network match (docs/implementation/M9_network.md, 4.16): until everyone has loaded the match waits; then the lockstep
    /// computes the ticks whose commands are all here, at the speed of the match, the host keeps the input delay to the network, the
    /// checksums are compared, and the screen says when someone is awaited or the match went apart.
    /// </summary>
    private void ProcessNetwork(double delta)
    {
        var net = _net!;
        if (net.Phase != Rts.Net.Session.SessionPhase.Running)
        {
            Alpha = 1f;
            ShowNetStatus(net.Phase == Rts.Net.Session.SessionPhase.Loading ? Tr("UI_NET_WAITING_LOAD") : "");
            return;
        }

        if (_lockstep is null)
        {
            StartLockstep(net);
        }

        var lockstep = _lockstep!;
        NetworkTestOptions(lockstep);
        var elapsedMs = (long)(delta * 1000);
        _netClockMs += elapsedMs;
        _latency!.Update(_netClockMs);
        _delay?.Update(_netClockMs);
        _referee?.Update(elapsedMs);
        _netMatch.Update(elapsedMs);
        lockstep.Update((long)(delta * 1000 * SpeedMultiplier));
        Alpha = lockstep.TickProgress;
        UpdateKickButtons(lockstep);

        if (_netEnded != "")
        {
            ShowNetStatus(_netEnded);
        }
        else if (_checksums!.Detector.Found is { } desync)
        {
            if (_checksums.Stopped && !_desyncSaved)
            {
                _desyncSaved = true;
                var folder = ProjectSettings.GlobalizePath($"user://desync/{DateTime.Now:yyyy-MM-dd_HH-mm-ss}_{desync.Tick}");
                Rts.Net.Desync.DesyncDump.Save(folder, desync, LocalPlayer, Simulation, _checksums.OwnChecksums, DataHash);
                GD.PrintErr($"[net] {desync}; dumps in {folder}");
            }

            ShowNetStatus(string.Format(Tr("UI_NET_DESYNC"), desync.Tick));
        }
        else if (_netMatch.PausedBy is { } pauser)
        {
            ShowNetStatus(string.Format(Tr(pauser == LocalPlayer ? "UI_NET_PAUSED_OWN" : "UI_NET_PAUSED"), pauser.Value,
                (_netMatch.PauseLeftMs + 999) / 1000, _netMatch.PausesLeft(LocalPlayer)));
        }
        else if (lockstep.IsWaitingForPlayers)
        {
            var who = string.Join(", ", lockstep.MissingPlayers().Select(player => player.Value));
            ShowNetStatus(string.Format(Tr("UI_NET_WAITING_PLAYER"), who, lockstep.WaitingMs / 1000));
        }
        else
        {
            ShowNetStatus(_netClockMs < _leftNoticeUntil ? _leftNotice : "");
        }
    }

    /// <summary>
    /// P in a network match (M9 step 10, 4.12): a pause for everyone, of those this player has left, or the end of its own pause. The
    /// host decides; a client asks it.
    /// </summary>
    public void ToggleNetworkPause()
    {
        if (_lockstep is null || _netEnded != "")
        {
            return;
        }

        var pause = _netMatch.PausedBy != LocalPlayer;
        if (_referee is not null)
        {
            _referee.RequestPause(LocalPlayer, pause);
        }
        else
        {
            SendToHost(new Rts.Net.Protocol.Messages.PauseRequest(LocalPlayer, pause));
        }
    }

    // A vote to drop a player who keeps everyone waiting (4.11): the host counts its own at once, a client's goes to it.
    private void VoteOut(PlayerId target)
    {
        _votedOut.Add(target);
        if (_referee is not null)
        {
            _referee.Vote(LocalPlayer, target);
        }
        else
        {
            SendToHost(new Rts.Net.Protocol.Messages.KickVote(LocalPlayer, target));
        }
    }

    // A client is connected to the host only: to everyone is to the host.
    private void SendToHost(Rts.Net.Protocol.NetMessage message) =>
        _net!.Transport.SendToAll(Rts.Net.Protocol.MessageCodec.Write(message), Rts.Net.Transport.DeliveryChannel.ReliableOrdered);

    // After 10 s of waiting a button for each player awaited: «Drop player N»; pressed, it waits for the others' votes.
    private void UpdateKickButtons(Rts.Net.Lockstep.LockstepEngine lockstep)
    {
        if (_kickButtons is null)
        {
            return;
        }

        var targets = _netEnded == "" && lockstep.WaitingMs >= _netMatch.Settings.KickVoteAfterMs
            ? lockstep.MissingPlayers().Where(player => player != LocalPlayer).ToList()
            : [];
        if (targets.Count == 0)
        {
            _votedOut.Clear();
        }

        var wanted = string.Join(",", targets.Select(player => $"{player.Value}{(_votedOut.Contains(player) ? "v" : "")}"));
        if ((string)_kickButtons.GetMeta("shown", "") == wanted)
        {
            return;
        }

        _kickButtons.SetMeta("shown", wanted);
        foreach (var child in _kickButtons.GetChildren())
        {
            child.QueueFree();
        }

        foreach (var target in targets)
        {
            var voted = _votedOut.Contains(target);
            var button = new Button
            {
                Text = string.Format(Tr(voted ? "UI_NET_KICK_VOTED" : "UI_NET_KICK"), target.Value),
                Disabled = voted,
                FocusMode = Control.FocusModeEnum.None,
            };
            button.Pressed += () => VoteOut(target);
            _kickButtons.AddChild(button);
        }
    }

    // What the host decided about who is in the match: a player out — this one, or another — and the host gone.
    private void OnPlayerRemoved(PlayerId player, int tick)
    {
        var reason = _netMatch.Left.TryGetValue(player, out var why) ? why : Rts.Net.Protocol.Messages.LeaveReason.Disconnected;
        GD.Print($"[net] player {player.Value} out on tick {tick}: {reason}");
        if (player == LocalPlayer)
        {
            _netEnded = Tr("UI_NET_YOU_WERE_DROPPED");
            return;
        }

        _leftNotice = string.Format(Tr(reason switch
        {
            Rts.Net.Protocol.Messages.LeaveReason.Timeout => "UI_NET_LEFT_TIMEOUT",
            Rts.Net.Protocol.Messages.LeaveReason.Kicked => "UI_NET_LEFT_KICKED",
            _ => "UI_NET_LEFT",
        }), player.Value);
        _leftNoticeUntil = _netClockMs + 6000;
    }

    private void OnHostLost()
    {
        if (_netEnded != "")
        {
            return;
        }

        // The direct mode has no host but the one (4.11): the match ends here, where it stands.
        _lockstep!.StopAt(_lockstep.CurrentTick);
        _netEnded = Tr("UI_NET_HOST_LEFT");
        GD.Print($"[net] the host left on tick {_lockstep.CurrentTick}");
    }

    // For trying two copies on one computer: --net-test-orders sends the own units to the middle of the map on tick 30;
    // --net-ticks N ends the game on tick N and prints how far the checksums agreed.
    private bool _testOrdered;

    private void NetworkTestOptions(Rts.Net.Lockstep.LockstepEngine lockstep)
    {
        var args = OS.GetCmdlineUserArgs();
        if (!_testOrdered && lockstep.CurrentTick >= 30 && args.Contains("--net-test-orders"))
        {
            _testOrdered = true;
            var own = Simulation.World.Entities.Where(entity => entity is Unit && entity.Owner == LocalPlayer).Select(entity => entity.Id).ToList();
            var middle = new Rts.Simulation.Geometry.CellPos(Simulation.World.WidthCells / 2, Simulation.World.HeightCells / 2).Center;
            IssueMove(own, middle, false);
            GD.Print($"[net] {own.Count} units sent to the middle");
        }

        if (Array.IndexOf(args, "--net-ticks") is var at and >= 0 && at + 1 < args.Length && int.TryParse(args[at + 1], out var last)
            && lockstep.CurrentTick >= last)
        {
            var players = Simulation.ExecutedCommands.Select(command => command.Player.Value).Distinct().Order();
            GD.Print($"[net] done on tick {lockstep.CurrentTick}: checksums agreed up to tick {_checksums!.Detector.LastAgreedTick}, "
                     + $"desync {(_checksums.Detector.Found?.ToString() ?? "none")}, commands of players {string.Join(", ", players)}, "
                     + $"input delay {lockstep.InputDelay}, checksums sent {_checksums.OwnChecksums.Count}, received {_checksums.ReceivedChecksums}");
            GetTree().Quit(_checksums.Detector.Found is null ? 0 : 1);
        }
    }

    // Tick 0: the lockstep of the match over the game's network — the host's through its relay — with the checksums, the pings, and on
    // the host the input delay.
    private void StartLockstep(Net.NetGame net)
    {
        var start = net.Start!;
        Rts.Net.Transport.ITransport transport = net.IsHost ? new Rts.Net.Transport.HostRelay(net.Transport) : net.Transport;
        _lockstep = new Rts.Net.Lockstep.LockstepEngine(Simulation, transport, LocalPlayer, net.ActivePlayers,
            new Rts.Net.Lockstep.LockstepSettings { InputDelay = start.InputDelay });
        _lockstep.Stepping += _ => CapturePoses();
        _lockstep.Stepped += _ => AfterStep();
        _checksums = new Rts.Net.Desync.ChecksumExchange(_lockstep, Simulation, transport, net.ActivePlayers);
        _latency = new Rts.Net.Lockstep.LatencyMonitor(transport);
        _latency.Attach(_lockstep);
        _delay = net.IsHost ? new Rts.Net.Lockstep.InputDelayAuthority(_lockstep, _latency, transport, Simulation.Settings.TicksPerSecond) : null;
        // Who leaves and when the game pauses the host decides (step 10); the clients hear of it.
        if (net.Host is { } host)
        {
            var players = host.Seats.Where(seat => net.ActivePlayers.Contains(new PlayerId(seat.Value)))
                .ToDictionary(seat => seat.Key, seat => new PlayerId(seat.Value));
            _referee = new Rts.Net.Session.MatchReferee(_lockstep, transport, players);
            _referee.Announced += _netMatch.Apply;
        }
        else
        {
            _lockstep.MessageReceived += (_, message) => _netMatch.Apply(message);
            _lockstep.PeerDisconnected += _ => OnHostLost();
        }

        _lockstep.PlayerRemoved += OnPlayerRemoved;
        _lockstep.Start();
        GD.Print($"[net] tick 0 as {LocalPlayer}, players {string.Join(", ", net.ActivePlayers)}, input delay {start.InputDelay}");
    }

    private void ShowNetStatus(string text)
    {
        if (_netStatus is null)
        {
            var layer = new CanvasLayer { Layer = 50 };
            AddChild(layer);
            _netStatus = new Label
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                AnchorLeft = 0f,
                AnchorRight = 1f,
                OffsetTop = 120f,
                OffsetBottom = 160f,
            };
            _netStatus.AddThemeFontSizeOverride("font_size", 22);
            _netStatus.AddThemeColorOverride("font_outline_color", Colors.Black);
            _netStatus.AddThemeConstantOverride("outline_size", 6);
            layer.AddChild(_netStatus);
            _kickButtons = new HBoxContainer
            {
                Alignment = BoxContainer.AlignmentMode.Center,
                AnchorLeft = 0f,
                AnchorRight = 1f,
                OffsetTop = 165f,
                OffsetBottom = 200f,
            };
            layer.AddChild(_kickButtons);
        }

        _netStatus.Text = text;
    }

    public override void _ExitTree()
    {
        // Leaving the match leaves the network game.
        _net?.Close();
        _net = null;
    }

    /// <summary>A match opens with the camera over the start of the person at the keyboard.</summary>
    private void LookAtStart()
    {
        if (_match?.PlayerOf(LocalPlayer) is { } player)
        {
            GetNodeOrNull<RtsCamera>("../CameraRig")?.CenterOn(WorldCoords.ToGodotOnGround(player.Start.Center));
        }

        if (_match is { } chosen)
        {
            // The speed of the skirmish screen, 1–6 as in RA2: 1 and 2 slow, 3 and 4 normal, 5 fast, 6 fastest.
            _speedIndex = SpeedIndexOf(chosen.Options.GameSpeed);
        }
    }

    /// <summary>The match setup the scene router brought, if any.</summary>
    private static MatchSetup? RoutedMatch() =>
        SceneRouter.Instance?.Args.TryGetValue(MatchArgument, out var routed) == true ? routed as MatchSetup : null;

    /// <summary>The map at <paramref name="path"/>, with its definition when it is an <c>.rtsmap</c> (its starts make a match).</summary>
    private (TestMap Map, MapDefinition? Definition) LoadMap(string path)
    {
        // The loading screen has read and parsed it on a background thread already (docs/implementation/M6_menus_skirmish.md, 4.14).
        if (PreparedMap.TryTake(path, out var prepared))
        {
            return prepared;
        }

        var text = Godot.FileAccess.GetFileAsString(path);
        if (string.IsNullOrEmpty(text))
        {
            throw new InvalidOperationException($"Cannot read {path}: {Godot.FileAccess.GetOpenError()}.");
        }

        return PreparedMap.Parse(path, text, Data.Rules);
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
        CapturePoses();
        Simulation.Step();
        AfterStep();
    }

    // Where every unit is before the tick, to draw it between the two.
    private void CapturePoses()
    {
        _previousPoses.Clear();
        foreach (var entity in Simulation.World.Entities)
        {
            if (entity is Unit unit)
            {
                _previousPoses[unit.Id] = new UnitPose(unit.Position, unit.Facing, unit.TurretFacing);
            }
        }
    }

    // What follows a tick: the replay, the measures, the events for the screen.
    private void AfterStep()
    {
        _recorder?.AfterStep();
        _ticksThisSecond++;
        QueueHistory.Add(Simulation.World.PathRequests.ExpandedLastTick, Simulation.World.PathRequests.Requests.Count);

        var wallPlaced = false;
        foreach (var simEvent in Simulation.LastTickEvents)
        {
            GD.Print($"[sim] {simEvent}");
            EventRaised?.Invoke(simEvent);
            if (NoticeOf(simEvent) is { } notice)
            {
                Notice = notice;
            }

            wallPlaced |= simEvent is BuildingPlaced placed && placed.Player == LocalPlayer && placed.DefIndex == BuildIndex;
        }

        if (wallPlaced && DebugBuild && _playback is null)
        {
            OrderNextWall();
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
            UnderAttack attack when attack.Player == LocalPlayer => $"under attack at {attack.Point.ToCell().X},{attack.Point.ToCell().Y}",
            BuildingSold sold when sold.Player == LocalPlayer => $"sold {buildings[sold.DefIndex].Id}, {sold.Refund} back",
            RepairToggled toggled when Simulation.World.TryGet(toggled.Building, out var repaired) && repaired.Owner == LocalPlayer =>
                toggled.On ? $"repairing {NameOf(toggled.Building)}" : "repair off",
            RepairDone done when Simulation.World.TryGet(done.Building, out var whole) && whole.Owner == LocalPlayer => "repair done",
            HarvesterUnloading unloading when Simulation.World.TryGet(unloading.Harvester, out var harvester) && harvester.Owner == LocalPlayer =>
                "harvester unloading",
            OreDepleted depleted when Simulation.CurrentTick - _lastDepletedNotice >= 75 => DepletedNotice(),
            PowerRestored restored when restored.Player == LocalPlayer => "power restored",
            _ => null,
        };
    }

    // The tick of the last "ore field depleted" notice: cells empty one after another, the notice comes once in 5 seconds at most.
    private int _lastDepletedNotice = -1000;

    private string DepletedNotice()
    {
        _lastDepletedNotice = Simulation.CurrentTick;
        return "a cell of ore is depleted";
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
        PlacementProblem.Ore => "ore",
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
