using System;

namespace Rts.Simulation;

/// <summary>
/// Parameters of a match that every peer must share. Values are part of the replay header (step 8).
/// </summary>
public sealed record SimulationSettings
{
    public const int DefaultTicksPerSecond = 15;
    public const int DefaultMapSizeCells = 64;

    public SimulationSettings(ulong seed)
    {
        Seed = seed;
    }

    public ulong Seed { get; }

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
    }
}
