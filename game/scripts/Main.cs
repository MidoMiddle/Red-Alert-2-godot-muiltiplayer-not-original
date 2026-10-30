using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Rts.Client.Debug;
using Rts.Client.Orders;
using Rts.Simulation;
using Rts.Simulation.Benchmarks;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.World;

namespace Rts.Client;

/// <summary>
/// Scene root. Besides normal play it supports service modes, passed after <c>--</c> on the command line:
/// <c>--smoke</c> runs a scripted session, saves and replays it, and exits with 0 or 1;
/// <c>--check-camera</c> verifies the RA2 camera angle (a cell projects to a 2:1 diamond) and exits with 0 or 1;
/// <c>--check-terrain</c> checks that the drawn ground is the ground of the rules and that clicks find it, exits with 0 or 1;
/// <c>--screenshot &lt;path&gt; [--frames N]</c> saves frame N as PNG and exits.
/// </summary>
public partial class Main : Node3D
{
    private const int SmokeTicks = 300;
    private const int SmokeDetourTicks = 600;
    private const int SmokeSpread = 2;

    /// <summary>
    /// Direction the sunlight of the winter day travels in: mostly along world +X, slightly towards +Z, about 50° downward, so
    /// shadows fall right-down on screen as in RA2. The scene is lit by the map's time of day (game/data/climates/winter/look.yaml,
    /// whose day matches this); the shader warm-up takes this one.
    /// </summary>
    public static readonly Vector3 SunDirection = new Vector3(1f, -1.25f, 0.25f).Normalized();

    private string? _screenshotPath;
    private int _screenshotFrame = 60;
    private int _frame;

    // --look-at X Y: the cell the camera stands over for a screenshot, with the fog lifted (a debug view of a map's buildings).
    private Vector2? _lookAt;

    public override void _Ready()
    {
        GD.Print($"Simulation v{SimulationVersion.Current}");
        AddChild(new PathPreview { Name = "PathPreview" });
        AddChild(new CellOverlay { Name = "CellOverlay" });
        AddChild(new OrderMarkers { Name = "OrderMarkers" });
        AddChild(new RouteLines { Name = "RouteLines" });
        AddChild(new RangeRings { Name = "RangeRings" });
        AddChild(new Effects.CombatEffects { Name = "CombatEffects" });
        AddChild(new Effects.RadiationView { Name = "RadiationView" });
        AddChild(new Effects.ProjectileViews { Name = "ProjectileViews" });
        AddChild(new Effects.CrateViews { Name = "CrateViews" });
        AddChild(new Effects.IntentLines { Name = "IntentLines" });
        AddChild(new Audio.CombatSounds { Name = "CombatSounds" });
        AddChild(new Terrain.FogOfWar { Name = "FogOfWar" });
        // The health bars lie under the HUD (M4 step 13).
        var bars = new CanvasLayer { Name = "Bars", Layer = 0 };
        bars.AddChild(new Hud.HealthBars { Name = "HealthBars" });
        AddChild(bars);

        var args = OS.GetCmdlineUserArgs();
        var screenshotIndex = Array.IndexOf(args, "--screenshot");
        if (screenshotIndex >= 0 && screenshotIndex + 1 < args.Length)
        {
            _screenshotPath = args[screenshotIndex + 1];
        }

        var lookIndex = Array.IndexOf(args, "--look-at");
        if (lookIndex >= 0 && lookIndex + 2 < args.Length && int.TryParse(args[lookIndex + 1], out var lookX)
            && int.TryParse(args[lookIndex + 2], out var lookY))
        {
            _lookAt = new Vector2(lookX + 0.5f, lookY + 0.5f);
        }

        var framesIndex = Array.IndexOf(args, "--frames");
        if (framesIndex >= 0 && framesIndex + 1 < args.Length && int.TryParse(args[framesIndex + 1], out var frames))
        {
            _screenshotFrame = frames;
        }

        if (App.GraphicsBenchmark.Requested)
        {
            CallDeferred(MethodName.RunGraphicsTest);
        }
        else if (args.Contains("--smoke"))
        {
            CallDeferred(MethodName.RunSmoke);
        }
        else if (args.Contains("--perf"))
        {
            CallDeferred(MethodName.RunPerf);
        }
        else if (SimulationHost.IsBattle)
        {
            CallDeferred(MethodName.RunPerfBattle);
        }
        else if (args.Contains("--check-camera"))
        {
            CallDeferred(MethodName.RunCameraCheck);
        }
        else if (args.Contains("--check-terrain"))
        {
            CallDeferred(MethodName.RunTerrainCheck);
        }
    }

