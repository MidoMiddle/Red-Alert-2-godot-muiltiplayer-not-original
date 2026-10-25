using System.Collections.Generic;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Math;
using Rts.Simulation.Rules;
using Rts.Simulation.World;

namespace Rts.Simulation.Systems;

/// <summary>
/// The crates of a match with the option «Crates Appear» (docs/implementation/M11_content.md, a6; RA2 <c>[Powerups]</c>; the numbers
/// are the <c>crates</c> record, <see cref="CratesDef"/>). On tick 0 the map gets as many as it holds at most — <c>per_player</c> for
/// every player, never fewer than <c>at_least</c> — and while it holds fewer, a new one comes every <c>regen</c> ticks. A crate lies
/// on a cell of open land drawn by the simulation's generator: clear, rough or road a tank can enter, without ore, building, bridge or
/// another crate; after <see cref="Attempts"/> cells none fitted, it comes at the next chance.
/// <para>
/// A unit of a player on the ground — not a ship, not an aircraft — that stands on the cell of a crate after moving takes it; the
/// first of the world's order. What it gives is drawn by the weights then: Money (credits), Heal (every unit of the player whole),
/// Veteran (a rank up for the player's units within <c>radius</c> cells), Reveal (the whole map explored), Armor, Firepower or
/// Speed (for good, to the player's units within <c>radius</c>; they do not add up), or Unit (one of the player's faction from the
/// list, beside the crate). A Veteran that raises nobody, or a Unit that has no unit of the faction or no room, gives the Money.
/// </para>
/// </summary>
public static class CrateSystem
{
    /// <summary>The cells drawn for a new crate before giving up until the next chance.</summary>
    public const int Attempts = 64;

    /// <summary>Whether the match has crates: its option.</summary>
    public static bool Enabled(GameWorld world) => world.Match is { Options.Crates: true };

    /// <summary>The crates the map holds at most in the match.</summary>
    public static int Most(GameWorld world) =>
        IntMath.Max(world.Rules.Crates.AtLeast, world.Rules.Crates.PerPlayer * (world.Match?.Players.Length ?? 0));

    public static void Update(GameWorld world, List<SimEvent>? events)
    {
        if (!Enabled(world))
        {
            return;
        }

        var crates = world.Rules.Crates;
        TakeUp(world, crates, events);
        var most = Most(world);
        if (world.CurrentTick == 0)
        {
            for (var i = 0; i < most; i++)
            {
                Place(world, events);
            }

            world.CrateTimer = crates.Regen;
            return;
        }

        if (world.Crates.Count >= most)
        {
            world.CrateTimer = crates.Regen;
            return;
        }

        if (--world.CrateTimer <= 0)
        {
            Place(world, events);
            world.CrateTimer = crates.Regen;
        }
    }

    /// <summary>The kind the weights give for a draw of 0 … sum − 1.</summary>
    public static CrateKind KindOf(CratesDef crates, int draw)
    {
        for (var i = 0; i < crates.Weights.Length; i++)
        {
            draw -= crates.Weights[i];
            if (draw < 0)
            {
                return (CrateKind)(i + 1);
            }
        }

        return CrateKind.Money;
    }

    /// <summary>The damage of a hit with the shooter's Firepower and the victim's Armor.</summary>
    internal static int Damage(GameWorld world, Entity victim, EntityId shooter, int amount)
    {
        var crates = world.Rules.Crates;
        if (world.TryGet(shooter, out var entity) && entity is Unit { Upgrades: var mine } && (mine & CrateUpgrade.Firepower) != 0)
        {
            amount = (int)((long)amount * crates.Firepower.Raw / Fixed.OneRaw);
        }

        if (victim is Unit { Upgrades: var its } && (its & CrateUpgrade.Armor) != 0)
        {
            amount = IntMath.Max(1, (int)((long)amount * Fixed.OneRaw / crates.Armor.Raw));
        }

        return amount;
    }

    /// <summary>The speed of a unit with the Speed of a crate.</summary>
    internal static int Speed(GameWorld world, Unit unit, int speed) =>
        (unit.Upgrades & CrateUpgrade.Speed) != 0 ? (int)((long)speed * world.Rules.Crates.Speed.Raw / Fixed.OneRaw) : speed;

    // The crates on cells where a unit of a player stands now, in the order they came.
    private static void TakeUp(GameWorld world, CratesDef crates, List<SimEvent>? events)
    {
        for (var i = 0; i < world.Crates.Count; i++)
        {
            var crate = world.Crates[i];
            if (PickerAt(world, crate.Cell) is not { } picker)
            {
                continue;
            }

            world.Crates.RemoveAt(i--);
            var kind = Open(world, crates, crate, picker, KindOf(crates, world.Random.NextInt(Sum(crates))), events);
            events?.Add(new CratePicked(world.CurrentTick, crate.Id, picker.Owner, picker.Id, kind, crate.Cell));
        }
    }

    private static Unit? PickerAt(GameWorld world, CellPos cell)
    {
        foreach (var entity in world.Entities)
        {
            if (entity is Unit { IsAlive: true, ErasedBy: null } unit && unit.Owner.Value is >= 1 and <= PlayerId.MaxPlayers
                && unit.Archetype.Category is not (UnitCategory.Naval or UnitCategory.Aircraft) && !world.IsDefeated(unit.Owner)
                && unit.Position.ToCell() == cell)
            {
                return unit;
            }
        }

        return null;
    }

