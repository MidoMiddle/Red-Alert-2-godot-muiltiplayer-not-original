using System;
using System.Linq;
using Godot;
using Rts.Simulation;
using Rts.Simulation.Geometry;
using Rts.Simulation.World;

namespace Rts.Client;

/// <summary>
/// Scene root. Besides normal play it supports two service modes, passed after <c>--</c> on the command line:
/// <c>--smoke</c> runs a scripted session, saves and replays it, and exits with 0 or 1;
/// <c>--screenshot &lt;path&gt; [--frames N]</c> saves frame N as PNG and exits.
/// </summary>
public partial class Main : Node3D
{
    private const int SmokeTicks = 300;

    private string? _screenshotPath;
    private int _screenshotFrame = 60;
    private int _frame;

    public override void _Ready()
    {
        GD.Print($"Simulation v{SimulationVersion.Current}");
        GetNode<DirectionalLight3D>("Sun").RotationDegrees = new Vector3(-55f, 35f, 0f);

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
    }

    public override void _Process(double delta)
    {
        if (_screenshotPath is null || ++_frame < _screenshotFrame)
        {
            return;
        }

        var error = GetViewport().GetTexture().GetImage().SavePng(_screenshotPath);
        GD.Print($"[main] screenshot {_screenshotPath}: {error}");
        GetTree().Quit(error == Error.Ok ? 0 : 1);
    }

    /// <summary>
    /// Scripted check through the same host API the player uses: spawn, order a move, play,
    /// save the replay, play it back and compare every checksum.
    /// </summary>
    private void RunSmoke()
    {
        var host = GetNode<SimulationHost>("SimulationHost");
        host.Paused = true;
        host.StepOnce(); // tick 0: starting units

        var target = new CellPos(32, 32).Center;
        host.IssueMove(target);
        for (var i = 0; i < SmokeTicks; i++)
        {
            host.StepOnce();
        }

        var blueUnits = host.Simulation.World.Entities.OfType<TestUnit>().Where(u => u.Owner == SimulationHost.LocalPlayer).ToList();
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