    public override void _Process(double delta)
    {
        using var frameTimer = Rts.Client.Debug.PerfLog.Time("Main.process");
        TrackPerf(delta);
        if (_lookAt is { } look && _frame < 10)
        {
            // Over the first frames, so the start of the match does not take the camera back to the player's start.
            var rig = GetNode<RtsCamera>("CameraRig");
            rig.CenterOn(new Vector3(look.X, 0f, look.Y));
            rig.ZoomInFully();
            Terrain.FogOfWar.Current?.Reveal();
            _frame += _screenshotPath is null ? 1 : 0;
        }

        if (_screenshotPath is null || ++_frame < _screenshotFrame)
        {
            return;
        }

        var error = GetViewport().GetTexture().GetImage().SavePng(_screenshotPath);
        GD.Print($"[main] screenshot {_screenshotPath}: {error}, FPS {Engine.GetFramesPerSecond():0}");
        GetTree().Quit(error == Error.Ok ? 0 : 1);
    }

    /// <summary>
    /// <c>--perf</c> (docs/implementation/M2_unit_control.md, 4.15): the load map with 300 units, the camera zoomed out over
    /// the start, the four groups ordered across the map as in the benchmarks; frame times are logged for
    /// <see cref="PerfSeconds"/> seconds, then the game quits. Exit code 0 when the average reaches 60 FPS.
    /// </summary>
    private void RunPerf()
    {
        var camera = GetNode<RtsCamera>("CameraRig");
        camera.CenterOn(new Vector3(24f, 0f, 64f));
        camera.ZoomOutFully();
        _perfFrames = new List<double>();
    }

    /// <summary>
    /// <c>--perf-battle</c> (docs/implementation/M4_combat.md, 4.21, step 15): the general battle of 150 against 150 on the load map,
    /// the camera zoomed out over the clash — shots, projectiles, explosions, health bars and the fog all at once; measured as
    /// <c>--perf</c>, from the charge on.
    /// </summary>
    private void RunPerfBattle()
    {
        var camera = GetNode<RtsCamera>("CameraRig");
        var middle = (BattleScenario.BlueLeft + BattleScenario.RedLeft + 2 * BattleScenario.Columns) / 2f;
        camera.CenterOn(new Vector3(middle, 0f, BattleScenario.Top + BattleScenario.Rows));
        camera.ZoomOutFully();
        _perfFrames = new List<double>();
        _perfStart = ChargeBattle;
    }

    /// <summary>
    /// The graphics test (<see cref="App.GraphicsBenchmark"/>, the owner, 2026-10-28): the general battle of <c>--perf-battle</c>, seen as
    /// a player sees it — at the zoom of a match, the clash in the middle of the screen, the land without its shroud — the levels of graphics
    /// tried over it once the armies charge.
    /// </summary>
    private void RunGraphicsTest()
    {
        var camera = GetNode<RtsCamera>("CameraRig");
        var middle = (BattleScenario.BlueLeft + BattleScenario.RedLeft + 2 * BattleScenario.Columns) / 2f;
        camera.CenterOn(new Vector3(middle, 0f, BattleScenario.Top + BattleScenario.Rows / 2f));
        AddChild(new App.GraphicsBenchmark(() =>
        {
            if (GetNode<SimulationHost>("SimulationHost").Simulation.World.Entities.Count == 0)
            {
                return false;
            }

            // The whole land drawn, as a match shows it once explored: the shroud is no picture to measure.
            Terrain.FogOfWar.Current?.Reveal();
            return ChargeBattle();
        })
        {
            Name = "GraphicsBenchmark",
        });
    }

