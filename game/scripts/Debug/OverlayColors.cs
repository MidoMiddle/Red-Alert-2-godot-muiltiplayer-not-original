namespace Rts.Client.Debug;

/// <summary>A cell colour of the debug overlay, bytes 0…255; <see cref="A"/> 0 leaves the ground as it is.</summary>
public readonly record struct OverlayColor(byte R, byte G, byte B, byte A)
{
    public static readonly OverlayColor None = new(0, 0, 0, 0);
}

/// <summary>
/// The colours of the cell overlay F7 (docs/implementation/M2_unit_control.md, 4.14). Pure, tested in Rts.Simulation.Tests.
/// </summary>
public static class OverlayColors
{
    public static readonly OverlayColor Impassable = new(230, 40, 30, 150);
    public static readonly OverlayColor Vehicle = new(40, 90, 255, 150);

    /// <summary>A cell the selected units see (M7 step 8).</summary>
    public static readonly OverlayColor Seen = new(90, 200, 255, 90);

    /// <summary>
    /// Passability for one locomotor: impassable red; slower than normal orange, denser the slower; normal untouched; faster
    /// green.
    /// </summary>
    public static OverlayColor Passability(int speedPercent)
    {
        if (speedPercent <= 0)
        {
            return Impassable;
        }

        if (speedPercent < 100)
        {
            return new OverlayColor(255, 150, 20, (byte)(40 + (100 - speedPercent) * 120 / 100));
        }

        return speedPercent == 100 ? OverlayColor.None : new OverlayColor(40, 220, 60, 110);
    }

    /// <summary>Who stands in the cell: a vehicle blue, soldiers yellow, denser with each of the five spots taken.</summary>
    public static OverlayColor Occupancy(bool vehicle, int soldiers)
    {
        if (vehicle)
        {
            return Vehicle;
        }

        return soldiers <= 0 ? OverlayColor.None : new OverlayColor(255, 220, 30, (byte)(40 + System.Math.Min(soldiers, 5) * 24));
    }

    /// <summary>How full a bucket of the spatial index is: empty untouched, then warmer up to <see cref="HotUnits"/> units.</summary>
    public static OverlayColor Heat(int units)
    {
        if (units <= 0)
        {
            return OverlayColor.None;
        }

        var t = System.Math.Min(units, HotUnits);
        return new OverlayColor(255, (byte)(200 - t * 160 / HotUnits), 40, (byte)(60 + t * 100 / HotUnits));
    }

    /// <summary>The bucket count at which the heat stops growing.</summary>
    public const int HotUnits = 12;
}

/// <summary>What the cell overlay F7 shows; each press goes to the next one.</summary>
public enum CellOverlayMode
{
    Off,
    Passability,
    Reservations,
    Buckets,
    Sight,
}

/// <summary>The order of the cell overlays and their line on the debug panel. Pure, tested in Rts.Simulation.Tests.</summary>
public static class CellOverlayModes
{
    public static CellOverlayMode Next(CellOverlayMode mode) => mode == CellOverlayMode.Sight ? CellOverlayMode.Off : mode + 1;

    /// <param name="locomotor">The locomotor the passability is shown for, by name.</param>
    public static string Describe(CellOverlayMode mode, string locomotor) => mode switch
    {
        CellOverlayMode.Passability => $"cells: passability {locomotor} (F7)",
        CellOverlayMode.Reservations => "cells: reservations (F7)",
        CellOverlayMode.Buckets => "cells: index buckets 4x4 (F7)",
        CellOverlayMode.Sight => "cells: sight of the selected (F7)",
        _ => "cells off (F7)",
    };
}
