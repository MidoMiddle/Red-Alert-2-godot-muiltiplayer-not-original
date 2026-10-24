using System;
using Godot;

namespace Rts.Client.Controls;

/// <summary>
/// Keeps the mouse inside the game window while the player plays (the owner's wish, 2026-10-25: «in the game the mouse must not leave
/// the field, in a window too»), so the view scrolls at any edge without aiming for it. It lets the mouse go while
/// <see cref="Released"/> says so — the pause menu, the statistics, the results are open — and while the window is not in focus, and
/// when the match ends. Runs while the tree is paused: the pause menu pauses it.
/// </summary>
public partial class MouseConfinement : Node
{
    /// <summary>Whether the mouse may leave the window now; set by the HUD that knows its panels.</summary>
    public Func<bool> Released { get; set; } = () => false;

    private bool? _confined;

    public override void _Ready() => ProcessMode = ProcessModeEnum.Always;

    public override void _Process(double delta)
    {
        // The window of a headless run (the tests, the smoke) has no mouse to hold.
        var confine = DisplayServer.GetName() != "headless" && DisplayServer.WindowIsFocused() && !Released();
        if (confine == _confined)
        {
            return;
        }

        _confined = confine;
        Input.MouseMode = confine ? Input.MouseModeEnum.Confined : Input.MouseModeEnum.Visible;
        GD.Print(confine ? "[mouse] held in the window" : "[mouse] free");
    }

    public override void _ExitTree()
    {
        if (_confined == true)
        {
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }
    }
}