    /// <summary>Both armies of the battle charge, once they have spawned (on the first tick).</summary>
    private bool ChargeBattle()
    {
        var simulation = GetNode<SimulationHost>("SimulationHost").Simulation;
        BattleScenario.Charge(simulation);
        GD.Print($"[perf] {simulation.World.Entities.Count} units charge, window {GetViewport().GetVisibleRect().Size}");
        return true;
    }

    /// <summary>The orders of the performance run, once the map's units have spawned (on the first tick).</summary>
    private bool OrderPerfGroups()
    {
        var host = GetNode<SimulationHost>("SimulationHost");
        var units = host.Simulation.World.Entities
            .Where(e => e is Unit { IsAlive: true } && e.Owner == SimulationHost.LocalPlayer)
            .Select(e => e.Id)
            .ToList();
        (int X, int Y)[] targets = [(100, 50), (104, 66), (100, 82), (96, 96)];
        for (var group = 0; group < targets.Length; group++)
        {
            var members = units.Skip(group * PerfGroupSize).Take(PerfGroupSize).ToList();
            host.IssueMove(members, new CellPos(targets[group].X, targets[group].Y).Center);
        }

        GD.Print($"[perf] {units.Count} units ordered across the map, window {GetViewport().GetVisibleRect().Size}");
        return units.Count > 0;
    }

    private const int PerfGroupSize = 75;
    private const double PerfSeconds = 20;
    private List<double>? _perfFrames;
    private Func<bool>? _perfStart;
    private bool _perfOrdered;

    /// <summary>Collects frame times of the performance run and prints the result at the end.</summary>
    private void TrackPerf(double delta)
    {
        if (_perfFrames is null)
        {
            return;
        }

        if (!_perfOrdered)
        {
            if (GetNode<SimulationHost>("SimulationHost").Simulation.World.Entities.Count > 0)
            {
                _perfOrdered = (_perfStart ?? OrderPerfGroups)();
            }

            return;
        }

        _perfFrames.Add(delta);
        if (_perfFrames.Sum() < PerfSeconds)
        {
            return;
        }

        // The first frames include loading and the order tick; the steady part is what the target is about.
        var frames = _perfFrames.Skip(30).ToList();
        var sorted = frames.OrderByDescending(t => t).ToList();
        var average = frames.Count / frames.Sum();
        var worst = 1.0 / sorted[0];
        var low1 = 1.0 / sorted.Take(Math.Max(1, sorted.Count / 100)).Average();
        var info = RenderingServer.GetRenderingInfo(RenderingServer.RenderingInfo.TotalDrawCallsInFrame);
        GD.Print($"[perf] {frames.Count} frames: average {average:0.0} FPS, 1% low {low1:0.0} FPS, worst {worst:0.0} FPS, draw calls {info}");
        _perfFrames = null;
        GetTree().Quit(average >= 60 ? 0 : 1);
    }

    private void RunCameraCheck()
    {
        var ok = CheckCamera();
        GetTree().Quit(ok ? 0 : 1);
    }

