using System;
using Godot;

namespace Rts.Client.App;

/// <summary>
/// Saves one frame of the current screen as PNG and quits. The UI screens use it with
/// <c>--screenshot &lt;path&gt; [--frames N] [--press &lt;button name&gt;]</c>, the same arguments that <c>Main</c>
/// understands. <c>--press</c> emits the button's pressed signal halfway to the capture, so a dialog can be checked.
/// </summary>
public partial class ScreenshotCapture : Node
{
    private const int DefaultFrame = 60;

    private string _path = string.Empty;
    private string? _pressName;
    private int _captureFrame;
    private int _frame;

    /// <summary>Adds a capture node under <paramref name="owner"/> if the arguments ask for a screenshot.</summary>
    public static void AttachIfRequested(Node owner)
    {
        var args = OS.GetCmdlineUserArgs();
        var pathIndex = Array.IndexOf(args, "--screenshot");
        if (pathIndex < 0 || pathIndex + 1 >= args.Length)
        {
            return;
        }

        var frames = DefaultFrame;
        var framesIndex = Array.IndexOf(args, "--frames");
        if (framesIndex >= 0 && framesIndex + 1 < args.Length && int.TryParse(args[framesIndex + 1], out var parsed))
        {
            frames = parsed;
        }

        var pressIndex = Array.IndexOf(args, "--press");
        var pressName = pressIndex >= 0 && pressIndex + 1 < args.Length ? args[pressIndex + 1] : null;

        owner.AddChild(new ScreenshotCapture
        {
            _path = args[pathIndex + 1],
            _captureFrame = frames,
            _pressName = pressName,
        });
    }

    public override void _Process(double delta)
    {
        using var frameTimer = Rts.Client.Debug.PerfLog.Time("ScreenshotCapture.process");
        _frame++;
        if (_pressName is not null && _frame == _captureFrame / 2)
        {
            Press(_pressName);
        }

        if (_frame < _captureFrame)
        {
            return;
        }

        var error = GetViewport().GetTexture().GetImage().SavePng(_path);
        GD.Print(error == Error.Ok ? $"[screenshot] saved {_path}" : $"[screenshot] failed: {error}");
        GetTree().Quit(error == Error.Ok ? 0 : 1);
    }

    private void Press(string name)
    {
        if (GetTree().CurrentScene?.FindChild(name, true, false) is BaseButton button)
        {
            button.EmitSignal(BaseButton.SignalName.Pressed);
        }
        else
        {
            GD.PushError($"[screenshot] no button named {name}");
        }
    }
}
