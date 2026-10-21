using System;
using System.Collections.Generic;
using System.Globalization;
using Rts.Client.Controls;

namespace Rts.Client.App;

/// <summary>Window modes of the Graphics tab, in the order of the dropdown.</summary>
public enum WindowMode
{
    Windowed,
    Fullscreen,
    Borderless,
}

/// <summary>A window size in pixels.</summary>
public readonly record struct WindowSize(int Width, int Height);

/// <summary>
/// The player's settings (docs/implementation/M6_menus_skirmish.md, 4.5 and 4.6). No Godot types here: <c>AppSettings</c> reads
/// and writes the file and passes plain text through this class, so the rules are covered by unit tests. A missing or
/// invalid value gets its default, and an unknown key is ignored.
/// </summary>
public sealed record SettingsValues(
    WindowMode WindowMode,
    WindowSize Resolution,
    bool VSync,
    int FpsLimit,
    int InterfaceScale,
    int MasterVolume,
    ControlScheme Scheme,
    int ScrollSpeed,
    bool EdgeScroll,
    bool CameraDrag)
{
    /// <summary>Layout version of the file, written as <c>meta/format</c>.</summary>
    public const int FormatVersion = 1;

    /// <summary>Frame limit that means no limit.</summary>
    public const int Unlimited = 0;

    public const int MinInterfaceScale = 75;
    public const int MaxInterfaceScale = 200;
    public const int InterfaceScaleStep = 5;
    public const int MinMasterVolume = 0;
    public const int MaxMasterVolume = 100;
    public const int MinScrollSpeed = 1;
    public const int MaxScrollSpeed = 10;

    private const string FormatKey = "meta/format";
    private const string WindowModeKey = "graphics/window_mode";
    private const string ResolutionWidthKey = "graphics/resolution_width";
    private const string ResolutionHeightKey = "graphics/resolution_height";
    private const string VSyncKey = "graphics/vsync";
    private const string FpsLimitKey = "graphics/fps_limit";
    private const string InterfaceScaleKey = "graphics/interface_scale";
    private const string MasterVolumeKey = "audio/master_volume";
    private const string SchemeKey = "controls/scheme";
    private const string ScrollSpeedKey = "controls/scroll_speed";
    private const string EdgeScrollKey = "controls/edge_scroll";
    private const string CameraDragKey = "controls/camera_drag";

    /// <summary>Resolutions of the Graphics tab. This is the size of a windowed game; fullscreen uses the screen.</summary>
    public static WindowSize[] ResolutionChoices { get; } =
    {
        new(1280, 720),
        new(1920, 1080),
        new(2560, 1440),
    };

    /// <summary>Frame limits of the Graphics tab, in the order of the dropdown. <see cref="Unlimited"/> is the last one.</summary>
    public static int[] FpsChoices { get; } = { 30, 60, 120, 144, Unlimited };

    /// <summary>
    /// What the game uses without a settings file: windowed 1280 × 720, VSync, no frame limit, 100 % and full volume, Classic
    /// scheme, scroll speed 5, edge scrolling and camera drag on.
    /// </summary>
    public static SettingsValues Defaults { get; } = new(
        WindowMode.Windowed,
        new WindowSize(1280, 720),
        true,
        Unlimited,
        100,
        MaxMasterVolume,
        ControlScheme.Classic,
        5,
        true,
        true);

    /// <summary>Reads the values from the file contents, keyed by <c>section/key</c>.</summary>
    public static SettingsValues Parse(IReadOnlyDictionary<string, string> raw)
    {
        var fallback = Defaults;
        return new SettingsValues(
            ParseWindowMode(raw, fallback.WindowMode),
            ParseResolution(raw, fallback.Resolution),
            ParseBool(raw, VSyncKey, fallback.VSync),
            ParseFpsLimit(raw, fallback.FpsLimit),
            ParseInterfaceScale(raw, fallback.InterfaceScale),
            Math.Clamp(ParseInteger(raw, MasterVolumeKey, fallback.MasterVolume), MinMasterVolume, MaxMasterVolume),
            ParseScheme(raw, fallback.Scheme),
            Math.Clamp(ParseInteger(raw, ScrollSpeedKey, fallback.ScrollSpeed), MinScrollSpeed, MaxScrollSpeed),
            ParseBool(raw, EdgeScrollKey, fallback.EdgeScroll),
            ParseBool(raw, CameraDragKey, fallback.CameraDrag));
    }

    /// <summary>The values as file contents, keyed by <c>section/key</c>. Every known key is written.</summary>
    public IReadOnlyDictionary<string, string> ToRaw()
    {
        return new Dictionary<string, string>
        {
            [FormatKey] = FormatVersion.ToString(CultureInfo.InvariantCulture),
            [WindowModeKey] = WindowMode.ToString().ToLowerInvariant(),
            [ResolutionWidthKey] = Resolution.Width.ToString(CultureInfo.InvariantCulture),
            [ResolutionHeightKey] = Resolution.Height.ToString(CultureInfo.InvariantCulture),
            [VSyncKey] = VSync ? "true" : "false",
            [FpsLimitKey] = FpsLimit.ToString(CultureInfo.InvariantCulture),
            [InterfaceScaleKey] = InterfaceScale.ToString(CultureInfo.InvariantCulture),
            [MasterVolumeKey] = MasterVolume.ToString(CultureInfo.InvariantCulture),
            [SchemeKey] = Scheme.ToString().ToLowerInvariant(),
            [ScrollSpeedKey] = ScrollSpeed.ToString(CultureInfo.InvariantCulture),
            [EdgeScrollKey] = EdgeScroll ? "true" : "false",
            [CameraDragKey] = CameraDrag ? "true" : "false",
        };
    }

    private static WindowMode ParseWindowMode(IReadOnlyDictionary<string, string> raw, WindowMode fallback)
    {
        return Enum.TryParse(Text(raw, WindowModeKey), ignoreCase: true, out WindowMode mode) && Enum.IsDefined(mode)
            ? mode
            : fallback;
    }

    private static ControlScheme ParseScheme(IReadOnlyDictionary<string, string> raw, ControlScheme fallback)
    {
        return Enum.TryParse(Text(raw, SchemeKey), ignoreCase: true, out ControlScheme scheme) && Enum.IsDefined(scheme)
            ? scheme
            : fallback;
    }

    private static WindowSize ParseResolution(IReadOnlyDictionary<string, string> raw, WindowSize fallback)
    {
        var size = new WindowSize(
            ParseInteger(raw, ResolutionWidthKey, fallback.Width),
            ParseInteger(raw, ResolutionHeightKey, fallback.Height));
        return Array.IndexOf(ResolutionChoices, size) >= 0 ? size : fallback;
    }

    private static int ParseFpsLimit(IReadOnlyDictionary<string, string> raw, int fallback)
    {
        var fps = ParseInteger(raw, FpsLimitKey, fallback);
        return Array.IndexOf(FpsChoices, fps) >= 0 ? fps : fallback;
    }

    /// <summary>Rounds to the nearest step, then keeps the range: the slider moves in steps of 5.</summary>
    private static int ParseInterfaceScale(IReadOnlyDictionary<string, string> raw, int fallback)
    {
        var percent = ParseInteger(raw, InterfaceScaleKey, fallback);
        var rounded = (percent + InterfaceScaleStep / 2) / InterfaceScaleStep * InterfaceScaleStep;
        return Math.Clamp(rounded, MinInterfaceScale, MaxInterfaceScale);
    }

    private static bool ParseBool(IReadOnlyDictionary<string, string> raw, string key, bool fallback) =>
        bool.TryParse(Text(raw, key), out var value) ? value : fallback;

    private static int ParseInteger(IReadOnlyDictionary<string, string> raw, string key, int fallback) =>
        int.TryParse(Text(raw, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;

    /// <summary>The text stored under a key, or null if the file has no such key.</summary>
    private static string? Text(IReadOnlyDictionary<string, string> raw, string key) =>
        raw.TryGetValue(key, out var text) ? text : null;
}
