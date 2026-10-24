namespace Rts.Simulation.Rules;

/// <summary>
/// A country of a faction (loose ends, stage 5; the countries of RA2): a player of the faction may play it, which gives a unit or a
/// building of its own (<see cref="UnitDef.Countries"/>) and takes away what it may not have (<see cref="UnitDef.ForbiddenCountries"/>).
/// </summary>
public sealed record CountryDef : IDefinition
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    /// <summary>The faction whose players may play it.</summary>
    public required string Faction { get; init; }
}
