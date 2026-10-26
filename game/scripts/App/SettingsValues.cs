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

/// <summary>
/// Whose health bars show (M4 step 13), in the order of the dropdown: everybody's, the selected and the damaged (the default, decision 4
/// of the M4 plan), or the selected only.
/// </summary>
public enum HealthBarMode
{
    Always,
    Damaged,
    Selected,
}

/// <summary>
/// The smoothing of the edges of the 3D, in the order of the dropdown (the owner, 2026-10-26): none, FXAA — a pass over the picture,
/// cheap and a little blurred — or multisampling of 2 or 4 samples a pixel, the default as before.
/// </summary>
public enum AntiAliasing
{
    Off,
    Fxaa,
    Msaa2,
    Msaa4,
}

/// <summary>
/// The shadows of the sun, in the order of the dropdown (the owner, 2026-10-26: lighter on the graphics card): none; low — one hard
/// shadow map; medium — two, a little soft; high — two, soft, as the game had them (the default).
/// </summary>
public enum ShadowQuality
{
    Off,
    Low,
    Medium,
    High,
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
    bool CameraDrag,
    HealthBarMode HealthBars = HealthBarMode.Selected,
    bool PlaytestSurvey = false,
    bool Ra2Names = true,
    int RenderScale = SettingsValues.FullRenderScale,
    AntiAliasing AntiAliasing = AntiAliasing.Msaa4,
    ShadowQuality Shadows = ShadowQuality.High,
    bool Ssao = true)
{
    /// <summary>
    /// Layout version of the file, written as <c>meta/format</c>. 2 (2026-10-26): the frame limit's default is 60; a file of format 1
    /// with no limit had it by default, and gets 60.
    /// </summary>
    public const int FormatVersion = 2;

    /// <summary>The frame limit of a new game (the owner, 2026-10-26: no limit heated the computer).</summary>
    public const int DefaultFpsLimit = 60;

    /// <summary>The 3D drawn at the size of the window: 100 %.</summary>
    public const int FullRenderScale = 100;

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
    private const string RenderScaleKey = "graphics/render_scale";
    private const string AntiAliasingKey = "graphics/anti_aliasing";
    private const string ShadowsKey = "graphics/shadows";
    private const string SsaoKey = "graphics/ssao";
    private const string MasterVolumeKey = "audio/master_volume";
    private const string SchemeKey = "controls/scheme";
    private const string ScrollSpeedKey = "controls/scroll_speed";
    private const string EdgeScrollKey = "controls/edge_scroll";
    private const string CameraDragKey = "controls/camera_drag";
    private const string HealthBarsKey = "interface/health_bars";
    private const string PlaytestSurveyKey = "interface/playtest_survey";
    private const string Ra2NamesKey = "interface/ra2_names";

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
    /// The scales of the 3D of the Graphics tab, in percent of the window's pixels, in the order of the dropdown (the owner, 2026-10-26: a
    /// Retina screen draws some 6 million pixels a frame). The interface stays sharp: only the 3D is drawn smaller and stretched.
    /// </summary>
    public static int[] RenderScaleChoices { get; } = { FullRenderScale, 85, 75, 67, 50 };

    /// <summary>
    /// What the game uses without a settings file: windowed 1280 × 720, VSync, 60 frames a second at most, 100 % and full volume, Classic
    /// scheme, scroll speed 5, edge scrolling and camera drag on, health bars of the selected and the damaged, no playtest survey (M8 step 14),
    /// the names of RA2 on screen (while there are no models).
    /// </summary>
    public static SettingsValues Defaults { get; } = new(
        WindowMode.Windowed,
        new WindowSize(1280, 720),
        true,
        DefaultFpsLimit,
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
            ParseBool(raw, CameraDragKey, fallback.CameraDrag),
            ParseHealthBars(raw, fallback.HealthBars),
            ParseBool(raw, PlaytestSurveyKey, fallback.PlaytestSurvey),
            ParseBool(raw, Ra2NamesKey, fallback.Ra2Names),
            ParseRenderScale(raw, fallback.RenderScale),
            ParseAntiAliasing(raw, fallback.AntiAliasing),
            ParseShadows(raw, fallback.Shadows),
            ParseBool(raw, SsaoKey, fallback.Ssao));
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
            [HealthBarsKey] = HealthBars.ToString().ToLowerInvariant(),
            [PlaytestSurveyKey] = PlaytestSurvey ? "true" : "false",
            [Ra2NamesKey] = Ra2Names ? "true" : "false",
            [RenderScaleKey] = RenderScale.ToString(CultureInfo.InvariantCulture),
            [AntiAliasingKey] = AntiAliasing.ToString().ToLowerInvariant(),
            [ShadowsKey] = Shadows.ToString().ToLowerInvariant(),
            [SsaoKey] = Ssao ? "true" : "false",
        };
    }

    private static WindowMode ParseWindowMode(IReadOnlyDictionary<string, string> raw, WindowMode fallback)
    {
        return Enum.TryParse(Text(raw, WindowModeKey), ignoreCase: true, out WindowMode mode) && Enum.IsDefined(mode)
            ? mode
            : fallback;
    }

    private static HealthBarMode ParseHealthBars(IReadOnlyDictionary<string, string> raw, HealthBarMode fallback)
    {
        return Enum.TryParse(Text(raw, HealthBarsKey), ignoreCase: true, out HealthBarMode mode) && Enum.IsDefined(mode)
            ? mode
            : fallback;
    }

    private static AntiAliasing ParseAntiAliasing(IReadOnlyDictionary<string, string> raw, AntiAliasing fallback)
    {
        return Enum.TryParse(Text(raw, AntiAliasingKey), ignoreCase: true, out AntiAliasing mode) && Enum.IsDefined(mode)
            ? mode
            : fallback;
    }

    private static ShadowQuality ParseShadows(IReadOnlyDictionary<string, string> raw, ShadowQuality fallback)
    {
        return Enum.TryParse(Text(raw, ShadowsKey), ignoreCase: true, out ShadowQuality quality) && Enum.IsDefined(quality)
            ? quality
            : fallback;
    }

    private static int ParseRenderScale(IReadOnlyDictionary<string, string> raw, int fallback)
    {
        var percent = ParseInteger(raw, RenderScaleKey, fallback);
        return Array.IndexOf(RenderScaleChoices, percent) >= 0 ? percent : fallback;
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
        // A file of before format 2 wrote no limit by default: it gets the default of now.
        if (fps == Unlimited && ParseInteger(raw, FormatKey, FormatVersion) < 2)
        {
            return DefaultFpsLimit;
        }

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
