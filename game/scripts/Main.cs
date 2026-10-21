using System;
using System.Linq;
using Godot;
using Rts.Simulation;
using Rts.Simulation.Geometry;
using Rts.Simulation.World;

namespace Rts.Client;

/// <summary>
/// Scene root. Besides normal play it supports service modes, passed after <c>--</c> on the command line:
/// <c>--smoke</c> runs a scripted session, saves and replays it, and exits with 0 or 1;
/// <c>--check-camera</c> verifies the RA2 camera angle (a cell projects to a 2:1 diamond) and exits with 0 or 1;
/// <c>--check-terrain</c> clicks points on hill slopes and verifies the camera ray finds them on the terrain, exits with 0 or 1;
/// <c>--screenshot &lt;path&gt; [--frames N]</c> saves frame N as PNG and exits.
/// </summary>
public partial class Main : Node3D
{
    private const int SmokeTicks = 300;

    /// <summary>Direction the sunlight travels in: mostly along world +X, slightly towards +Z, about 50° downward.</summary>
    private static readonly Vector3 SunDirection = new Vector3(1f, -1.25f, 0.25f).Normalized();

    private string? _screenshotPath;
    private int _screenshotFrame = 60;
    private int _frame;

    public override void _Ready()
    {
        GD.Print($"Simulation v{SimulationVersion.Current}");
        // RA2 lighting: shadows fall right-down on screen, i.e. towards world +X, from a sun about 50° high.
        GetNode<DirectionalLight3D>("Sun").Basis = Basis.LookingAt(SunDirection, Vector3.Up);

        var args = OS.GetCmdlineUserArgs();
        var screenshotIndex = Array.IndexOf(args, "--screenshot");
        if (screenshotIndex >= 0 && screenshotIndex + 1 < args.Length)
        {
            _screenshotPath = args[screenshotIndex + 1];
        }

        var framesIndex = Array.IndexOf(args, "--frames");
        if (framesIndex >= 0 && framesIndex + 1 < args.Length && int.TryParse(args[framesIndex + 1], out var frames))
        {
            _screenshotFrame = frames;
        }

        if (args.Contains("--smoke"))
        {
            CallDeferred(MethodName.RunSmoke);
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
        if (_screenshotPath is null || ++_frame < _screenshotFrame)
        {
            return;
        }

        var error = GetViewport().GetTexture().GetImage().SavePng(_screenshotPath);
        GD.Print($"[main] screenshot {_screenshotPath}: {error}, FPS {Engine.GetFramesPerSecond():0}");
        GetTree().Quit(error == Error.Ok ? 0 : 1);
    }

    private void RunCameraCheck()
    {
        var ok = CheckCamera();
        GetTree().Quit(ok ? 0 : 1);
    }

    /// <summary>
    /// Projects the corners of one ground cell to the screen: the diamond must be twice as wide as it is tall
    /// (±1 %), world X must run right-down and world Y (Godot Z) left-down, as in RA2.
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
        return ok;
    }

    private void RunTerrainCheck()
    {
        var ok = CheckTerrain();
        GetTree().Quit(ok ? 0 : 1);
    }

    /// <summary>
    /// "A click on a slope gives the right point" (docs/implementation/M1_visual_spike.md, step 3): each sample point on the
    /// hill slopes is projected to the screen and clicked through the camera; the ground point found must be the sample point
    /// (within 0.05 cell), and the points must really lie on slopes.
    /// </summary>
    private bool CheckTerrain()
    {
        var terrain = WorldCoords.Terrain;
        if (terrain is null)
        {
            GD.Print("[terrain] FAILED: no terrain in the scene");
            return false;
        }

        (float X, float Z)[] samples =
        [
            (59f, 32f), (49f, 32f), (54f, 27f), (54f, 37f),
            (16.5f, 42f), (7.5f, 42f), (12f, 46.5f), (12f, 37.5f),
            (44f, 12f), (36f, 12f), (40f, 16f), (40f, 8f),
        ];
        var camera = GetNode<RtsCamera>("CameraRig");
        var worstError = 0f;
        var lowest = float.MaxValue;
        var missed = 0;
        foreach (var (x, z) in samples)
        {
            var point = new Vector3(x, terrain.HeightAt(x, z), z);
            lowest = Math.Min(lowest, point.Y);
            if (!camera.TryGroundAt(camera.UnprojectPosition(point), out var hit))
            {
                missed++;
                continue;
            }

            worstError = Math.Max(worstError, new Vector2(hit.X - x, hit.Z - z).Length());
        }

        var ok = missed == 0 && worstError <= 0.05f && lowest > 0.1f;
        GD.Print($"[terrain] {(ok ? "OK" : "FAILED")}: {samples.Length} slope points, lowest {lowest:0.00}, " +
                 $"worst click error {worstError:0.0000} cell, missed {missed}");
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

        var blueUnits = host.Simulation.World.Entities.OfType<Unit>().Where(u => u.Owner == SimulationHost.LocalPlayer).ToList();
        var arrived = blueUnits.Count > 0 && blueUnits.All(u => u.Position == target && u.MoveTarget is null);
        var liveChecksum = host.Simulation.LastChecksum;

        host.SaveReplay();
        host.StartPlayback();
        while (!host.PlaybackFinished)
        {
            host.StepOnce();
        }

        var sameFinalState = host.Simulation.LastChecksum == liveChecksum;
        var ok = arrived && host.PlaybackVerified && sameFinalState;
        GD.Print(ok
            ? $"[smoke] OK: {blueUnits.Count} units arrived, replay verified, final checksum {liveChecksum}"
            : $"[smoke] FAILED: arrived={arrived}, replayVerified={host.PlaybackVerified}, sameFinalState={sameFinalState}, status={host.ReplayStatus}");
        GetTree().Quit(ok ? 0 : 1);
    }
}
