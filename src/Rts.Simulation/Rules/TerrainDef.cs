using System.Collections.Immutable;

namespace Rts.Simulation.Rules;

/// <summary>
/// A land type from the data (docs/implementation/M7_maps.md, 4.3; docs/implementation/M3_base_economy.md, 4.5): what a cell is for
/// movement and building, not how it looks. <see cref="Speed"/> lists a <see cref="LocomotorSpeed"/> for each locomotor, sorted by id.
/// </summary>
public sealed record TerrainDef : IDefinition
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    /// <summary>The character of this land type in the land layer of a map file.</summary>
    public required char Code { get; init; }

    /// <summary>Whether a construction site may stand on it.</summary>
    public bool Buildable { get; init; }

    public required ImmutableArray<LocomotorSpeed> Speed { get; init; }
}
