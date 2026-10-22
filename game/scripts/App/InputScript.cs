using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Godot;

namespace Rts.Client.App;

/// <summary>
/// Debug harness: plays a script of mouse and key events into the running game, so the controls can be checked without a person
/// at the keyboard. Started with <c>--input-script &lt;path&gt;</c> after <c>--</c>. One command per line; empty lines and lines that
/// start with <c>#</c> are skipped: <c>wait N</c>, <c>node NAME</c> (presses a button), <c>click|down|up BUTTON X Y [MODIFIERS]</c>
/// (BUTTON is left, right or middle), <c>move X Y</c> (the system pointer moves there too), <c>key NAME</c> (a Godot key name), <c>keydown NAME</c> and <c>keyup NAME</c>
/// (hold and release a key, a modifier included), <c>shot PATH</c> (saves a PNG), <c>log TEXT</c>. MODIFIERS are any of
/// <c>shift</c>, <c>ctrl</c>, <c>alt</c>, <c>meta</c>, added to the modifiers held with <c>keydown</c>. X and Y are in the game's
/// 1920 x 1080 base space, whatever the window size is. The run ends with exit code 0 after the last line.
/// </summary>
public partial class InputScript : Node
{
    private readonly Queue<string> _lines = new();
    private int _waitFrames;

    // Modifiers held with keydown, so that later events carry them the way a keyboard would.
    private bool _shift;
    private bool _ctrl;
    private bool _alt;
    private bool _meta;

    /// <summary>Adds a script player under <paramref name="owner"/> if <c>--input-script</c> asks for one.</summary>
    public static void AttachIfRequested(Node owner)
    {
        var args = OS.GetCmdlineUserArgs();
        var index = Array.IndexOf(args, "--input-script");
        if (index < 0 || index + 1 >= args.Length)
        {
            return;
        }

        // Keeps the script running while the pause menu stops the game.
        var script = new InputScript { ProcessMode = ProcessModeEnum.Always };
        foreach (var raw in File.ReadAllLines(args[index + 1]))
        {
            var line = raw.Trim();
            if (line.Length > 0 && !line.StartsWith('#'))
            {
                script._lines.Enqueue(line);
            }
        }

        owner.AddChild(script);
        GD.Print($"[input-script] {script._lines.Count} commands from {args[index + 1]}");
    }

    public override void _Process(double delta)
    {
        if (_waitFrames > 0)
        {
            _waitFrames--;
            return;
        }

        if (_lines.Count == 0)
        {
            GD.Print("[input-script] done");
            GetTree().Quit(0);
            return;
        }

        Execute(_lines.Dequeue());
    }

    private void Execute(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        GD.Print($"[input-script] {line}");
        switch (parts[0])
        {
            case "wait":
                _waitFrames = int.Parse(parts[1], CultureInfo.InvariantCulture);
                break;
            case "node":
                PressNode(parts[1]);
                break;
            case "click":
                {
                    var button = ParseButton(parts[1]);
                    var at = Position(parts[2], parts[3]);
                    var mods = HeldModifiers(parts, 4);
                    Mouse(button, true, at, mods);
                    Mouse(button, false, at, mods);
                    break;
                }
            case "down":
                Mouse(ParseButton(parts[1]), true, Position(parts[2], parts[3]), HeldModifiers(parts, 4));
                break;
            case "up":
                Mouse(ParseButton(parts[1]), false, Position(parts[2], parts[3]), HeldModifiers(parts, 4));
                break;
            case "move":
                {
                    var to = Position(parts[1], parts[2]);
                    // The pointer itself moves too: hover and the cell under the pointer follow it, not the motion event.
                    Input.WarpMouse(to);
                    Input.ParseInputEvent(new InputEventMouseMotion { Position = to, GlobalPosition = to });
                    break;
                }
            case "key":
                {
                    var key = ParseKey(parts[1]);
                    SendKey(key, true);
                    SendKey(key, false);
                    break;
                }
            case "keydown":
                HoldKey(ParseKey(parts[1]), true);
                break;
            case "keyup":
                HoldKey(ParseKey(parts[1]), false);
                break;
            case "shot":
                {
                    var error = GetViewport().GetTexture().GetImage().SavePng(parts[1]);
                    GD.Print($"[input-script] saved {parts[1]}: {error}");
                    break;
                }
            case "log":
                GD.Print("[input-script] " + (line.Length > 4 ? line[4..] : string.Empty));
                break;
            default:
                GD.PushError($"[input-script] unknown command {parts[0]}");
                break;
        }
    }

    private void PressNode(string name)
    {
        if (GetTree().CurrentScene?.FindChild(name, true, false) is BaseButton button)
        {
            button.EmitSignal(BaseButton.SignalName.Pressed);
        }
        else
        {
            GD.PushError($"[input-script] no button named {name}");
        }
    }

    /// <summary>A point in the game's 1920 x 1080 base space, converted to the window's pixels, where input events are measured.</summary>
    private Vector2 Position(string x, string y)
    {
        var scale = DisplayServer.WindowGetSize().X / GetViewport().GetVisibleRect().Size.X;
        return new Vector2(float.Parse(x, CultureInfo.InvariantCulture), float.Parse(y, CultureInfo.InvariantCulture)) * scale;
    }

    private static MouseButton ParseButton(string name) => name switch
    {
        "right" => MouseButton.Right,
        "middle" => MouseButton.Middle,
        _ => MouseButton.Left,
    };

    /// <summary>The modifiers a mouse command names after its coordinates, together with the ones held with keydown.</summary>
    private Modifiers HeldModifiers(string[] parts, int from)
    {
        var named = new HashSet<string>();
        for (var i = from; i < parts.Length; i++)
        {
            named.Add(parts[i]);
        }

        return new Modifiers(
            _shift || named.Contains("shift"),
            _ctrl || named.Contains("ctrl"),
            _alt || named.Contains("alt"),
            _meta || named.Contains("meta"));
    }

    private static Key ParseKey(string name) => name switch
    {
        "ctrl" => Key.Ctrl,
        "shift" => Key.Shift,
        "alt" => Key.Alt,
        "meta" => Key.Meta,
        _ => OS.FindKeycodeFromString(name),
    };

    private static void Mouse(MouseButton button, bool pressed, Vector2 at, Modifiers mods)
    {
        Input.ParseInputEvent(new InputEventMouseButton
        {
            ButtonIndex = button,
            Pressed = pressed,
            Position = at,
            GlobalPosition = at,
            ShiftPressed = mods.Shift,
            CtrlPressed = mods.Ctrl,
            AltPressed = mods.Alt,
            MetaPressed = mods.Meta,
        });
    }

    /// <summary>A key press or release with the modifiers held at the time.</summary>
    private void SendKey(Key key, bool pressed)
    {
        Input.ParseInputEvent(new InputEventKey
        {
            Keycode = key,
            PhysicalKeycode = key,
            Pressed = pressed,
            ShiftPressed = _shift,
            CtrlPressed = _ctrl,
            AltPressed = _alt,
            MetaPressed = _meta,
        });
    }

    /// <summary>Holds or releases a key. A modifier key also changes the modifiers of the events that follow it.</summary>
    private void HoldKey(Key key, bool pressed)
    {
        switch (key)
        {
            case Key.Shift:
                _shift = pressed;
                break;
            case Key.Ctrl:
                _ctrl = pressed;
                break;
            case Key.Alt:
                _alt = pressed;
                break;
            case Key.Meta:
                _meta = pressed;
                break;
        }

        SendKey(key, pressed);
    }

    private readonly record struct Modifiers(bool Shift, bool Ctrl, bool Alt, bool Meta);
}
