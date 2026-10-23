using System;

namespace Rts.Client.Audio;

/// <summary>
/// Where a positional sound stands on the screen (docs/implementation/M6_menus_skirmish.md, 4.8). The camera is orthographic, so
/// there is no 3D audio: a sound is placed by its point on the screen. Inside the screen it plays at full volume. Outside, it gets
/// quieter by <see cref="OffScreenDb"/> at <see cref="SilentScreens"/> screens away, and beyond that it is not played. Measured along
/// each axis from the edge of the screen, in screens of that axis. The pan is Godot's: a 2D sound is panned by its offset from
/// the centre of the screen, see <see cref="PanningStrength"/>.
/// </summary>
public static class ScreenPlacement
{
    /// <summary>How far outside the screen a sound is still played, in screens.</summary>
    public const float SilentScreens = 1.5f;

    /// <summary>The loss at the limit of <see cref="SilentScreens"/>; it falls off in a straight line from the edge.</summary>
    public const float OffScreenDb = -18f;

    /// <summary>Whether the sound plays, and how much quieter it is than at full volume.</summary>
    public readonly record struct Placement(bool Audible, float GainDb);

    /// <summary>The placement of a point at (<paramref name="x"/>, <paramref name="y"/>) on a screen of the given size.</summary>
    public static Placement Place(float x, float y, float width, float height)
    {
        var outsideX = Math.Max(0f, Math.Abs(x - width / 2f) - width / 2f) / width;
        var outsideY = Math.Max(0f, Math.Abs(y - height / 2f) - height / 2f) / height;
        var outside = Math.Max(outsideX, outsideY);
        return outside > SilentScreens
            ? new Placement(false, 0f)
            : new Placement(true, OffScreenDb * outside / SilentScreens);
    }

    /// <summary>
    /// The panning strength of a 2D sound that makes a point at the edge of the screen fully left or right. Godot pans a 2D sound by its
    /// offset from the centre divided by the screen width, times this strength times the project's
    /// <c>audio/general/2d_panning_strength</c> (<paramref name="globalStrength"/>) times one half; with the strength set to two over
    /// the global one, the offset of half a screen is the whole pan.
    /// </summary>
    public static float PanningStrength(float globalStrength) => globalStrength > 0f ? 2f / globalStrength : 0f;
}
