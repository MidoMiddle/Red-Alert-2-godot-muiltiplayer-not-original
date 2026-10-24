using Rts.Simulation.Geometry;
using Rts.Simulation.Math;
using Rts.Simulation.Pathfinding;

namespace Rts.Simulation.World;

/// <summary>
/// A unit of one of the <see cref="UnitTypes"/>, with the rules of its type from the data (<see cref="UnitArchetype"/>): position, move target, hull and turret direction.
/// State is changed only by the simulation; the client reads it.
/// </summary>
public sealed class Unit : Entity
{
    internal Unit(EntityId id, PlayerId owner, UnitArchetype archetype, WorldPos position)
        : base(id, owner)
    {
        Archetype = archetype;
        Position = position;
        Speed = archetype.Speed;
        TurnRate = archetype.TurnRate;
        Harvester = archetype.Harvests ? new Harvester() : null;
        Hp = archetype.Hp;
        Weapons = !archetype.Armed ? [] : archetype.Secondary >= 0 ? [new(), new()] : [new()];
        Stance = archetype.Stance;
        Anchor = position;
        Ammo = archetype.Ammo;
    }

    /// <summary>How the unit fights by itself (M4, 4.11). State: hashed.</summary>
    public Stance Stance { get; internal set; }

    /// <summary>Where a guarding unit comes back to: where it last stood by an order of the player (M4, 4.11). State: hashed.</summary>
    public WorldPos Anchor { get; internal set; }

    /// <summary>Whether <see cref="Target"/> or <see cref="TargetPoint"/> was ordered by the player (M4, 4.12). State: hashed.</summary>
    public TargetOrder TargetOrder { get; internal set; }

    /// <summary>The point of the ground a forced attack fires at; null otherwise (M4, 4.12). State: hashed.</summary>
    public WorldPos? TargetPoint { get; internal set; }

    /// <summary>
    /// True while the unit's movement belongs to a fight: it follows its target or stopped to fire (M4, 4.11). Its route waits
    /// meanwhile. State: hashed.
    /// </summary>
    public bool Engaged { get; internal set; }

    /// <summary>The cell of the target the unit last asked a path to while following it; null when it does not follow. State: hashed.</summary>
    public CellPos? ChaseCell { get; internal set; }

    /// <summary>What the unit shoots at (M4, 4.7): a unit or a building; null without a target. State: hashed.</summary>
    public EntityId? Target { get; internal set; }

    /// <summary>
    /// The building the soldier was ordered into (stage 2e): it goes in once it stands beside it; any other order forgets it. State:
    /// hashed.
    /// </summary>
    public EntityId? EnterTarget { get; internal set; }

    /// <summary>Deployed in place (stage 3b: the toxin trooper fires its deploy weapon); any order of the player undeploys it. State: hashed.</summary>
    public bool Deployed { get; internal set; }

    /// <summary>The ticks the unit still phases in after a jump (stage 3b: the time trooper): it neither moves nor fires. State: hashed.</summary>
    public int WarpTicks { get; internal set; }

    /// <summary>The units it carries, in the order they boarded (stage 3d: the transports, the IFV). State: hashed.</summary>
    public System.Collections.Generic.List<Occupant> Passengers { get; } = [];

    /// <summary>
    /// The type of the parasite inside this vehicle (stage 3d: a combat drone eating it from inside); 0 for none. State: hashed with
    /// the four below.
    /// </summary>
    public UnitType Parasite { get; internal set; }

    /// <summary>The player of the parasite.</summary>
    public PlayerId ParasiteOwner { get; internal set; }

    /// <summary>The hit points the parasite had when it went in, and has when it comes out.</summary>
    public int ParasiteHp { get; internal set; }

    /// <summary>The weapon it went in with, by its place in the rules: its warhead says how it eats.</summary>
    public int ParasiteWeapon { get; internal set; } = -1;

    /// <summary>The ticks until its next bite.</summary>
    public int ParasiteTicks { get; internal set; }

    /// <summary>The volleys it has left (stage 3f: the strike jets); its type's ammo when built. State: hashed with the one below.</summary>
    public int Ammo { get; internal set; }

