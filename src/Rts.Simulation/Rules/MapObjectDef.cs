using System.Collections.Immutable;

namespace Rts.Simulation.Rules;

/// <summary>
/// A kind of map object (docs/implementation/M3_base_economy.md, step 14a; M7, 4.7): whom it stops, how much it takes, who drives over it
/// and flattens it, whether a tree leaves a stump, how it slows those who pass. The engine knows each kind by its id
/// (<see cref="Map.MapObjectTypes"/>): the number of the kind is part of the rules.
/// </summary>
public sealed record MapObjectDef : IDefinition
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    /// <summary>The locomotors it stops, by id.</summary>
    public required ImmutableArray<string> Blocks { get; init; }

    /// <summary>Its hit points; 0 when nothing destroys it.</summary>
    public required int Hp { get; init; }

    /// <summary>The locomotors that crush it by driving in, by id.</summary>
    public required ImmutableArray<string> CrushedBy { get; init; }

    /// <summary>A tree that leaves a stump when it falls.</summary>
    public required bool Stump { get; init; }

    /// <summary>The speed it gives a locomotor on its cell, below 100 %; those not named go at full speed.</summary>
    public required ImmutableArray<LocomotorSpeed> Speeds { get; init; }
}
