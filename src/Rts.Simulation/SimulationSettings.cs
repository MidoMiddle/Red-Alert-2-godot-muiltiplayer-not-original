using System;
using Rts.Simulation.Map;
using Rts.Simulation.Matches;

namespace Rts.Simulation;

/// <summary>
/// Parameters of a match that every peer must share. Values are part of the replay header (step 8).
/// The map is optional: without one the match is played on clear land of the given size.
/// </summary>
public sealed record SimulationSettings
{
    public const int DefaultTicksPerSecond = 15;
    public const int DefaultMapSizeCells = 64;

    /// <summary>The credits of every player at the start of a skirmish (docs/implementation/M3_base_economy.md, decision 5).</summary>
    public const int DefaultStartingCredits = 10_000;

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

    /// <summary>The credits every player starts with (M3 step 5).</summary>
    public int StartingCredits { get; init; } = DefaultStartingCredits;

    /// <summary>
    /// The description of the match (M5 step 2): its players, their factions and starts. Null for a session of the sandbox or a test,
    /// where the units come by commands. Its seed and credits are those of the settings (<see cref="ForMatch"/>).
    /// </summary>
    public MatchSetup? Match { get; init; }

    /// <summary>The settings of <paramref name="setup"/> played on <paramref name="map"/>: its seed, its credits, the setup itself.</summary>
    public static SimulationSettings ForMatch(MatchSetup setup, MapGrid map) =>
        new(setup.Seed, map) { StartingCredits = setup.Options.StartingCredits, Match = setup };

    internal void Validate()
    {
        if (TicksPerSecond <= 0 || HashInterval <= 0 || MapWidthCells <= 0 || MapHeightCells <= 0 || StartingCredits < 0)
        {
            throw new ArgumentException($"Invalid simulation settings: {this}.");
        }

        if (Map is not null && (Map.Width != MapWidthCells || Map.Height != MapHeightCells))
        {
            throw new ArgumentException(
                $"Map is {Map.Width}x{Map.Height}, but the settings say {MapWidthCells}x{MapHeightCells}.");
        }

        if (Match is not null && (Match.Seed != Seed || Match.Options.StartingCredits != StartingCredits))
        {
            throw new ArgumentException("The seed and the credits of the settings must be those of the match setup.");
        }
    }

    /// <summary>The settings and the match setup checked against <paramref name="rules"/>: what a simulation can be started with.</summary>
    internal void Validate(Rules.RulesDatabase rules)
    {
        Validate();
        if (Match?.Problem(MapWidthCells, MapHeightCells, rules) is { } problem)
        {
            throw new ArgumentException($"The match cannot be played: {problem}.");
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
        && StartingCredits == other.StartingCredits
        && (Match is null ? other.Match is null : Match.Equals(other.Match))
        && (Map is null ? other.Map is null : other.Map is not null && Map.ContentEquals(other.Map));

    // Only for dictionaries; System.HashCode is banned in the simulation (randomized per process).
    public override int GetHashCode() =>
        unchecked((int)Seed ^ (int)(Seed >> 32) ^ (TicksPerSecond * 31) ^ (MapWidthCells * 997) ^ (MapHeightCells * 7919) ^ HashInterval ^ (StartingCredits * 31337));
}
