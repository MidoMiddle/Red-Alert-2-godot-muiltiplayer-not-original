using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Godot;

namespace Rts.Client.App;

/// <summary>
/// Debug harness: plays a script of mouse and key events into the running game, so the controls can be checked without a person
/// at the keyboard. Started with <c>--input-script &lt;path&gt;</c> after <c>--</c>. One command per line; empty lines and lines that
/// start with <c>#</c> are skipped: <c>wait N</c>, <c>node NAME</c> (presses a button), <c>click|down|up BUTTON X Y [shift]</c>
/// (BUTTON is left, right or middle), <c>move X Y</c>, <c>key NAME</c> (a Godot key name), <c>shot PATH</c> (saves a PNG),
/// <c>log TEXT</c>. X and Y are in the game's 1920 x 1080 base space, whatever the window size is. The run ends with exit code 0
/// after the last line.
/// </summary>
public partial class InputScript : Node
{
    private readonly Queue<string> _lines = new();
    private int _waitFrames;

    /// <summary>Adds a script player under <paramref name="owner"/> if <c>--input-script</c> asks for one.</summary>
    public static void AttachIfRequested(Node owner)
    {
        var args = OS.GetCmdlineUserArgs();
        var index = Array.IndexOf(args, "--input-script");
        if (index < 0 || index + 1 >= args.Length)
        {
            return;
        }

        var script = new InputScript();
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
                    var shift = parts.Length > 4 && parts[4] == "shift";
                    Mouse(button, true, at, shift);
                    Mouse(button, false, at, shift);
                    break;
                }
            case "down":
                Mouse(ParseButton(parts[1]), true, Position(parts[2], parts[3]), parts.Length > 4 && parts[4] == "shift");
                break;
            case "up":
                Mouse(ParseButton(parts[1]), false, Position(parts[2], parts[3]), parts.Length > 4 && parts[4] == "shift");
                break;
            case "move":
                {
                    var to = Position(parts[1], parts[2]);
                    Input.ParseInputEvent(new InputEventMouseMotion { Position = to, GlobalPosition = to });
                    break;
                }
            case "key":
                {
                    var key = OS.FindKeycodeFromString(parts[1]);
                    SendKey(key, true);
                    SendKey(key, false);
                    break;
                }
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

    private static void Mouse(MouseButton button, bool pressed, Vector2 at, bool shift)
    {
        Input.ParseInputEvent(new InputEventMouseButton
        {
            ButtonIndex = button,
            Pressed = pressed,
            Position = at,
            GlobalPosition = at,
            ShiftPressed = shift,
        });
    }

    private static void SendKey(Key key, bool pressed)
    {
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = pressed });
    }
}
