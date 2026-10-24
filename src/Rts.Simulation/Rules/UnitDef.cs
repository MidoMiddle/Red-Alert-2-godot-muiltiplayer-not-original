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

    /// <summary>The countries that alone have it (RA2 <c>RequiredHouses</c>; loose ends, stage 5); empty for every country.</summary>
    public ImmutableArray<string> Countries { get; init; } = ImmutableArray<string>.Empty;

    /// <summary>The countries that may not have it (RA2 <c>ForbiddenHouses</c>).</summary>
    public ImmutableArray<string> ForbiddenCountries { get; init; } = ImmutableArray<string>.Empty;

    /// <summary>Credits of ore the unit carries when full (M3 step 10); 0 for a unit that does not harvest.</summary>
    public int HarvestCapacity { get; init; }

    /// <summary>Ticks to take one stage of ore off a cell.</summary>
    public int HarvestTime { get; init; }

    /// <summary>Credits the unit unloads per tick at the dock of a refinery.</summary>
    public int UnloadRate { get; init; }

    /// <summary>How dangerous it is, for the choice of targets (RA2 <c>ThreatPosed</c>, M4, 4.10).</summary>
    public int Threat { get; init; }

    /// <summary>The stance a new unit starts with (M4, 4.11).</summary>
    public World.Stance Stance { get; init; }

    /// <summary>Whether it crushes the infantry it drives over (RA2 <c>Crusher</c>, M4, 4.13); only a vehicle.</summary>
    public bool Crusher { get; init; }

    /// <summary>Whether a crushing vehicle can drive over it (RA2 <c>Crushable</c>, M4, 4.13); only infantry.</summary>
    public bool Crushable { get; init; }

    /// <summary>
    /// A soldier with a jet pack (stage 3a: the jetpack trooper, RA2 jumpjet): it hovers and flies as an aircraft does, built in the
    /// barracks; only infantry.
    /// </summary>
    public bool Jumpjet { get; init; }

    /// <summary>
    /// An engineer (stage 3b): it goes into a building of another player that can be taken and takes it, or into its own damaged one
    /// and mends it; either way it is spent.
    /// </summary>
    public bool Engineer { get; init; }

    /// <summary>An infiltrator (stage 3b, RA2 spy): it goes into a building of another player and does what the building allows.</summary>
    public bool Infiltrator { get; init; }

    /// <summary>Other players' units and defenses take it for one of their own and do not pick it as a target (RA2 <c>CanDisguise</c>).</summary>
    public bool Disguised { get; init; }

    /// <summary>It sees through a disguise and picks a disguised unit as any other (RA2 <c>DetectDisguise</c>: the dogs).</summary>
    public bool DetectsDisguise { get; init; }

    /// <summary>It picks no target by itself and fires only when ordered (RA2 <c>CanPassiveAquire=no</c>: the demolition trooper).</summary>
    public bool Passive { get; init; }

    /// <summary>The weapon that goes off where it dies (stage 3b: the demolition trooper); empty for none.</summary>
    public string DeathWeapon { get; init; } = "";

    /// <summary>It jumps to where it is sent instead of walking, and then phases in for a while (stage 3b: the time trooper).</summary>
    public bool Teleports { get; init; }

    /// <summary>
    /// The weapon it fires at its own cell, as often as it reloads, while deployed (stage 3b: the toxin trooper's eruption); empty for
    /// a unit that does not deploy so.
    /// </summary>
    public string DeployWeapon { get; init; } = "";

    /// <summary>
    /// The weapon it fires instead of its own while deployed (the Rifleman behind sandbags, RA2 <c>Deployer</c> with <c>Para</c>);
    /// empty for a unit that does not deploy so.
    /// </summary>
    public string DeployedWeapon { get; init; } = "";

    /// <summary>The toxin on the ground does not hurt it (RA2: the desolator).</summary>
    public bool RadiationImmune { get; init; }

    /// <summary>
    /// Standing still, it looks like a tree to other teams, and none of their units picks it as a target (stage 3c: the holo tank, RA2
    /// <c>DisguiseWhenStill</c>).
    /// </summary>
    public bool DisguiseWhenStill { get; init; }

    /// <summary>
    /// The room it has for passengers, in slots (stage 3d: the transports, RA2 <c>Passengers</c>); 0 for a unit that carries none.
    /// </summary>
    public int PassengerSlots { get; init; }

    /// <summary>The largest passenger it takes, in slots (RA2 <c>SizeLimit</c>).</summary>
    public int PassengerSizeLimit { get; init; }

    /// <summary>The slots it takes as a passenger (RA2 <c>Size</c>); 0 for the default — 1 for infantry, 3 for anything else.</summary>
    public int Size { get; init; }

    /// <summary>It fires the weapon of its passenger instead of its own (stage 3d: the IFV, RA2 <c>IFVMode</c>).</summary>
    public bool PassengerWeapon { get; init; }

    /// <summary>The weapon an IFV fires with this soldier inside; empty for none (the IFV keeps its own).</summary>
    public string IfvWeapon { get; init; } = "";

    /// <summary>It works only while its owner has a ready, powered building that controls robots (stage 3d: the robot tank).</summary>
    public bool Robotic { get; init; }

    /// <summary>
    /// It keeps under the water (stage 3e: the submarine, the dolphin, the squid): another team sees it only near a unit of theirs with
    /// sensors, and only weapons at the underwater hit it.
    /// </summary>
    public bool Submerged { get; init; }

    /// <summary>How far it finds units under the water, in cells (RA2 <c>Sensors</c>); 0 for none.</summary>
    public int Sensors { get; init; }

    /// <summary>
    /// The volleys it carries (stage 3f: the strike jets, RA2 <c>Ammo</c>); spent, it flies to a building that rearms aircraft and loads
    /// there. 0 for a unit that never runs out.
    /// </summary>
    public int Ammo { get; init; }

    /// <summary>The hit points it heals a second by itself (the Super Heavy Tank, RA2 <c>SelfHealing</c>); 0 for none.</summary>
    public int SelfHeal { get; init; }

    /// <summary>Whether it gains experience from its kills (RA2 <c>Trainable</c>; loose ends, stage 3: veterancy).</summary>
    public bool Trainable { get; init; } = true;

    /// <summary>The unit it carries and launches (the aircraft carrier's Hornets, RA2 <c>Spawns</c>); empty for none.</summary>
    public string Spawns { get; init; } = "";

    /// <summary>How many of them it carries (RA2 <c>SpawnsNumber</c>).</summary>
    public int SpawnCount { get; init; }

    /// <summary>The ticks to replace one that was lost (RA2 <c>SpawnRegenRate</c>).</summary>
    public int SpawnRegen { get; init; }

    /// <summary>It is launched by another unit and never built (a Hornet).</summary>
    public bool Spawned { get; init; }

    /// <summary>Id of the main weapon (RA2 <c>Primary</c>); empty for a unit that does not fight.</summary>
    public string Primary { get; init; } = "";

    /// <summary>Id of the second weapon (RA2 <c>Secondary</c>), such as a gun against aircraft; only with a primary one.</summary>
    public string Secondary { get; init; } = "";

    /// <summary>Path of the model, relative to the assets folder; the simulation does not use it.</summary>
    public string Model { get; init; } = "";

    /// <summary>Id of the sidebar icon; the simulation does not use it.</summary>
    public string Icon { get; init; } = "";

    /// <summary>
    /// The unit cannot be built yet: it needs a mechanic that comes later — ships a shipyard on the water, aircraft flight (the second
    /// stage of filling the game). It is in the data and the sidebar, never in the tech tree.
    /// </summary>
    public bool AvailableLater { get; init; }

    /// <summary>Its weapon or ability comes later (the third stage): it is built and moves, the sidebar says the rest is to come.</summary>
    public bool AbilityLater { get; init; }
}
