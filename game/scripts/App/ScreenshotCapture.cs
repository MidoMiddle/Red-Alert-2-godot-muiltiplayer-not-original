using System;
using Godot;

namespace Rts.Client.App;

/// <summary>
/// Saves one frame of the current screen as PNG and quits. The UI screens use it with
/// <c>--screenshot &lt;path&gt; [--frames N]</c>, the same arguments that <c>Main</c> understands.
/// </summary>
public partial class ScreenshotCapture : Node
{
    private const int DefaultFrame = 60;

    private string _path = string.Empty;
    private int _framesLeft;

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

        owner.AddChild(new ScreenshotCapture { _path = args[pathIndex + 1], _framesLeft = frames });
    }

    public override void _Process(double delta)
    {
        _framesLeft--;
        if (_framesLeft > 0)
        {
            return;
        }

        var error = GetViewport().GetTexture().GetImage().SavePng(_path);
        GD.Print(error == Error.Ok ? $"[screenshot] saved {_path}" : $"[screenshot] failed: {error}");
        GetTree().Quit(error == Error.Ok ? 0 : 1);
    }
}
