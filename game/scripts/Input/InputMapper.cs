using System.Collections.Generic;
using Godot;

namespace Rts.Client.Controls;

/// <summary>
/// Puts the key bindings into Godot's input map, so the game reads actions and not keys (docs/implementation/M6_menus_skirmish.md,
/// 4.6). The numpad keeps working next to the bound speed keys.
/// </summary>
public static class InputMapper
{
    private static readonly Dictionary<string, Key[]> FixedKeys = new()
    {
        ["speed_up"] = new[] { Key.Plus, Key.KpAdd },
        ["speed_down"] = new[] { Key.KpSubtract },
    };

    /// <summary>Replaces the events of every game action with its bound key and its fixed keys.</summary>
    public static void Apply(KeyBindings bindings)
    {
        foreach (var action in KeyBindings.Actions)
        {
            var name = new StringName(action.Id);
            if (!InputMap.HasAction(name))
            {
                InputMap.AddAction(name);
            }

            InputMap.ActionEraseEvents(name);
            AddKey(name, OS.FindKeycodeFromString(bindings.KeyOf(action.Id)));
            if (FixedKeys.TryGetValue(action.Id, out var fixedKeys))
            {
                foreach (var key in fixedKeys)
                {
                    AddKey(name, key);
                }
            }
        }
    }

    private static void AddKey(StringName action, Key key)
    {
        if (key == Key.None)
        {
            GD.PushWarning($"[input] no key for action {action}");
            return;
        }

        InputMap.ActionAddEvent(action, new InputEventKey { Keycode = key });
    }
}
