namespace Rts.Simulation.Rules;

/// <summary>
/// A type of armor (docs/implementation/M4_combat.md, 4.2): what a warhead's <see cref="WarheadDef.Verses"/> are given for. Its number is
/// its place in <see cref="RulesDatabase.Armors"/>, sorted by id like every kind.
/// </summary>
public sealed record ArmorDef : IDefinition
{
    public required string Id { get; init; }

    public required string Name { get; init; }
}
