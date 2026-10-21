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
/// uses is swapped with that action's key. Escape, the interface keys and the modifiers cannot be bound.
/// </summary>
public sealed class KeyBindings
{
    private const string KeyPrefix = "keys/";

    public static IReadOnlyList<KeyAction> Actions { get; } = new[]
    {
        new KeyAction("camera_up", "UI_KEY_CAMERA_UP", "W"),
        new KeyAction("camera_down", "UI_KEY_CAMERA_DOWN", "S"),
        new KeyAction("camera_left", "UI_KEY_CAMERA_LEFT", "A"),
        new KeyAction("camera_right", "UI_KEY_CAMERA_RIGHT", "D"),
        new KeyAction("pause", "UI_KEY_PAUSE", "P"),
        new KeyAction("step", "UI_KEY_STEP", "N"),
        new KeyAction("speed_up", "UI_KEY_SPEED_UP", "Equal"),
        new KeyAction("speed_down", "UI_KEY_SPEED_DOWN", "Minus"),
        new KeyAction("save_replay", "UI_KEY_SAVE_REPLAY", "F5"),
        new KeyAction("load_replay", "UI_KEY_LOAD_REPLAY", "F9"),
        new KeyAction("new_session", "UI_KEY_NEW_SESSION", "R"),
        new KeyAction("spawn_type", "UI_KEY_SPAWN_TYPE", "T"),
        new KeyAction("cancel_selection", "UI_KEY_CANCEL_SELECTION", "Escape"),
    };

    /// <summary>Keys that cannot be bound: Escape stops the wait for a key, the others are used by the interface.</summary>
    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "Escape", "Enter", "KP Enter", "Space", "Tab",
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

    /// <summary>The keys as file contents, keyed by <c>keys/action</c>. Every action is written.</summary>
    public IReadOnlyDictionary<string, string> ToRaw()
    {
        var raw = new Dictionary<string, string>();
        foreach (var action in Actions)
        {
            raw[KeyPrefix + action.Id] = _keys[action.Id];
        }

        return raw;
    }

    /// <summary>
    /// Reads the keys from the file contents. A missing, unknown, refused or unknown-to-the-game key gets its default. A key
    /// that an earlier action already uses falls back to the default too, so no two actions share a key from the file.
    /// </summary>
    public static KeyBindings Parse(IReadOnlyDictionary<string, string> raw, Func<string, bool> isKnownKey)
    {
        var bindings = new KeyBindings();
        foreach (var action in Actions)
        {
            if (raw.TryGetValue(KeyPrefix + action.Id, out var key) && !IsRefused(key) && isKnownKey(key))
            {
                bindings._keys[action.Id] = key;
            }
        }

        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var action in Actions)
        {
            if (!used.Add(bindings._keys[action.Id]))
            {
                bindings._keys[action.Id] = action.DefaultKey;
            }
        }

        return bindings;
    }

    private static bool IsRefused(string key) =>
        string.IsNullOrWhiteSpace(key) || Reserved.Contains(key) || Modifiers.Contains(key);

    private static bool SameKey(string first, string second) =>
        string.Equals(first, second, StringComparison.OrdinalIgnoreCase);
}
