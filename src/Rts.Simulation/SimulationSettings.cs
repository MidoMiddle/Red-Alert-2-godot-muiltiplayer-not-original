using System;
using Rts.Simulation.Map;

namespace Rts.Simulation;

/// <summary>
/// Parameters of a match that every peer must share. Values are part of the replay header (step 8).
/// The map is optional: without one the match is played on clear land of the given size.
/// </summary>
public sealed record SimulationSettings
{
    public const int DefaultTicksPerSecond = 15;
    public const int DefaultMapSizeCells = 64;

    public SimulationSettings(ulong seed)
    {
        Seed = seed;
    }

    /// <summary>A match on <paramref name="map"/>; the map size comes from it.</summary>
    public SimulationSettings(ulong seed, MapGrid map)
        : this(seed)
    {
        Map = map;
        MapWidthCells = map.Width;
        MapHeightCells = map.Height;
    }

    public ulong Seed { get; }

    /// <summary>
    /// Starting map, or null for clear land. Never changed by the match: the world plays on a copy.
    /// Compared by content, so a map read back from a replay equals the original.
    /// </summary>
    public MapGrid? Map { get; }

    /// <summary>Simulation rate. Game speed changes this, never the logic of a tick.</summary>
    public int TicksPerSecond { get; init; } = DefaultTicksPerSecond;

    public int MapWidthCells { get; init; } = DefaultMapSizeCells;

    public int MapHeightCells { get; init; } = DefaultMapSizeCells;

    /// <summary>State checksum is computed on ticks divisible by this value (step 7).</summary>
    public int HashInterval { get; init; } = 1;

    internal void Validate()
    {
        if (TicksPerSecond <= 0 || HashInterval <= 0 || MapWidthCells <= 0 || MapHeightCells <= 0)
        {
            throw new ArgumentException($"Invalid simulation settings: {this}.");
        }

        if (Map is not null && (Map.Width != MapWidthCells || Map.Height != MapHeightCells))
        {
            throw new ArgumentException(
                $"Map is {Map.Width}x{Map.Height}, but the settings say {MapWidthCells}x{MapHeightCells}.");
        }
    }

    /// <summary>The starting world map: a copy of <see cref="Map"/>, or clear land of the given size, going by <paramref name="speeds"/>.</summary>
    internal MapGrid CreateMap(LandSpeeds speeds) => Map?.Clone(speeds) ?? new MapGrid(MapWidthCells, MapHeightCells, speeds);

    public bool Equals(SimulationSettings? other) =>
        other is not null
        && Seed == other.Seed
        && TicksPerSecond == other.TicksPerSecond
        && MapWidthCells == other.MapWidthCells
        && MapHeightCells == other.MapHeightCells
        && HashInterval == other.HashInterval
        && (Map is null ? other.Map is null : other.Map is not null && Map.ContentEquals(other.Map));

    // Only for dictionaries; System.HashCode is banned in the simulation (randomized per process).
    public override int GetHashCode() =>
        unchecked((int)Seed ^ (int)(Seed >> 32) ^ (TicksPerSecond * 31) ^ (MapWidthCells * 997) ^ (MapHeightCells * 7919) ^ HashInterval);
}