    /// <summary>
    /// Projects the corners of one ground cell to the screen: the diamond must be twice as wide as it is tall
    /// (±1 %), world X must run right-down and world Y (Godot Z) left-down, as in RA2. Then the view stops at the frame of
    /// the map (<see cref="CheckCameraFrame"/>).
    /// </summary>
    private bool CheckCamera()
    {
        var camera = GetNode<RtsCamera>("CameraRig").Camera;
        var origin = camera.UnprojectPosition(new Vector3(32f, 0f, 32f));
        var alongX = camera.UnprojectPosition(new Vector3(33f, 0f, 32f)) - origin;
        var alongZ = camera.UnprojectPosition(new Vector3(32f, 0f, 33f)) - origin;
        var opposite = camera.UnprojectPosition(new Vector3(33f, 0f, 33f)) - origin;

        float[] xs = [0f, alongX.X, alongZ.X, opposite.X];
        float[] ys = [0f, alongX.Y, alongZ.Y, opposite.Y];
        var width = xs.Max() - xs.Min();
        var height = ys.Max() - ys.Min();
        var ratio = width / height;

        var ratioOk = Math.Abs(ratio - 2f) <= 0.02f;
        var directionsOk = alongX.X > 0f && alongX.Y > 0f && alongZ.X < 0f && alongZ.Y > 0f;
        var ok = ratioOk && directionsOk;
        GD.Print($"[camera] {(ok ? "OK" : "FAILED")}: diamond {width:0.0}x{height:0.0} px, ratio {ratio:0.000}, " +
                 $"world X on screen {alongX}, world Y on screen {alongZ}");
        return CheckCameraFrame() && ok;
    }

    /// <summary>
    /// As in RA2 the view stops at the edge of the map: driven as far as it goes to the top, bottom, left and right, the corner
    /// of its frame (the playable area, or the whole map) that reaches furthest that way stands on that edge of the field (±2 px),
    /// so nothing beyond the frame shows and a unit in that corner is at the edge of the screen. There, at the default zoom and zoomed out fully, the
    /// corners of the field look at the land beyond the edge (the skirt, <see cref="Terrain.MapSkirt"/>), never past it. The
    /// camera goes back where it was.
    /// </summary>
    private bool CheckCameraFrame()
    {
        var rig = GetNode<RtsCamera>("CameraRig");
        var map = GetNode<SimulationHost>("SimulationHost").Simulation.World.Map;
        var (w, h, far) = (map.Width, map.Height, 10_000f);
        var corners = rig.FrameCorners();
        var start = rig.Position;
        var startSize = rig.Camera.Size;
        var skirt = GetNode<Terrain.TerrainBuilder>("Terrain").Heights.Skirt;
        var worst = 0f;
        var beyond = float.MinValue;
        foreach (var (name, target) in new[]
                 {
                     ("top", new Vector3(-far, 0f, -far)), ("bottom", new Vector3(far, 0f, far)),
                     ("left", new Vector3(-far, 0f, far)), ("right", new Vector3(far, 0f, -far)),
                 })
        {
            rig.CenterOn(target);
            rig._Process(0);
            var field = rig.FieldRect();
            var screen = System.Array.ConvertAll(corners, rig.UnprojectPosition);
            var gap = name switch
            {
                "top" => screen.Min(p => p.Y) - field.Position.Y,
                "bottom" => field.End.Y - screen.Max(p => p.Y),
                "left" => screen.Min(p => p.X) - field.Position.X,
                _ => field.End.X - screen.Max(p => p.X),
            };
            // A map smaller than the field that way sits in its middle: there is no edge to stop at.
            var smaller = name is "top" or "bottom"
                ? screen.Max(p => p.Y) - screen.Min(p => p.Y) < field.Size.Y
                : screen.Max(p => p.X) - screen.Min(p => p.X) < field.Size.X;
            worst = smaller ? worst : Math.Max(worst, Math.Abs(gap));

            foreach (var size in new[] { RtsCamera.DefaultSize, RtsCamera.DefaultSize * RtsCamera.MaxZoom })
            {
                rig.Camera.Size = size;
                rig.CenterOn(target);
                rig._Process(0);
                var view = rig.FieldRect();
                foreach (var corner in new[] { view.Position, new Vector2(view.End.X, view.Position.Y), new Vector2(view.Position.X, view.End.Y), view.End })
                {
                    // Where the corner's ray meets the ground plane: how far beyond the skirt that is (0 inside it).
                    var origin = rig.Camera.ProjectRayOrigin(corner);
                    var direction = rig.Camera.ProjectRayNormal(corner);
                    var ground = origin + direction * (-origin.Y / direction.Y);
                    beyond = Math.Max(beyond, Math.Max(Math.Max(-skirt - ground.X, ground.X - w - skirt), Math.Max(-skirt - ground.Z, ground.Z - h - skirt)));
                }
            }

            rig.Camera.Size = startSize;
        }

        rig.CenterOn(start);
        rig._Process(0);
        var ok = worst <= 2f && beyond <= 0f;
        GD.Print($"[camera] frame {(ok ? "OK" : "FAILED")}: at the far top, bottom, left and right the map's corner is {worst:0.0} px from the field's edge; " +
                 $"the field's corners stop {-beyond:0.0} cells short of the end of the skirt of {skirt}");
        return ok;
    }

