using Rts.Simulation.Map;

namespace Rts.Simulation.World;

/// <summary>
/// Movement and sight data of one unit type: rules, not state. Made from the unit's definition in the data
/// (<see cref="Rules.UnitDef"/>) by <see cref="Rules.GameRules"/>, with the enums the simulation reads every tick.
/// </summary>
/// <param name="Id">Data id of the type, such as <c>heavy_tank</c>.</param>
/// <param name="Speed">Movement speed, leptons per tick.</param>
/// <param name="TurnRate">Turn speed, <c>BAngle</c> units per tick; 0 turns instantly, like RA2 infantry.</param>
/// <param name="Sight">Base sight radius in cells; height adds to it (see <c>Visibility.SightRules</c>).</param>
/// <param name="HarvestCapacity">Credits of ore the unit carries when full; 0 for a unit that does not harvest (M3 step 10).</param>
/// <param name="HarvestTime">Ticks to take one stage of ore off a cell.</param>
/// <param name="UnloadRate">Credits unloaded per tick at a dock.</param>
/// <param name="Hp">Hit points of a whole unit (M4 step 2).</param>
/// <param name="Armor">The number of its armor type: its place in <see cref="Rules.RulesDatabase.Armors"/> (M4 step 2).</param>
/// <param name="TurretTurnRate"><c>BAngle</c> units per tick the turret turns; 0 turns at once (M4 step 3).</param>
/// <param name="Primary">The position of the main weapon in <see cref="Rules.RulesDatabase.Weapons"/>; -1 without one (M4 step 3).</param>
/// <param name="Secondary">The position of the second weapon; -1 without one.</param>
/// <param name="Threat">How dangerous it is, for the choice of targets (M4 step 6).</param>
/// <param name="Stance">The stance a new unit starts with (M4 step 7).</param>
/// <param name="Crusher">Whether it crushes the infantry it drives over (M4 step 8).</param>
/// <param name="Crushable">Whether a crushing vehicle can drive over it (M4 step 8).</param>
public sealed record UnitArchetype(
    UnitType Type,
    string Id,
    UnitCategory Category,
    Locomotor Locomotor,
    UnitPlacement Placement,
    int Speed,
    int TurnRate,
    bool HasTurret,
    int Sight,
    int HarvestCapacity = 0,
    int HarvestTime = 0,
    int UnloadRate = 0,
    int Hp = 1,
    int Armor = 0,
    int TurretTurnRate = 0,
    int Primary = -1,
    int Secondary = -1,
    int Threat = 0,
    Stance Stance = Stance.Aggressive,
    bool Crusher = false,
    bool Crushable = false,
    bool Jumpjet = false,
    UnitTalents Talents = UnitTalents.None,
    int DeathWeapon = -1,
    int DeployWeapon = -1,
    int Size = 1,
    int PassengerSlots = 0,
    int PassengerSizeLimit = 0,
    int IfvWeapon = -1,
    int Sensors = 0,
    int Ammo = 0,
    int SelfHeal = 0,
    UnitType Spawn = 0,
    int SpawnCount = 0,
    int SpawnRegen = 0,
    int Cost = 0)
{
    /// <summary>True for a unit with a weapon.</summary>
    public bool Armed => Primary >= 0;

    /// <summary>
    /// A flier (stage 2b): it flies straight over land and water, takes no cell and is hit only by weapons at the air; an aircraft, or a
    /// soldier with a jet pack (stage 3a).
    /// </summary>
    public bool Flies => Category == UnitCategory.Aircraft || Jumpjet;

    /// <summary>Whether the unit has <paramref name="talent"/> (stage 3b).</summary>
    public bool Has(UnitTalents talent) => (Talents & talent) != 0;

    /// <summary>True for a unit that gathers ore.</summary>
    public bool Harvests => HarvestCapacity > 0;
}
