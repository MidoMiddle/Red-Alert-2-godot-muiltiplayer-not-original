using System.Collections.Immutable;
using Rts.Simulation.World;

namespace Rts.Simulation.Rules;

/// <summary>
/// A unit type from the data (docs/implementation/M3_base_economy.md, 4.1). Rules, not state: a match never changes it.
/// Speed is in leptons per tick, turn rate in <c>BAngle</c> units per tick, build time in ticks.
/// </summary>
public sealed record UnitDef : IDefinition
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    /// <summary>Id of the faction that builds it.</summary>
    public required string Faction { get; init; }

    public required UnitCategory Category { get; init; }

    public required int Cost { get; init; }

    /// <summary>Production time in ticks.</summary>
    public required int BuildTime { get; init; }

    public required int Hp { get; init; }

    /// <summary>Id of the armor type; empty until M4 gives armor a meaning.</summary>
    public string Armor { get; init; } = "";

    /// <summary>Leptons per tick.</summary>
    public required int Speed { get; init; }

    /// <summary><c>BAngle</c> units per tick; zero turns at once, as infantry does.</summary>
    public required int TurnRate { get; init; }

    /// <summary>Base sight radius in cells; the height of the unit's cell adds to it (M7, 4.2).</summary>
    public required int Sight { get; init; }

    /// <summary>Id of the locomotor that moves it.</summary>
    public required string Locomotor { get; init; }

    public required UnitPlacement Placement { get; init; }

    /// <summary>Whether the turret turns apart from the hull (M2: <c>HasTurret</c>).</summary>
    public bool Turret { get; init; }

    public ImmutableArray<string> Prerequisites { get; init; } = ImmutableArray<string>.Empty;

    /// <summary>Ids of the weapons; they do damage from M4 on.</summary>
    public ImmutableArray<string> Weapons { get; init; } = ImmutableArray<string>.Empty;

    /// <summary>Path of the model, relative to the assets folder; the simulation does not use it.</summary>
    public string Model { get; init; } = "";

    /// <summary>Id of the sidebar icon; the simulation does not use it.</summary>
    public string Icon { get; init; } = "";
}