    private void RunTerrainCheck()
    {
        var ok = CheckTerrain();
        GetTree().Quit(ok ? 0 : 1);
    }

    /// <summary>
    /// The ground drawn is the ground of the rules (docs/implementation/M7_maps.md, 4.12, step 9), and a click finds it: at
    /// points spread over the map the height of the view equals the height of the simulation (but on a cliff's wall), and each point projected to
    /// the screen and clicked through the camera comes back within 0.05 cell (docs/implementation/M1_visual_spike.md, step 3).
    /// </summary>
    private bool CheckTerrain()
    {
        var terrain = WorldCoords.Terrain;
        if (terrain is null)
        {
            GD.Print("[terrain] FAILED: no terrain in the scene");
            return false;
        }

        var map = GetNode<SimulationHost>("SimulationHost").Simulation.World.Map;
        var camera = GetNode<RtsCamera>("CameraRig");
        var worstHeight = 0f;
        var worstClick = 0f;
        var missed = 0;
        var occluded = 0;
        var samples = 0;
        var onScreen = 0;
        var view = GetViewport().GetVisibleRect();
        for (var z = 4.25f; z < map.Height; z += 7.5f)
        {
            for (var x = 3.5f; x < map.Width; x += 7.5f)
            {
                samples++;
                var rules = map.Heights.HeightAt(new WorldPos((int)(x * WorldPos.LeptonsPerCell), (int)(z * WorldPos.LeptonsPerCell)));
                var point = new Vector3(x, terrain.HeightAt(x, z), z);
                // A cliff is drawn as a wall (TerrainHeights.CliffHeight): nobody stands there, so its height may leave the rules'.
                if (map.Heights.ShapeAt(new CellPos((int)x, (int)z)) != CellShape.Cliff)
                {
                    worstHeight = Math.Max(worstHeight, Math.Abs(point.Y - rules.Raw / (float)Rts.Simulation.Math.Fixed.OneRaw / WorldPos.LeptonsPerCell));
                }

                // Only what the player can point at: a point on the screen that no higher hill hides from the camera.
                var screen = camera.UnprojectPosition(point);
                if (screen.X < view.Position.X || screen.Y < view.Position.Y || screen.X > view.End.X || screen.Y > view.End.Y)
                {
                    continue;
                }

                onScreen++;
                if (!camera.TryGroundAt(screen, out var hit))
                {
                    missed++;
                    continue;
                }

                if (hit.Y > point.Y + 0.05f)
                {
                    occluded++; // a higher hill stands in front of the point: the click rightly finds the hill
                    continue;
                }

                worstClick = Math.Max(worstClick, new Vector2(hit.X - x, hit.Z - z).Length());
            }
        }

        var ok = onScreen > 0 && missed == 0 && worstClick <= 0.05f && worstHeight <= 0.0001f;
        GD.Print($"[terrain] {(ok ? "OK" : "FAILED")}: {samples} points, {onScreen} on screen ({occluded} hidden by a hill), " +
                 $"worst height error {worstHeight:0.000000}, worst click error {worstClick:0.0000} cell, missed {missed}");
        return ok;
    }

