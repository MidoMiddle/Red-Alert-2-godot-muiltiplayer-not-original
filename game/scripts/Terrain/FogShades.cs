using System;
using Rts.Simulation.Visibility;

namespace Rts.Client.Terrain;

/// <summary>
/// The shade of a cell in the fog texture (docs/implementation/M4_combat.md, 4.15, step 12): 0 shroud, 128 fog, 255 visible, and how
/// a shade follows its target — all the way in <see cref="Seconds"/>, at an even pace, so the fog opens instead of snapping. Pure,
/// tested in Rts.Simulation.Tests.
/// </summary>
public static class FogShades
{
    public const byte Shroud = 0;
    public const byte Fog = 128;
    public const byte Visible = 255;

    /// <summary>The time a shade takes from shroud to visible.</summary>
    public const float Seconds = 0.25f;

    /// <summary>The shade a cell heads for: everything is visible while the fog is revealed.</summary>
    public static byte Target(CellVisibility state, bool revealed = false) =>
        revealed ? Visible : state switch
        {
            CellVisibility.Visible => Visible,
            CellVisibility.Fog => Fog,
            _ => Shroud,
        };

    /// <summary>
    /// The opacity of the black over a cell of the minimap: all of it on the shroud, <paramref name="fog"/> on the fog, none where it is
    /// seen, in between on the way.
    /// </summary>
    public static byte MinimapAlpha(byte shade, int fog) =>
        shade >= Fog
            ? (byte)((Visible - shade) * fog / (Visible - Fog))
            : (byte)(Visible - (shade * (Visible - fog) / Fog));

    /// <summary>The shade after <paramref name="delta"/> seconds on the way from <paramref name="current"/> to <paramref name="target"/>.</summary>
    public static byte Step(byte current, byte target, double delta)
    {
        var reach = (int)Math.Ceiling(Visible * delta / Seconds);
        return current < target ? (byte)Math.Min(target, current + reach) : (byte)Math.Max(target, current - reach);
    }
}