    // What the crate gives the picker's player; the kind given back, Money when the one drawn did nothing.
    internal static CrateKind Open(GameWorld world, CratesDef crates, Crate crate, Unit picker, CrateKind kind, List<SimEvent>? events)
    {
        var owner = picker.Owner;
        switch (kind)
        {
            case CrateKind.Heal:
                foreach (var unit in Units(world, owner, crate.Cell, radius: null))
                {
                    world.SetHp(unit, unit.MaxHp, events);
                }

                return kind;
            case CrateKind.Veteran:
                var raised = false;
                foreach (var unit in Units(world, owner, crate.Cell, crates.Radius))
                {
                    var next = Veterancy.RankOf(unit) switch
                    {
                        Rank.Rookie => unit.Archetype.Cost * Veterancy.VeteranRatio,
                        Rank.Veteran => unit.Archetype.Cost * Veterancy.VeteranRatio * 2,
                        _ => 0,
                    };
                    if (next > unit.Experience && !unit.Archetype.Has(UnitTalents.Untrainable) && unit.Archetype.Cost > 0)
                    {
                        Veterancy.Gain(world, unit, next - unit.Experience, events);
                        raised = true;
                    }
                }

                return raised ? kind : Money(world, crates, owner);
            case CrateKind.Reveal:
                world.VisionOf(owner).ExploreAll();
                return kind;
            case CrateKind.Armor or CrateKind.Firepower or CrateKind.Speed:
                var upgrade = kind switch
                {
                    CrateKind.Armor => CrateUpgrade.Armor,
                    CrateKind.Firepower => CrateUpgrade.Firepower,
                    _ => CrateUpgrade.Speed,
                };
                foreach (var unit in Units(world, owner, crate.Cell, crates.Radius))
                {
                    unit.Upgrades |= upgrade;
                }

                return kind;
            case CrateKind.Unit:
                return GiveUnit(world, crates, crate, owner, events) ? kind : Money(world, crates, owner);
            default:
                return Money(world, crates, owner);
        }
    }

    private static CrateKind Money(GameWorld world, CratesDef crates, PlayerId owner)
    {
        world.Player(owner).Credits += crates.Money;
        return CrateKind.Money;
    }

    // One of the faction's units of the list, drawn, beside the crate; false without one or without room.
    private static bool GiveUnit(GameWorld world, CratesDef crates, Crate crate, PlayerId owner, List<SimEvent>? events)
    {
        var faction = world.Player(owner).Faction.Id;
        var types = new List<UnitType>();
        foreach (var id in crates.Units)
        {
            if (world.Rules.Database.TryGetUnit(id, out var def) && def.Faction == faction && UnitTypes.TryGetById(id, out var type))
            {
                types.Add(type);
            }
        }

        if (types.Count == 0)
        {
            return false;
        }

        var chosen = types[world.Random.NextInt(types.Count)];
        var archetype = world.Rules.Unit(chosen);
        var cell = world.Map.CanEnter(crate.Cell, archetype.Locomotor) && world.Reservations.HasRoomFor(crate.Cell, archetype.Placement, EntityId.None)
            ? crate.Cell
            : TransportSystem.FreeCellAround(world, crate.Cell, archetype);
        if (cell is not { } free)
        {
            return false;
        }

        var unit = world.SpawnUnit(owner, chosen, free.Center, snapToSubcell: true);
        events?.Add(new UnitSpawned(world.CurrentTick, unit.Id, unit.Owner, unit.Type, unit.Position));
        return true;
    }

    // The living units of the player, within radius cells of the cell (the square of them), or all of them without one.
    private static List<Unit> Units(GameWorld world, PlayerId owner, CellPos cell, int? radius)
    {
        var units = new List<Unit>();
        foreach (var entity in world.Entities)
        {
            if (entity is not Unit { IsAlive: true, ErasedBy: null } unit || unit.Owner != owner)
            {
                continue;
            }

            var at = unit.Position.ToCell();
            if (radius is not { } reach || (IntMath.Abs(at.X - cell.X) <= reach && IntMath.Abs(at.Y - cell.Y) <= reach))
            {
                units.Add(unit);
            }
        }

        return units;
    }

    // A crate on a cell drawn by the generator, if one of Attempts fits.
    private static void Place(GameWorld world, List<SimEvent>? events)
    {
        var map = world.Map;
        for (var i = 0; i < Attempts; i++)
        {
            var cell = new CellPos(world.Random.NextInt(map.Width), world.Random.NextInt(map.Height));
            if (Fits(world, cell))
            {
                var crate = new Crate(world.NextCrateId++, cell);
                world.Crates.Add(crate);
                events?.Add(new CrateAppeared(world.CurrentTick, crate.Id, cell));
                return;
            }
        }
    }

    private static bool Fits(GameWorld world, CellPos cell)
    {
        var map = world.Map;
        if (map.SurfaceAt(cell) is not (LandType.Clear or LandType.Rough or LandType.Road) || map.IsBlocked(cell) || map.HasBuilding(cell)
            || map.BridgeAt(cell) is not null || !map.OreAt(cell).IsEmpty || world.BuildingAt(cell) is not null
            || !map.CanEnter(cell, Locomotor.Track))
        {
            return false;
        }

        foreach (var crate in world.Crates)
        {
            if (crate.Cell == cell)
            {
                return false;
            }
        }

        return true;
    }

    private static int Sum(CratesDef crates)
    {
        var sum = 0;
        foreach (var weight in crates.Weights)
        {
            sum += weight;
        }

        return sum;
    }
}
