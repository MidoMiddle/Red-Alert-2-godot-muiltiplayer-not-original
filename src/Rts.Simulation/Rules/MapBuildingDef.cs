namespace Rts.Simulation.Rules;

/// <summary>
/// A kind of building of the map — a neutral house, a tech building (docs/implementation/M3_base_economy.md, step 14a; M7, 4.8): its
/// footprint, hit points, the soldiers it holds, whether an engineer takes it, and what a taken tech building gives. The engine knows
/// each kind by its id (<see cref="World.BuildingTypes"/>) and gives some of them their own behaviour (the oil derrick, the hospital).
/// </summary>
public sealed record MapBuildingDef : IDefinition
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required CellSize Footprint { get; init; }

    public required int Hp { get; init; }

    /// <summary>The soldiers it holds; 0 for none.</summary>
    public int Garrison { get; init; }

    /// <summary>Whether an engineer takes it.</summary>
    public bool Capturable { get; init; }

    /// <summary>The superweapon a taken one gives its owner (the airfield's paradrop), every <see cref="Recharge"/> ticks.</summary>
    public SuperweaponKind Superweapon { get; init; }

    public int Recharge { get; init; }

    /// <summary>The weapon a taken one fires at its owner's enemies (the outpost), by id; empty for none.</summary>
    public string Weapon { get; init; } = "";

    /// <summary>Its armor type, by id; empty for the default of buildings.</summary>
    public string Armor { get; init; } = "";

    /// <summary>The cells a taken one sees; 0 for none.</summary>
    public int Sight { get; init; }

    public bool RepairsVehicles { get; init; }

    public bool ExtendsBase { get; init; }

    /// <summary>The weapon it fires where it falls (the oil derrick's blast), by id; empty for none.</summary>
    public string DeathWeapon { get; init; } = "";
}
