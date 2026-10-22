using System;
using System.Collections.Generic;

namespace Rts.Client.Controls;

/// <summary>A game action that has a key. Its title is a localization key; its default key is a Godot key name.</summary>
public sealed record KeyAction(string Id, string TitleKey, string DefaultKey);

/// <summary>How a new key went in: it was set, it took the key of another action, or it was refused.</summary>
public enum RebindStatus
{
    Refused,
    Set,
    Swapped,
}

/// <summary>The outcome of <see cref="KeyBindings.Rebind"/>. When swapped, <see cref="OtherAction"/> got the old key.</summary>
public sealed record RebindResult(RebindStatus Status, string? OtherAction = null, string? OtherKey = null);

/// <summary>
/// The keys of the game actions (docs/implementation/M6_menus_skirmish.md, 4.6). Every action has one key that the player can
/// change. Keys are Godot key names ("W", "F5", "Equal"), and the game maps them to its input map. A new key that another action
/// uses is swapped with that action's key. Escape, the interface keys, the digits (control groups) and the modifiers cannot be
/// bound. The layout is the RA2 one (docs/implementation/M2_unit_control.md, 4.14): the camera on the arrows, S / X / Z for
/// orders, T selects units of a type, B spawns a test unit, G guards and D deploys (both wait for M4 and M3).
/// </summary>
public sealed class KeyBindings
{
    private const string KeyPrefix = "keys/";

    /// <summary>Marks a file written with the layout of this step. Files without it keep their keys, but not the old ones (see <see cref="OldCameraKeys"/>).</summary>
    private const string LayoutKey = KeyPrefix + "layout";

    private const string Layout = "3";

    /// <summary>The camera keys of the M6 layout. A file without the layout mark that still has them gets the arrows.</summary>
    private static readonly Dictionary<string, string> OldCameraKeys = new(StringComparer.Ordinal)
    {
        ["camera_up"] = "W",
        ["camera_down"] = "S",
        ["camera_left"] = "A",
        ["camera_right"] = "D",
    };

    public static IReadOnlyList<KeyAction> Actions { get; } = new[]
    {
        new KeyAction("camera_up", "UI_KEY_CAMERA_UP", "Up"),
        new KeyAction("camera_down", "UI_KEY_CAMERA_DOWN", "Down"),
        new KeyAction("camera_left", "UI_KEY_CAMERA_LEFT", "Left"),
        new KeyAction("camera_right", "UI_KEY_CAMERA_RIGHT", "Right"),
        new KeyAction("pause", "UI_KEY_PAUSE", "P"),
        new KeyAction("step", "UI_KEY_STEP", "N"),
        new KeyAction("speed_up", "UI_KEY_SPEED_UP", "Equal"),
        new KeyAction("speed_down", "UI_KEY_SPEED_DOWN", "Minus"),
        new KeyAction("save_replay", "UI_KEY_SAVE_REPLAY", "F5"),
        new KeyAction("load_replay", "UI_KEY_LOAD_REPLAY", "F9"),
        new KeyAction("new_session", "UI_KEY_NEW_SESSION", "R"),
        new KeyAction("type_select", "UI_KEY_TYPE_SELECT", "T"),
        new KeyAction("spawn", "UI_KEY_SPAWN", "B"),
        new KeyAction("guard", "UI_KEY_GUARD", "G"),
        new KeyAction("deploy", "UI_KEY_DEPLOY", "D"),
        new KeyAction("debug_path", "UI_KEY_DEBUG_PATH", "F6"),
        new KeyAction("debug_cells", "UI_KEY_DEBUG_CELLS", "F7"),
        new KeyAction("debug_queue", "UI_KEY_DEBUG_QUEUE", "F8"),
        new KeyAction("select_all", "UI_KEY_SELECT_ALL", "E"),
        new KeyAction("stop", "UI_KEY_STOP", "S"),
        new KeyAction("scatter", "UI_KEY_SCATTER", "X"),
        new KeyAction("waypoint_mode", "UI_KEY_WAYPOINT_MODE", "Z"),
        new KeyAction("debug_damage", "UI_KEY_DEBUG_DAMAGE", "K"),
        new KeyAction("debug_build", "UI_KEY_DEBUG_BUILD", "V"),
        new KeyAction("cancel_selection", "UI_KEY_CANCEL_SELECTION", "Escape"),
    };

