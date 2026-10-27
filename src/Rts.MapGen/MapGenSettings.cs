using System;
using Rts.Data.Maps;

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

    /// <summary>The sea along the edges of the playable area, every base on its coast (the coast climate's maps; any climate may have it).</summary>
    Sea,

    /// <summary>
    /// An island for every player, raised over the sea with cliffs round it and two ramps down to a beach: across the water only ships,
    /// transports and aircraft go (the relief of the settings is not used).
    /// </summary>
    Islands,
}

public enum Amount
{
    Low,
    Medium,
    High,
}

/// <summary>
/// The settings of a random map (docs/implementation/M7_maps.md, 4.14): seed, size (64, 96, 128 or 160), players and symmetry
/// (two players mirrored or turned by 180°, four turned by 90°), relief, water, forest, resources, drills, climate and time of day.
/// </summary>
public sealed record MapGenSettings
{
    public static readonly int[] Sizes = [64, 96, 128, 160];

    /// <summary>
    /// Cells a side of the grid for a size: about size·√2, even, so the playable square in the screen's axes inscribed in it
    /// (<see cref="MapArea.Inscribed"/>) has about size² cells, as many as the square maps of that size had.
    /// </summary>
    public static int GridSize(int size) => size switch
    {
        64 => 90,
        96 => 136,
        128 => 182,
        160 => 226,
        _ => throw new ArgumentException($"Size {size} is not one of 64, 96, 128, 160."),
    };

    public uint Seed { get; init; }

    /// <summary>The kinds of map objects, bridges and buildings of the data the map is made of (M3 step 14a: <c>GameRules.MapTypes</c>).</summary>
    public Rts.Simulation.Map.MapTypes Types { get; init; } = Rts.Simulation.Map.MapTypes.Empty;

    public int Size { get; init; } = 64;

    public int Players { get; init; } = 2;

    public MapSymmetry Symmetry { get; init; } = MapSymmetry.Mirror;

    public ReliefKind Relief { get; init; } = ReliefKind.Hilly;

    public WaterKind Water { get; init; } = WaterKind.Rivers;

    /// <summary>
    /// The share of the playable area the sea takes, in percent (5…50; the owner, 2026-10-25: a map of 80 % land); 0 — the sea's own
    /// width, about a third of the area. Only for <see cref="WaterKind.Sea"/>.
    /// </summary>
    public int SeaPercent { get; init; }

    public Amount Forest { get; init; } = Amount.Medium;

    public Amount Resources { get; init; } = Amount.Medium;

    /// <summary>The drills of the fields (M8 step 10b): ore at home and gems in the contested field unless said otherwise.</summary>
    public DrillKind Drills { get; init; } = DrillKind.OreAndGems;

    /// <summary>The climate of the map, one of <see cref="ClimateGrounds.Ids"/>: the look of its ground and trees only.</summary>
    public string Climate { get; init; } = ClimateGrounds.Winter;

    /// <summary>The time of day of the map, one of <see cref="MapLighting.Ids"/>: look only.</summary>
    public string Lighting { get; init; } = MapLighting.Default;

    /// <summary>Throws when the settings do not go together: an unknown size, or players that do not match the symmetry.</summary>
    public void Check()
    {
        if (!System.Linq.Enumerable.Any(Types.Objects))
        {
            throw new ArgumentException("The generator needs the kinds of the map of the data (Types: GameRules.MapTypes).");
        }

        if (Array.IndexOf(Sizes, Size) < 0)
        {
            throw new ArgumentException($"Size {Size} is not one of 64, 96, 128, 160.");
        }

        var players = Symmetry == MapSymmetry.Rotate90 ? 4 : 2;
        if (Players != players)
        {
            throw new ArgumentException($"{Symmetry} symmetry is for {players} players, not {Players}.");
        }

        if (!ClimateGrounds.IsKnown(Climate))
        {
            throw new ArgumentException($"Climate '{Climate}' is not one of {string.Join(", ", ClimateGrounds.Ids)}.");
        }

        if (!MapLighting.IsKnown(Lighting))
        {
            throw new ArgumentException($"Lighting '{Lighting}' is none of {string.Join(", ", MapLighting.Ids)}.");
        }

        if (SeaPercent != 0 && (Water != WaterKind.Sea || SeaPercent < 5 || SeaPercent > 50))
        {
            throw new ArgumentException($"A sea of {SeaPercent} % needs the water Sea and 5…50 %.");
        }
    }
}
