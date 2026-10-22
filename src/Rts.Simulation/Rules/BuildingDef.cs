using System.Collections.Immutable;
using Rts.Simulation.Geometry;

namespace Rts.Simulation.Rules;

/// <summary>
/// A building type from the data (docs/implementation/M3_base_economy.md, 4.1, 4.8). Rules, not state. A positive power produces energy,
/// a negative one drains it.
/// </summary>
public sealed record BuildingDef : IDefinition
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    /// <summary>Id of the faction that builds it.</summary>
    public required string Faction { get; init; }

    public required ProductionCategory Category { get; init; }

    public required int Cost { get; init; }

    /// <summary>Production time in ticks.</summary>
    public required int BuildTime { get; init; }

    public required int Hp { get; init; }

    /// <summary>The cells the building covers, counted from its north-west cell.</summary>
    public required CellSize Footprint { get; init; }

    public int Power { get; init; }

    /// <summary>Ticks of the build-up animation: the building blocks its cells but does not work yet.</summary>
    public int BuildUp { get; init; }

    /// <summary>Where a factory releases its units, relative to the north-west cell of the footprint.</summary>
    public CellPos? ExitCell { get; init; }

    /// <summary>What the building produces (RA2 <c>Factory</c>): the construction yard buildings and defenses, barracks infantry.</summary>
    public ImmutableArray<ProductionCategory> Produces { get; init; } = ImmutableArray<ProductionCategory>.Empty;

    /// <summary>How many cells from the player's base the building may stand (RA2 <c>Adjacent</c>, 4.8).</summary>
    public int Adjacent { get; init; } = DefaultAdjacent;

    /// <summary>Whether new buildings may be placed next to this one (RA2 <c>BaseNormal</c>): false for walls.</summary>
    public bool ExtendsBase { get; init; } = true;

    /// <summary>The <see cref="Adjacent"/> of a building that does not set it.</summary>
    public const int DefaultAdjacent = 2;

    /// <summary>The largest <see cref="Adjacent"/> the data may set.</summary>
    public const int MaxAdjacent = 10;

    public ImmutableArray<string> Prerequisites { get; init; } = ImmutableArray<string>.Empty;

    /// <summary>Path of the model, relative to the assets folder; the simulation does not use it.</summary>
    public string Model { get; init; } = "";

    /// <summary>Id of the sidebar icon; the simulation does not use it.</summary>
    public string Icon { get; init; } = "";
}