    /// <summary>The ticks it has loaded for its next volley beside a building that rearms aircraft.</summary>
    public int RearmTicks { get; internal set; }

    /// <summary>The state of its weapons: the primary first, then the secondary; empty for a unit that does not fight.</summary>
    public Combat.WeaponInstance[] Weapons { get; }

    /// <summary>Hit points left (M4, 4.4): whole on arrival; weapons take them from step 5 on. State: hashed.</summary>
    public int Hp { get; internal set; }

    /// <summary>Hit points of a whole unit of its type.</summary>
    public int MaxHp => Archetype.Hp;

    /// <summary>The work of a unit that gathers ore (M3 step 10); null for every other unit.</summary>
    public Harvester? Harvester { get; }

    /// <summary>Rules of the unit's type. Only <see cref="UnitArchetype.Type"/> is state; the rest is fixed data.</summary>
    public UnitArchetype Archetype { get; }

    public UnitType Type => Archetype.Type;

    public WorldPos Position { get; internal set; }

    /// <summary>Point the unit is moving to, or null when it stands still.</summary>
    public WorldPos? MoveTarget { get; internal set; }

    /// <summary>Movement speed in leptons per tick. Starts at the type's value; veterancy and damage may change it later.</summary>
    public int Speed { get; internal set; }

    /// <summary>Hull turn speed in <c>BAngle</c> units per tick; 0 turns instantly (infantry).</summary>
    public int TurnRate { get; internal set; }

    /// <summary>
    /// The unit's route (docs/implementation/M2_unit_control.md, 4.10): legs in order, empty when it has no orders.
    /// The current leg is <see cref="OrderIndex"/>; arriving moves on to the next one.
    /// </summary>
    public System.Collections.Immutable.ImmutableArray<UnitOrder> Orders { get; internal set; } = [];

    /// <summary>Index in <see cref="Orders"/> of the leg the unit is on.</summary>
    public int OrderIndex { get; internal set; }

    /// <summary>True for a patrol: after the last leg the route starts over.</summary>
    public bool LoopOrders { get; internal set; }

    /// <summary>True when the current leg is an attack-move.</summary>
    public bool IsAttackMoving => OrderIndex < Orders.Length && Orders[OrderIndex].AttackMove;

    /// <summary>Spot of a soldier in its cell (0 centre, 1–4 corners, see <see cref="Movement.Reservations"/>); 0 for vehicles.</summary>
    public int Subcell { get; internal set; }

    /// <summary>Where the hull points. A new unit faces north.</summary>
    public BAngle Facing { get; internal set; }

    /// <summary>
    /// Where the turret points. Until targeting arrives in M4 it follows the hull; units without a turret
    /// (<see cref="UnitArchetype.HasTurret"/>) keep it too, the client just does not draw it.
    /// </summary>
    public BAngle TurretFacing { get; internal set; }

    /// <summary>True while the unit waits for its path search (docs/implementation/M2_unit_control.md, 4.4).</summary>
    public bool PathPending { get; internal set; }

    /// <summary>The path the unit drives along, or null when it has none (standing, or moving straight in tests).</summary>
    public CellPath? Path { get; internal set; }

    /// <summary>Index in the <see cref="CellPath.Waypoints"/> of <see cref="Path"/> that the unit drives to next.</summary>
    public int PathIndex { get; internal set; }

    /// <summary>Ticks in a row the unit had an order but did not move (docs/implementation/M2_unit_control.md, 4.7).</summary>
    public int WaitTicks { get; internal set; }

    /// <summary>Paths asked for around other units since the unit last entered a new cell; after 3 it gives up.</summary>
    public int Replans { get; internal set; }

    /// <summary>Where the unit goes again after giving way in a side pocket; null when it is not giving way.</summary>
    public WorldPos? ResumeTarget { get; internal set; }

    /// <summary>Ticks left to wait in the pocket before heading for <see cref="ResumeTarget"/> again.</summary>
    public int HoldTicks { get; internal set; }

    /// <summary>
    /// True while the unit only goes back to the place it was moved off to make room. Such a move is polite: it never asks
    /// others to make room in turn, so two units cannot keep moving each other off their places.
    /// </summary>
    public bool Returning { get; internal set; }
}
