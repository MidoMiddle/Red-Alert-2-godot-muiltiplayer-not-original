namespace Rts.Simulation.Rules;

/// <summary>
/// A faction from the data (docs/implementation/M3_base_economy.md, 4.1): the units and buildings it starts with and needs. Every
/// reference is an id of the matching definition; the validator of M3 step 3 checks that they exist.
/// </summary>
public sealed record FactionDef : IDefinition
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    /// <summary>The unit that deploys into the construction yard at the start.</summary>
    public required string Mcv { get; init; }

    /// <summary>The building that every base starts from.</summary>
    public required string ConstructionYard { get; init; }

    /// <summary>The ore refinery, which takes ore from the harvester.</summary>
    public required string Refinery { get; init; }

    /// <summary>The harvester that the refinery gives for free.</summary>
    public required string Harvester { get; init; }

    /// <summary>The infantry every player of the faction starts a match with, three of them beside the MCV (none when empty).</summary>
    public string StartingInfantry { get; init; } = "";

    /// <summary>The faction a player plays unless one is chosen: one of the data has it when there are several.</summary>
    public bool IsDefault { get; init; }
}
