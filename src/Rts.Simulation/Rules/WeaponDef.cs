using Rts.Simulation.Math;

namespace Rts.Simulation.Rules;

/// <summary>
/// A weapon from the data. This is a placeholder for M4, where damage, range and reload start to matter (docs/implementation/M3_base_economy.md, 4.1).
/// </summary>
public sealed record WeaponDef : IDefinition
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public int Damage { get; init; }

    /// <summary>Range in cells; fractions are allowed.</summary>
    public Fixed Range { get; init; }

    /// <summary>Ticks between two shots.</summary>
    public int Reload { get; init; }
}
