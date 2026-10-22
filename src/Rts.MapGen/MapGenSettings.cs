using System;

namespace Rts.MapGen;

/// <summary>How the halves or quarters of a random map repeat each other.</summary>
public enum MapSymmetry
{
    /// <summary>Left and right are mirror images: two players.</summary>
    Mirror,

    /// <summary>A half turn about the centre: two players.</summary>
    Rotate180,

    /// <summary>Quarter turns about the centre: four players.</summary>
    Rotate90,
}

public enum ReliefKind
{
    /// <summary>Level 0 everywhere.</summary>
    Flat,

    /// <summary>Hills up to level 4 with slopes only, no cliffs.</summary>
    Hilly,

    /// <summary>Terraces up to level 8 with cliffs, joined by ramps.</summary>
    Mountainous,
}

public enum WaterKind
{
    None,
    Rivers,
    Lakes,
}

public enum Amount
{
    Low,
    Medium,
    High,
}

/// <summary>
/// The settings of a random map (docs/implementation/M7_maps.md, 4.14): seed, size (64, 96, 128 or 160), players and symmetry
/// (two players mirrored or turned by 180°, four turned by 90°), relief, water, forest, resources and climate.
/// </summary>
public sealed record MapGenSettings
{
    public static readonly int[] Sizes = [64, 96, 128, 160];

    public uint Seed { get; init; }

    public int Size { get; init; } = 64;

    public int Players { get; init; } = 2;

    public MapSymmetry Symmetry { get; init; } = MapSymmetry.Mirror;

    public ReliefKind Relief { get; init; } = ReliefKind.Hilly;

    public WaterKind Water { get; init; } = WaterKind.Rivers;

    public Amount Forest { get; init; } = Amount.Medium;

    public Amount Resources { get; init; } = Amount.Medium;

    public string Climate { get; init; } = "winter";

    /// <summary>Throws when the settings do not go together: an unknown size, or players that do not match the symmetry.</summary>
    public void Check()
    {
        if (Array.IndexOf(Sizes, Size) < 0)
        {
            throw new ArgumentException($"Size {Size} is not one of 64, 96, 128, 160.");
        }

        var players = Symmetry == MapSymmetry.Rotate90 ? 4 : 2;
        if (Players != players)
        {
            throw new ArgumentException($"{Symmetry} symmetry is for {players} players, not {Players}.");
        }

        if (Climate != "winter")
        {
            throw new ArgumentException($"Climate '{Climate}' comes in M11; only winter for now.");
        }
    }
}
