using Rts.Simulation.Math;

namespace Rts.Simulation.Rules;

/// <summary>
/// How a unit moves over the ground (docs/implementation/M7_maps.md, 4.3). Its speed on each land type is in <see cref="TerrainDef.Speed"/>.
/// </summary>
public sealed record LocomotorDef : IDefinition
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    /// <summary>The speed multiplier on a slope, as a fraction: 0.8 for 80 percent.</summary>
    public required Fixed Slope { get; init; }
}