    /// <summary>
    /// Scripted check through the same host API the player uses: spawn, order a move, play,
    /// save the replay, play it back and compare every checksum.
    /// </summary>
    private void RunSmoke()
    {
        if (!CheckCamera() || !CheckTerrain())
        {
            GD.Print("[smoke] FAILED: camera or terrain check");
            GetTree().Quit(1);
            return;
        }

        var host = GetNode<SimulationHost>("SimulationHost");
        host.Paused = true;
        host.StepOnce(); // tick 0: starting units

        var target = new CellPos(32, 32).Center;
        host.IssueMove(target);
        for (var i = 0; i < SmokeTicks; i++)
        {
            host.StepOnce();
        }

        // Group destinations (M2 steps 9–10): every unit stops on a place of its own, all within SmokeSpread cells of the point.
        var blueUnits = host.Simulation.World.Entities.OfType<Unit>().Where(u => u.Owner == SimulationHost.LocalPlayer).ToList();
        var arrived = blueUnits.Count > 0 && blueUnits.All(u => u.MoveTarget is null && Near(u, target)) && OwnCells(blueUnits);
        if (!arrived)
        {
            foreach (var u in blueUnits)
            {
                GD.Print($"[smoke] {u.Id} {u.Type} at {u.Position.ToCell()} target {u.MoveTarget?.ToCell()} resume {u.ResumeTarget?.ToCell()} hold {u.HoldTicks} wait {u.WaitTicks} replans {u.Replans} pending {u.PathPending}");
            }
        }

        // Second leg: past the forest north of the blue base. The straight line crosses the trees, so every unit
        // must drive around them along its path (M2 step 6) and never get stopped by an obstacle.
        var behindForest = new CellPos(22, 5).Center;
        host.IssueMove(behindForest);
        // The harvester goes back to the sandbox ore once the order runs out (M3, HarvesterSystem.ManualTicks, shorter than this
        // leg), so a unit counts once it has stood at the point.
        var blocked = 0;
        var reached = new HashSet<Unit>();
        for (var i = 0; i < SmokeDetourTicks; i++)
        {
            host.StepOnce();
            blocked += host.Simulation.LastTickEvents.Count(e => e is UnitBlocked);
            reached.UnionWith(blueUnits.Where(u => u.MoveTarget is null && Near(u, behindForest)));
        }

        var detoured = blocked == 0 && reached.Count == blueUnits.Count && OwnCells(blueUnits);
        var liveChecksum = host.Simulation.LastChecksum;

        host.SaveReplay();
        host.StartPlayback();
        while (!host.PlaybackFinished)
        {
            host.StepOnce();
        }

        var sameFinalState = host.Simulation.LastChecksum == liveChecksum;
        var ok = arrived && detoured && host.PlaybackVerified && sameFinalState;
        GD.Print(ok
            ? $"[smoke] OK: {blueUnits.Count} units arrived and drove around the forest, replay verified, final checksum {liveChecksum}"
            : $"[smoke] FAILED: arrived={arrived}, detoured={detoured} (blocked {blocked}), replayVerified={host.PlaybackVerified}, sameFinalState={sameFinalState}, status={host.ReplayStatus}");
        GetTree().Quit(ok ? 0 : 1);
    }

    private static bool Near(Unit unit, WorldPos point)
    {
        var cell = unit.Position.ToCell();
        var target = point.ToCell();
        return Math.Max(Math.Abs(cell.X - target.X), Math.Abs(cell.Y - target.Y)) <= SmokeSpread;
    }

    /// <summary>Every unit on a place of its own: vehicles in their own cells, soldiers on their own spots, never sharing with vehicles.</summary>
    private static bool OwnCells(List<Unit> units)
    {
        var vehicleCells = units.Where(u => u.Archetype.Placement == UnitPlacement.Cell).Select(u => u.Position.ToCell()).ToList();
        var soldiers = units.Where(u => u.Archetype.Placement == UnitPlacement.Subcell).ToList();
        return vehicleCells.Distinct().Count() == vehicleCells.Count
            && soldiers.Select(u => u.Position).Distinct().Count() == soldiers.Count
            && !soldiers.Any(u => vehicleCells.Contains(u.Position.ToCell()));
    }
}
