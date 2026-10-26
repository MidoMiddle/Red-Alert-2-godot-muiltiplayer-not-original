using System;
using Godot;

namespace Rts.Client.Controls;

/// <summary>
/// Keeps the mouse inside the game window while the player plays (the owner's wish, 2026-10-25: «in the game the mouse must not leave
/// the field, in a window too»), so the view scrolls at any edge without aiming for it. It lets the mouse go while
/// <see cref="Released"/> says so — the pause menu, the statistics, the results are open — and while the window is not in focus, and
/// when the match ends. Runs while the tree is paused: the pause menu pauses it.
/// <para>
/// On macOS not with Godot's <c>Confined</c> mode: there it unties the cursor from the mouse and moves it itself, once a frame, so the
/// cursor went as smoothly as the game ran — every hitch a jerk of the mouse (the owner, 2026-10-25: «the mouse glitches»). There the
/// cursor stays the system's, and a cursor that went past the edge is brought back to it the same frame (<see cref="HoldOnMac"/>).
/// </para>
/// </summary>
public partial class MouseConfinement : Node
{
    /// <summary>Whether the mouse may leave the window now; set by the HUD that knows its panels.</summary>
    public Func<bool> Released { get; set; } = () => false;

    private static readonly bool OwnHold = OS.GetName() == "macOS";

    private bool? _confined;

    public override void _Ready() => ProcessMode = ProcessModeEnum.Always;

    public override void _Process(double delta)
    {
        using var frameTimer = Rts.Client.Debug.PerfLog.Time("MouseConfinement.process");
        // The window of a headless run (the tests, the smoke) has no mouse to hold.
        var confine = DisplayServer.GetName() != "headless" && DisplayServer.WindowIsFocused() && !Released();
        if (confine && OwnHold)
        {
            HoldOnMac();
        }

        if (confine == _confined)
        {
            return;
        }

        _confined = confine;
        if (!OwnHold)
        {
            Input.MouseMode = confine ? Input.MouseModeEnum.Confined : Input.MouseModeEnum.Visible;
        }

        GD.Print(confine ? "[mouse] held in the window" : "[mouse] free");
    }

    /// <summary>A cursor outside the window goes back to the nearest point of its edge, where the view scrolls.</summary>
    private static void HoldOnMac()
    {
        var window = new Rect2I(DisplayServer.WindowGetPosition(), DisplayServer.WindowGetSize());
        var mouse = DisplayServer.MouseGetPosition();
        if (window.Size.X <= 0 || window.Size.Y <= 0 || window.HasPoint(mouse))
        {
            return;
        }

        var inside = new Vector2I(Math.Clamp(mouse.X, window.Position.X, window.End.X - 1), Math.Clamp(mouse.Y, window.Position.Y, window.End.Y - 1));
        DisplayServer.WarpMouse(inside - window.Position);
    }

    public override void _ExitTree()
    {
        if (_confined == true && !OwnHold)
        {
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }
    }
}
