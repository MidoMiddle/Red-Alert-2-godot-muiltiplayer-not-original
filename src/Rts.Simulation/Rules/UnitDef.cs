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

    /// <summary>Id of the armor type (M4, 4.2); the checks of the data require it.</summary>
    public string Armor { get; init; } = "";

    /// <summary>Leptons per tick.</summary>
    public required int Speed { get; init; }

    /// <summary><c>BAngle</c> units per tick; zero turns at once, as infantry does.</summary>
    public required int TurnRate { get; init; }

    /// <summary><c>BAngle</c> units per tick the turret turns (M4, 4.6); not set, it turns as fast as the hull.</summary>
    public int? TurretTurnRate { get; init; }

    /// <summary>Base sight radius in cells; the height of the unit's cell adds to it (M7, 4.2).</summary>
    public required int Sight { get; init; }

    /// <summary>Id of the locomotor that moves it.</summary>
    public required string Locomotor { get; init; }

    public required UnitPlacement Placement { get; init; }

    /// <summary>Whether the turret turns apart from the hull (M2: <c>HasTurret</c>).</summary>
    public bool Turret { get; init; }

    public ImmutableArray<string> Prerequisites { get; init; } = ImmutableArray<string>.Empty;

    /// <summary>Credits of ore the unit carries when full (M3 step 10); 0 for a unit that does not harvest.</summary>
    public int HarvestCapacity { get; init; }

    /// <summary>Ticks to take one stage of ore off a cell.</summary>
    public int HarvestTime { get; init; }

    /// <summary>Credits the unit unloads per tick at the dock of a refinery.</summary>
    public int UnloadRate { get; init; }

    /// <summary>How dangerous it is, for the choice of targets (RA2 <c>ThreatPosed</c>, M4, 4.10).</summary>
    public int Threat { get; init; }

    /// <summary>Id of the main weapon (RA2 <c>Primary</c>); empty for a unit that does not fight.</summary>
    public string Primary { get; init; } = "";

    /// <summary>Id of the second weapon (RA2 <c>Secondary</c>), such as a gun against aircraft; only with a primary one.</summary>
    public string Secondary { get; init; } = "";

    /// <summary>Path of the model, relative to the assets folder; the simulation does not use it.</summary>
    public string Model { get; init; } = "";

    /// <summary>Id of the sidebar icon; the simulation does not use it.</summary>
    public string Icon { get; init; } = "";
}
