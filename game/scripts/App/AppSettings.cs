using System;
using System.Collections.Generic;
using Godot;
using Rts.Client.Controls;

namespace Rts.Client.App;

/// <summary>
/// The player's settings (docs/implementation/M6_menus_skirmish.md, 4.5 and 4.6). Reads <c>user://settings.cfg</c> when the game
/// starts and applies the values to the window, the engine, the audio and the input map. Each setter applies and saves at once.
/// A new window size is first only shown (<see cref="PreviewResolution"/>) and saved when the player keeps it
/// (<see cref="CommitResolution"/>). Registered as the <c>AppSettings</c> autoload.
/// </summary>
public partial class AppSettings : Node
{
    private const string FilePath = "user://settings.cfg";
    private const int MasterBus = 0;
    private const float SilenceDb = -80f;

    /// <summary>The running settings. Set when the autoload is ready.</summary>
    public static AppSettings? Instance { get; private set; }

    /// <summary>The current values. Change them only through the setters of this class.</summary>
    public SettingsValues Values { get; private set; } = SettingsValues.Defaults;

    /// <summary>The key of every game action. Change it only through <see cref="SetKey"/> and <see cref="ResetKeys"/>.</summary>
    public KeyBindings Keys { get; private set; } = new();

    public override void _Ready()
    {
        Instance = this;
        var raw = ReadFile();
        Values = SettingsValues.Parse(raw);
        Keys = KeyBindings.Parse(raw, IsKnownKey);
        foreach (var actionId in Keys.Unbound())
        {
            GD.PushWarning($"[settings] action {actionId} has no key: its default key is used by another action");
        }

        ApplyWindowMode(Values.WindowMode);
        ApplyResolution(Values.Resolution);
        ApplyVSync(Values.VSync);
        ApplyFpsLimit(Values.FpsLimit);
        ApplyInterfaceScale(Values.InterfaceScale);
        ApplyMasterVolume(Values.MasterVolume);
        InputMapper.Apply(Keys);
    }

    /// <summary>Changes the window mode. The window keeps the stored windowed size.</summary>
    public void SetWindowMode(WindowMode mode)
    {
        Values = Values with { WindowMode = mode };
        ApplyWindowMode(mode);
        ApplyResolution(Values.Resolution);
        Save();
    }

    public void SetVSync(bool enabled)
    {
        Values = Values with { VSync = enabled };
        ApplyVSync(enabled);
        Save();
    }

    public void SetFpsLimit(int fps)
    {
        Values = Values with { FpsLimit = fps };
        ApplyFpsLimit(fps);
        Save();
    }

    public void SetHealthBars(HealthBarMode mode)
    {
        Values = Values with { HealthBars = mode };
        Save();
    }

    public void SetInterfaceScale(int percent)
    {
        Values = Values with { InterfaceScale = percent };
        ApplyInterfaceScale(percent);
        Save();
    }

    public void SetMasterVolume(int percent)
    {
        Values = Values with { MasterVolume = percent };
        ApplyMasterVolume(percent);
        Save();
    }

    public void SetScheme(ControlScheme scheme)
    {
        Values = Values with { Scheme = scheme };
        Save();
    }

    public void SetScrollSpeed(int speed)
    {
        Values = Values with { ScrollSpeed = speed };
        Save();
    }

    public void SetEdgeScroll(bool enabled)
    {
        Values = Values with { EdgeScroll = enabled };
        Save();
    }

    public void SetCameraDrag(bool enabled)
    {
        Values = Values with { CameraDrag = enabled };
        Save();
    }

    /// <summary>Shows a window size without saving it. The settings screen uses it while the player decides.</summary>
    public void PreviewResolution(WindowSize size) => ApplyResolution(size);

    /// <summary>Keeps a window size: shows it and saves it.</summary>
    public void CommitResolution(WindowSize size)
    {
        Values = Values with { Resolution = size };
        ApplyResolution(size);
        Save();
    }