    /// <summary>
    /// Keys that cannot be bound: Escape stops the wait for a key, the digits select control groups, F1…F4 are the camera
    /// bookmarks (M2 step 14), the others are used by the interface.
    /// </summary>
    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "Escape", "Enter", "KP Enter", "Space", "Tab", "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "F1", "F2", "F3", "F4",
    };

    /// <summary>Modifier keys are held together with other keys; they are not bindings of their own.</summary>
    private static readonly HashSet<string> Modifiers = new(StringComparer.OrdinalIgnoreCase)
    {
        "Shift", "Ctrl", "Control", "Alt", "Meta", "Command",
    };

    private readonly Dictionary<string, string> _keys = new(StringComparer.Ordinal);

    public KeyBindings()
    {
        Reset();
    }

    /// <summary>The key of the action; empty when the action has none (see <see cref="Parse"/>).</summary>
    public string KeyOf(string actionId) => _keys[actionId];

    /// <summary>Restores the default key of every action.</summary>
    public void Reset()
    {
        _keys.Clear();
        foreach (var action in Actions)
        {
            _keys[action.Id] = action.DefaultKey;
        }
    }

    /// <summary>
    /// Gives an action a new key. If another action uses that key, it gets the old key of this action (a swap).
    /// Refused keys leave everything as it was.
    /// </summary>
    public RebindResult Rebind(string actionId, string key)
    {
        if (!_keys.ContainsKey(actionId) || IsRefused(key))
        {
            return new RebindResult(RebindStatus.Refused);
        }

        var previous = _keys[actionId];
        string? other = null;
        foreach (var pair in _keys)
        {
            if (pair.Key != actionId && SameKey(pair.Value, key))
            {
                other = pair.Key;
                break;
            }
        }

        _keys[actionId] = key;
        if (other is null)
        {
            return new RebindResult(RebindStatus.Set);
        }

        _keys[other] = previous;
        return new RebindResult(RebindStatus.Swapped, other, previous);
    }

    /// <summary>The keys as file contents, keyed by <c>keys/action</c>, with the layout mark. Every action is written.</summary>
    public IReadOnlyDictionary<string, string> ToRaw()
    {
        var raw = new Dictionary<string, string> { [LayoutKey] = Layout };
        foreach (var action in Actions)
        {
            raw[KeyPrefix + action.Id] = _keys[action.Id];
        }

        return raw;
    }

    /// <summary>
    /// Reads the keys from the file contents. A missing, unknown, refused or unknown-to-the-game key gets its default. A file
    /// without the current layout mark keeps its own keys, except the old W / S / A / D camera keys, which give way to the arrows.
    /// A key of an action that no longer exists (spawn_type, before the layout 3) is ignored, so T goes to type select. Keys
    /// from the file come first: a file key that an earlier action already has falls back to the default, and a default that
    /// is already taken leaves the action without a key (empty), so no two actions ever share one.
    /// </summary>
    public static KeyBindings Parse(IReadOnlyDictionary<string, string> raw, Func<string, bool> isKnownKey)
    {
        var bindings = new KeyBindings();
        var oldLayout = !raw.TryGetValue(LayoutKey, out var layout) || layout != Layout;
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var fromFile = new HashSet<string>(StringComparer.Ordinal);
        foreach (var action in Actions)
        {
            if (raw.TryGetValue(KeyPrefix + action.Id, out var key) && !IsRefused(key) && isKnownKey(key)
                && !(oldLayout && OldCameraKeys.TryGetValue(action.Id, out var oldKey) && SameKey(oldKey, key))
                && used.Add(key))
            {
                bindings._keys[action.Id] = key;
                fromFile.Add(action.Id);
            }
        }

        foreach (var action in Actions)
        {
            if (!fromFile.Contains(action.Id))
            {
                bindings._keys[action.Id] = used.Add(action.DefaultKey) ? action.DefaultKey : string.Empty;
            }
        }

        return bindings;
    }

    /// <summary>The actions left without a key by <see cref="Parse"/>.</summary>
    public IEnumerable<string> Unbound()
    {
        foreach (var action in Actions)
        {
            if (_keys[action.Id].Length == 0)
            {
                yield return action.Id;
            }
        }
    }

    private static bool IsRefused(string key) =>
        string.IsNullOrWhiteSpace(key) || Reserved.Contains(key) || Modifiers.Contains(key);

    private static bool SameKey(string first, string second) =>
        string.Equals(first, second, StringComparison.OrdinalIgnoreCase);
}
