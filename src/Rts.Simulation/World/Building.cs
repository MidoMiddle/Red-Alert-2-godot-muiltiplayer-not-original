using Rts.Simulation.Geometry;
using Rts.Simulation.Rules;

namespace Rts.Simulation.World;

/// <summary>
/// A building: a footprint of cells that stops every mover, hit points and an owner. Two sorts for now:
/// <list type="bullet">
/// <item>a building of the map (docs/implementation/M7_maps.md, 4.8): a neutral house or tech building of a <see cref="BuildingKind"/>,
/// owned by the neutral player, always ready;</item>
/// <item>a building of a player from the data (docs/implementation/M3_base_economy.md, 4.7–4.11): a <see cref="BuildingDef"/>, which
/// gives power and opens technologies once it is <see cref="IsReady"/>.</item>
/// </list>
/// The buildings of the map move to the data with M3 step 6; M11 adds garrisons and capture. The footprint does not turn with
/// <see cref="Facing"/>, which is for the look only. A destroyed building leaves no ruins: its cells open at once.
/// </summary>
public sealed class Building : Entity
{
    /// <summary>A building of the map.</summary>
    internal Building(EntityId id, PlayerId owner, BuildingKind kind, CellPos cell, int facing)
        : base(id, owner)
    {
        Kind = kind;
        Cell = cell;
        Facing = facing;
        Width = BuildingTypes.Get(kind).Width;
        Height = BuildingTypes.Get(kind).Height;
        Hp = BuildingTypes.Get(kind).MaxHp;
        DefIndex = -1;
        Weapons = BuildingTypes.Get(kind).Weapon.Length > 0 ? [new()] : [];
    }

    /// <summary>
    /// A building of a player, the definition at <paramref name="defIndex"/> of the rules, growing for <paramref name="buildUp"/> ticks.
    /// </summary>
    internal Building(EntityId id, PlayerId owner, BuildingDef def, int defIndex, CellPos cell, int buildUp)
        : base(id, owner)
    {
        Def = def;
        DefIndex = defIndex;
        Cell = cell;
        Width = def.Footprint.Width;
        Height = def.Footprint.Height;
        Hp = def.Hp;
        BuildUpLeft = buildUp;
        ChargeLeft = def.Recharge;
        Weapons = def.Primary.Length > 0 ? [new()] : [];
    }

    /// <summary>What a defense shoots at (M4, 4.7); null without a target. State: hashed.</summary>
    public EntityId? Target { get; internal set; }

    /// <summary>The state of the weapon of a defense (or of the outpost of the map); empty for any other building.</summary>
    public Combat.WeaponInstance[] Weapons { get; } = [];

    /// <summary>
    /// The soldiers inside, in the order they went in (stage 2e); at most <see cref="Rules.BuildingDef.GarrisonCapacity"/>. State:
    /// hashed.
    /// </summary>
    public System.Collections.Generic.List<Occupant> Occupants { get; } = [];

    /// <summary>The ticks the superweapon still charges (stage 2d); 0 when it is ready to fire. State: hashed.</summary>
    public int ChargeLeft { get; internal set; }

    /// <summary>The ticks until a taken oil derrick gives its owner credits again; 0 for any other building. State: hashed.</summary>
    public int CashTimer { get; internal set; }

    /// <summary>
    /// The soldiers charging it now (RA2 tesla troopers by a tesla coil): counted every tick from their <see cref="Unit.Charging"/>,
    /// so derived, not hashed.
    /// </summary>
    public int Chargers { get; internal set; }

    /// <summary>The kind of a building of the map; 0 for a building of a player.</summary>
    public BuildingKind Kind { get; }

    /// <summary>The type of a building of the map.</summary>
    public BuildingType Type => BuildingTypes.Get(Kind);

    /// <summary>The definition of a building of a player; null for a building of the map.</summary>
    public BuildingDef? Def { get; }

    /// <summary>The position of <see cref="Def"/> in <see cref="RulesDatabase.Buildings"/>; -1 for a building of the map.</summary>
    public int DefIndex { get; }

    /// <summary>The north-west cell of the footprint.</summary>
    public CellPos Cell { get; }

    public int Width { get; }

    public int Height { get; }

    /// <summary>0…7 in 45° steps, for the look only.</summary>
    public int Facing { get; }

    public int Hp { get; internal set; }

    /// <summary>
    /// Ticks the building still grows (docs/implementation/M3_base_economy.md, 4.8): it stands and blocks its cells, but gives no
    /// power and counts as no prerequisite until 0. The buildings of the map never grow.
    /// </summary>
    public int BuildUpLeft { get; internal set; }

    /// <summary>
    /// The radius of the veil this building added to its owner's <see cref="Visibility.VisibilityGrid"/> (stage 2d); 0 while it adds
    /// none. Derived from the building and its owner's power, so not hashed: the grids are.
    /// </summary>
    internal int VeilRadius { get; set; }

    /// <summary>True once the building has grown.</summary>
    public bool IsReady => BuildUpLeft == 0;

    /// <summary>How far the building has grown, 0…1000 (per mille), for the client: 1000 when ready.</summary>
    public int BuildUpPermille => Def is { BuildUp: > 0 } def ? 1000 - BuildUpLeft * 1000 / def.BuildUp : 1000;

    /// <summary>The player turned the repair on (M3 step 11): it gains hit points every tick and costs credits.</summary>
    public bool Repairing { get; internal set; }

    /// <summary>What is left over from rounding the cost of the repair down, carried to the next tick so the sum comes out exact.</summary>
    public long RepairCarry { get; internal set; }

    /// <summary>The hit points of the building when whole.</summary>
    public int MaxHp => Def?.Hp ?? Type.MaxHp;

    /// <summary>Where the units of a factory go once they are out (4.10); null: one cell on from the exit.</summary>
    public WorldPos? RallyPoint { get; internal set; }

    /// <summary>The south-east cell of the footprint.</summary>
    public CellPos FarCell => new(Cell.X + Width - 1, Cell.Y + Height - 1);

    /// <summary>True when the cell is part of the footprint.</summary>
    public bool Covers(CellPos cell) =>
        cell.X >= Cell.X && cell.Y >= Cell.Y && cell.X < Cell.X + Width && cell.Y < Cell.Y + Height;
}