    /// <summary>
    /// Binds a key to an action. An action that had the key gets the old key of this one (see <see cref="KeyBindings.Rebind"/>).
    /// A refused key changes nothing.
    /// </summary>
    public RebindResult SetKey(string actionId, string key)
    {
        var result = Keys.Rebind(actionId, key);
        if (result.Status != RebindStatus.Refused)
        {
            InputMapper.Apply(Keys);
            Save();
        }

        return result;
    }

    /// <summary>Restores the default key of every action.</summary>
    public void ResetKeys()
    {
        Keys.Reset();
        InputMapper.Apply(Keys);
        Save();
    }

    /// <summary>Whether the game knows a key name, so a binding can be made from it.</summary>
    public static bool IsKnownKey(string name) => OS.FindKeycodeFromString(name) != Key.None;

    private static void ApplyWindowMode(WindowMode mode)
    {
        DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, mode == WindowMode.Borderless);
        DisplayServer.WindowSetMode(mode == WindowMode.Fullscreen
            ? DisplayServer.WindowMode.Fullscreen
            : DisplayServer.WindowMode.Windowed);
    }

    /// <summary>Sizes the window and centres it. Fullscreen uses the screen's size, so there is nothing to set.</summary>
    private void ApplyResolution(WindowSize size)
    {
        if (Values.WindowMode == WindowMode.Fullscreen)
        {
            return;
        }

        var target = new Vector2I(size.Width, size.Height);
        DisplayServer.WindowSetSize(target);
        var free = DisplayServer.ScreenGetSize() - target;
        DisplayServer.WindowSetPosition(new Vector2I(Math.Max(0, free.X / 2), Math.Max(0, free.Y / 2)));
    }

    private static void ApplyVSync(bool enabled)
    {
        DisplayServer.WindowSetVsyncMode(enabled ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled);
    }

    private static void ApplyFpsLimit(int fps)
    {
        Engine.MaxFps = fps;
    }

    private void ApplyInterfaceScale(int percent)
    {
        GetTree().Root.ContentScaleFactor = percent / 100f;
    }

    private static void ApplyMasterVolume(int percent)
    {
        AudioServer.SetBusVolumeDb(MasterBus, percent == 0 ? SilenceDb : Mathf.LinearToDb(percent / 100f));
    }

    /// <summary>The file contents keyed by <c>section/key</c>. A missing file is normal on the first start; a broken one is reported and ignored.</summary>
    private static Dictionary<string, string> ReadFile()
    {
        var raw = new Dictionary<string, string>();
        var config = new ConfigFile();
        var error = config.Load(FilePath);
        if (error == Error.FileNotFound)
        {
            return raw;
        }

        if (error != Error.Ok)
        {
            GD.PushWarning($"[settings] cannot read {FilePath}, defaults are used: {error}");
            return raw;
        }

        foreach (var section in config.GetSections())
        {
            foreach (var key in config.GetSectionKeys(section))
            {
                raw[$"{section}/{key}"] = config.GetValue(section, key).ToString();
            }
        }

        return raw;
    }

    /// <summary>Writes every known value and key. Unknown keys of the file are not kept.</summary>
    private void Save()
    {
        var all = new Dictionary<string, string>();
        foreach (var pair in Values.ToRaw())
        {
            all[pair.Key] = pair.Value;
        }

        foreach (var pair in Keys.ToRaw())
        {
            all[pair.Key] = pair.Value;
        }

        var config = new ConfigFile();
        foreach (var pair in all)
        {
            var separator = pair.Key.IndexOf('/');
            config.SetValue(pair.Key[..separator], pair.Key[(separator + 1)..], pair.Value);
        }

        var error = config.Save(FilePath);
        if (error != Error.Ok)
        {
            GD.PushError($"[settings] cannot save {FilePath}: {error}");
        }
    }
}
