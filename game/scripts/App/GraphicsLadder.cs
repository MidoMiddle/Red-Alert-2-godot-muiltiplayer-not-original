using System;
using System.Collections.Generic;

namespace Rts.Client.App;

/// <summary>The settings of the 3D a graphics test chooses among: its scale, the smoothing of its edges, the shadows, the SSAO.</summary>
public readonly record struct GraphicsLevel(int RenderScale, AntiAliasing AntiAliasing, ShadowQuality Shadows, bool Ssao)
{
    /// <summary><paramref name="values"/> with the graphics of this level; the window, the frame limit and the rest as they were.</summary>
    public SettingsValues ApplyTo(SettingsValues values) =>
        values with { RenderScale = RenderScale, AntiAliasing = AntiAliasing, Shadows = Shadows, Ssao = Ssao };

    /// <summary>The level of <paramref name="values"/>.</summary>
    public static GraphicsLevel Of(SettingsValues values) => new(values.RenderScale, values.AntiAliasing, values.Shadows, values.Ssao);
}

/// <summary>
/// The levels of graphics the test of the Graphics tab chooses among (the owner, 2026-10-28: «the game picks the settings for 60 FPS»),
/// from the finest to the lightest: each costs the graphics card no more than the one before it — what goes first is what costs the
/// most for what it shows (the SSAO, the soft shadows, MSAA 4×), then the 3D is drawn smaller. The defaults of a new game
/// (<see cref="SettingsValues.Defaults"/>) are one of them.
/// </summary>
public static class GraphicsLadder
{
    /// <summary>
    /// The time a frame may take the graphics card, in milliseconds: 60 FPS is 16.7 ms, and the general battle the test draws is not the
    /// heaviest moment of every match — a quarter is kept in reserve.
    /// </summary>
    public const double TargetMs = 12.5;

    public static IReadOnlyList<GraphicsLevel> Levels { get; } =
    [
        new(SettingsValues.FullRenderScale, AntiAliasing.Msaa4, ShadowQuality.High, true),
        new(SettingsValues.FullRenderScale, AntiAliasing.Msaa4, ShadowQuality.High, false),
        new(SettingsValues.FullRenderScale, AntiAliasing.Msaa2, ShadowQuality.Medium, false),
        new(SettingsValues.FullRenderScale, AntiAliasing.Msaa2, ShadowQuality.Low, false),
        new(85, AntiAliasing.Msaa2, ShadowQuality.Low, false),
        new(75, AntiAliasing.Msaa2, ShadowQuality.Low, false),
        new(67, AntiAliasing.Fxaa, ShadowQuality.Low, false),
        new(50, AntiAliasing.Fxaa, ShadowQuality.Low, false),
        new(50, AntiAliasing.Off, ShadowQuality.Off, false),
    ];
}

/// <summary>
/// The search of the graphics test: the finest level of <see cref="GraphicsLadder.Levels"/> whose frame takes the graphics card no more
/// than the target, by halving — four measurements of nine levels. <see cref="Next"/> is the level to measure, <see cref="Report"/> its
/// time; once <see cref="Next"/> is null, <see cref="Result"/> is the level, and <see cref="Fits"/> whether even it is within the target
/// (else it is the lightest). Where only the time of the whole frame is known — the processor's share in it — <see cref="Floor"/> raises
/// the target over what the lightest level takes: graphics that cost no more than that are not what holds the game back.
/// </summary>
public sealed class GraphicsSearch
{
    /// <summary>How much more than the lightest level a level's whole frame may take, in percent, and still count as within reach.</summary>
    public const int FloorPercent = 115;

    private double _targetMs;
    private int _low;
    private int _high;

    public GraphicsSearch(double targetMs = GraphicsLadder.TargetMs)
    {
        _targetMs = targetMs;
        _high = GraphicsLadder.Levels.Count;
    }

    /// <summary>The time a level may take; raised by <see cref="Floor"/>.</summary>
    public double TargetMs => _targetMs;

    /// <summary>
    /// The whole frame of the lightest level took <paramref name="milliseconds"/>: a level within <see cref="FloorPercent"/> of it is
    /// within reach, if that is above the target.
    /// </summary>
    public void Floor(double milliseconds) => _targetMs = Math.Max(_targetMs, milliseconds * FloorPercent / 100);

    /// <summary>The level to measure next; null once the search is over.</summary>
    public int? Next => _low < _high ? (_low + _high) / 2 : null;

    /// <summary>How long a frame of <see cref="Next"/> took the graphics card, in milliseconds.</summary>
    public void Report(double milliseconds)
    {
        var level = Next ?? throw new InvalidOperationException("the search is over");
        if (milliseconds <= _targetMs)
        {
            _high = level;
        }
        else
        {
            _low = level + 1;
        }
    }

    /// <summary>The level chosen: the finest within the target, or the lightest when none is.</summary>
    public int Result => Math.Min(_low, GraphicsLadder.Levels.Count - 1);

    /// <summary>Whether <see cref="Result"/> is within the target.</summary>
    public bool Fits => _low < GraphicsLadder.Levels.Count;
}
