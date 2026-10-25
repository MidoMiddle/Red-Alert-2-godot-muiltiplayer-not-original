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

    /// <summary>Where a harvester stands to unload, relative to the north-west cell (M3 step 10); null for a building without a dock.</summary>
    public CellPos? DockCell { get; init; }

    /// <summary>What the building produces (RA2 <c>Factory</c>): the construction yard buildings and defenses, barracks infantry.</summary>
    public ImmutableArray<ProductionCategory> Produces { get; init; } = ImmutableArray<ProductionCategory>.Empty;

    /// <summary>How many cells from the player's base the building may stand (RA2 <c>Adjacent</c>, 4.8).</summary>
    public int Adjacent { get; init; } = DefaultAdjacent;

    /// <summary>Whether new buildings may be placed next to this one (RA2 <c>BaseNormal</c>): false for walls and defenses.</summary>
    public bool ExtendsBase { get; init; } = true;

    /// <summary>
    /// A wall (M4 step 10): placed in lines, a base for walls of its kind, no target of the choice of targets, hurt only by a warhead
    /// that damages walls. One cell, never extending the base.
    /// </summary>
    public bool Wall { get; init; }

    /// <summary>
    /// Whether it stops working while its owner is low on power (RA2 <c>Powered</c>, M4 step 10) — only if it drains power itself, so a
    /// defense of power 0 works on.
    /// </summary>
    public bool Powered { get; init; }

    /// <summary>
    /// Whether the building gives its owner the minimap while there is power (RA2 <c>Radar</c>, 4.17). Only the client reads it.
    /// </summary>
    public bool Radar { get; init; }

    /// <summary>Id of the armor type (M4, 4.2); the checks of the data require it.</summary>
    public string Armor { get; init; } = "";

    /// <summary>How dangerous it is, for the choice of targets (RA2 <c>ThreatPosed</c>, M4, 4.10).</summary>
    public int Threat { get; init; }

    /// <summary>Sight radius in cells from the middle of the footprint (RA2 <c>Sight</c>, M4, 4.14); height adds to it.</summary>
    public int Sight { get; init; }

    /// <summary>Id of the weapon of a defense (RA2 <c>Primary</c>); empty for a building that does not fight.</summary>
    public string Primary { get; init; } = "";

    /// <summary>The <see cref="Adjacent"/> of a building that does not set it.</summary>
    public const int DefaultAdjacent = 3;

    /// <summary>The largest <see cref="Adjacent"/> the data may set.</summary>
    public const int MaxAdjacent = 10;

    public ImmutableArray<string> Prerequisites { get; init; } = ImmutableArray<string>.Empty;

    /// <summary>The countries that alone have it (RA2 <c>RequiredHouses</c>; loose ends, stage 5); empty for every country.</summary>
    public ImmutableArray<string> Countries { get; init; } = ImmutableArray<string>.Empty;

    /// <summary>The countries that may not have it (RA2 <c>ForbiddenHouses</c>).</summary>
    public ImmutableArray<string> ForbiddenCountries { get; init; } = ImmutableArray<string>.Empty;

    /// <summary>The countries whose players its superweapon works for (the USA's paradrop of the air command); empty for every one.</summary>
    public ImmutableArray<string> SuperweaponCountries { get; init; } = ImmutableArray<string>.Empty;

    /// <summary>The soldier its paradrop drops (<see cref="SuperweaponKind.Paradrop"/>); empty: the owner's faction's.</summary>
    public string Paradrop { get; init; } = "";

    /// <summary>How many of <see cref="Paradrop"/> it drops.</summary>
    public int ParadropCount { get; init; }

    /// <summary>Path of the model, relative to the assets folder; the simulation does not use it.</summary>
    public string Model { get; init; } = "";

    /// <summary>Id of the sidebar icon; the simulation does not use it.</summary>
    public string Icon { get; init; } = "";

    /// <summary>
    /// The building's own ability needs a mechanic that comes later (M11: superweapons, ships, repair…): it is built, draws power and
    /// opens the tech tree, and the sidebar says the rest is to come. The simulation does not use it.
    /// </summary>
    public bool AbilityLater { get; init; }

    /// <summary>The building stands on deep water, not on land (the shipyards, stage 2a): every cell of its footprint is deep.</summary>
    public bool Water { get; init; }

    /// <summary>Vehicles and ships of the owner that stand still beside it are repaired, at the price of repairing (stage 2c).</summary>
    public bool RepairsVehicles { get; init; }

    /// <summary>The ore unloaded brings its owner this many percent more while it stands ready (stage 2c); the best counts.</summary>
    public int IncomeBonusPercent { get; init; }

    /// <summary>
    /// RA2 cloning vats (<c>Cloning</c>): while it stands ready, every soldier its owner's barracks train comes out once more beside it,
    /// free, with or without power; a soldier of the owner's sent in leaves for half its price (<see cref="Systems.EntryAction.Recycle"/>).
    /// </summary>
    public bool ClonesInfantry { get; init; }

    /// <summary>
    /// RA2 psychic sensor, while it stands ready and online: within this many cells disguised spies are shown to its owner's team for
    /// what they are (<c>DetectDisguise</c>); 0 for none.
    /// </summary>
    public int DetectsDisguiseRadius { get; init; }

    /// <summary>The same: within this many cells units under the water are found (<c>SensorArray</c>, <c>SensorsSight</c>); 0 for none.</summary>
    public int Sensors { get; init; }

    /// <summary>The same: within this many cells the team sees what enemy units aim at (<c>PsychicDetectionRadius</c>); 0 for none.</summary>
    public int RevealsIntentRadius { get; init; }

    /// <summary>The weapon that goes off where it stood when a shot brings it down (stage 2c: the atomic plant); empty for none.</summary>
    public string DeathWeapon { get; init; } = "";

    /// <summary>While it stands ready and online, its owner's whole map is explored (stage 2d: the orbital uplink, RA2 spy satellite).</summary>
    public bool RevealsMap { get; init; }

    /// <summary>
    /// While it stands ready and online, the cells within this many of its middle are veiled from every player outside its owner's team
    /// (stage 2d: the veil generator, RA2 <c>GapRadius</c>); 0 for none.
    /// </summary>
    public int VeilRadius { get; init; }

    /// <summary>How many soldiers of its owner it holds (stage 2e: the fortified bunker); they fire from inside with their weapons.</summary>
    public int GarrisonCapacity { get; init; }

    /// <summary>
    /// The damage a supporting tower adds to the shot of another, in percent of its weapon (stage 2e: the lens tower chains); 0 for a
    /// building that neither supports nor is supported.
    /// </summary>
    public int ChainBonusPercent { get; init; }

    /// <summary>The most towers that support one shot.</summary>
    public int ChainMax { get; init; }

    /// <summary>Whether an engineer of another player takes it (stage 3b, RA2 <c>Capturable</c>): not a defense, wall or superweapon.</summary>
    public bool Capturable { get; init; } = true;

    /// <summary>What an infiltrator of another player does to it (stage 3b); <see cref="SpyEffect.None"/> takes none.</summary>
    public SpyEffect SpyEffect { get; init; }

    /// <summary>Aircraft out of ammo fly here and load beside it (stage 3f: the air force command, RA2 airport pads).</summary>
    public bool RearmsAircraft { get; init; }

    /// <summary>
    /// The pads it has for aircraft that rearm (RA2 <c>NumberOfDocks</c>): a player builds no more such aircraft than its pads.
    /// </summary>
    public int AircraftDocks { get; init; }

    /// <summary>The superweapon it is (stage 2d); <see cref="SuperweaponKind.None"/> for any other building.</summary>
    public SuperweaponKind Superweapon { get; init; }

    /// <summary>The ticks the superweapon charges, while it stands ready and online (RA2 <c>RechargeTime</c>).</summary>
    public int Recharge { get; init; }

    /// <summary>The weapon of the superweapon: the missile, the lightning; empty for the others.</summary>
    public string SuperweaponWeapon { get; init; } = "";

    /// <summary>How many of it a player may have, built and in the queue (RA2 <c>BuildLimit</c>); 0 for no limit.</summary>
    public int BuildLimit { get; init; }
}
