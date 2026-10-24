using System.Collections.Generic;
using Rts.Simulation.Combat;
using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Math;
using Rts.Simulation.Movement;
using Rts.Simulation.Rules;
using Rts.Simulation.World;

namespace Rts.Simulation.Systems;

/// <summary>
/// The superweapons (stage 2d of filling the game; numbers of RA2). A superweapon building charges for its <see cref="BuildingDef.Recharge"/>
/// ticks while it stands ready and online — low power holds the charge — and tells every player when it is ready
/// (<see cref="SuperweaponReady"/>). A <see cref="SuperweaponCommand"/> fires a ready one (<see cref="SuperweaponFired"/>), and it charges
/// anew:
/// <list type="bullet">
/// <item>the Bastion Field Projector covers what stands in 3 x 3 cells round the target for <see cref="CurtainTicks"/> ticks — nothing hurts
/// it, a parasite inside dies — and kills the soldiers there;</item>
/// <item>the Strategic Missile Silo sends its missile from its centre to the target;</item>
/// <item>the Phase Gate takes the vehicles and ships in 3 x 3 cells round the target to the same places round the second cell; a soldier
/// dies, as does one that cannot stand where it comes, and a squid holding a ship; the others stand still for
/// <see cref="GateFreeze"/> ticks;</item>
/// <item>the Storm Generator calls a storm over the target: after <see cref="StormDelay"/> ticks, for <see cref="StormTicks"/> ticks, its
/// lightning strikes the target every <see cref="StrikeEvery"/> ticks and a random cell within <see cref="StormSpread"/> every
/// <see cref="ScatterEvery"/> (the simulation's generator).</item>
/// </list>
/// A player has no more of a superweapon than its <see cref="BuildingDef.BuildLimit"/>. A taken airfield of the map charges its paradrop
/// the same way (<see cref="BuildingType.Superweapon"/>, loose ends, stage 1): the plane of <see cref="ParadropSystem"/> drops the
/// soldiers of the owner's faction.
/// </summary>
public static class SuperweaponSystem
{
    /// <summary>RA2 <c>IronCurtainDuration</c>.</summary>
    public const int CurtainTicks = 750;

    /// <summary>RA2: the Chronosphere's pause.</summary>
    public const int GateFreeze = 60;

    /// <summary>RA2 <c>LightningDeferment</c>.</summary>
    public const int StormDelay = 250;

    /// <summary>RA2 <c>LightningStormDuration</c>.</summary>
    public const int StormTicks = 180;

    /// <summary>RA2 <c>LightningHitDelay</c>.</summary>
    public const int StrikeEvery = 10;

    /// <summary>RA2 <c>LightningScatterDelay</c>.</summary>
    public const int ScatterEvery = 5;

    /// <summary>The side of the square of the random strikes, in cells (RA2 <c>LightningCellSpread</c>).</summary>
    public const int StormSpread = 10;

    /// <summary>The charges, and the lightning of the storms; after the damage, so the strikes land in the next tick's.</summary>
    public static void Update(GameWorld world, List<SimEvent>? events)
    {
        foreach (var entity in world.Entities)
        {
            if (entity is Building { IsAlive: true, IsReady: true, ChargeLeft: > 0 } building && KindOf(world, building) != SuperweaponKind.None
                && Defenses.IsOnline(world, building) && --building.ChargeLeft == 0)
            {
                events?.Add(new SuperweaponReady(world.CurrentTick, building.Id, building.Owner, KindOf(world, building)));
            }
        }

        var tick = world.CurrentTick;
        for (var i = world.Storms.Count - 1; i >= 0; i--)
        {
            var storm = world.Storms[i];
            if (tick >= storm.End)
            {
                world.Storms.RemoveAt(i);
                continue;
            }

            if (tick < storm.Start)
            {
                continue;
            }

            var since = tick - storm.Start;
            if (since % StrikeEvery == 0)
            {
                Strike(world, storm, storm.Centre, events);
            }

            if (since % ScatterEvery == 0)
            {
                var cell = new CellPos(
                    storm.Centre.X + world.Random.NextInt(StormSpread) - (StormSpread / 2),
                    storm.Centre.Y + world.Random.NextInt(StormSpread) - (StormSpread / 2));
                if (world.Map.Contains(cell))
                {
                    Strike(world, storm, cell, events);
                }
            }
        }
    }

    /// <summary>Fires the player's ready superweapon; false, and nothing happens, when it cannot.</summary>
    internal static bool Fire(GameWorld world, SuperweaponCommand command, List<SimEvent>? events)
    {
        if (!world.TryGet(command.Building, out var entity) || entity is not Building { IsAlive: true, IsReady: true, ChargeLeft: 0 } building
            || building.Owner != command.Player || KindOf(world, building) == SuperweaponKind.None || !Defenses.IsOnline(world, building)
            || !world.Map.Contains(command.Target) || !world.Map.Contains(command.Second))
        {
            return false;
        }

        var kind = KindOf(world, building);
        var weapon = building.Def is { } def ? world.Rules.Database.IndexOfWeapon(def.SuperweaponWeapon) : -1;
        switch (kind)
        {
            case SuperweaponKind.Curtain:
                Curtain(world, building, command.Target, events);
                break;
            case SuperweaponKind.Missile:
                Missile(world, building, weapon, command.Target);
                break;
            case SuperweaponKind.Gate:
                Gate(world, building, command.Target, command.Second, events);
                break;
            case SuperweaponKind.Storm:
                world.Storms.Add(new Storm(building.Owner, building.Id, weapon, command.Target, world.CurrentTick + StormDelay, world.CurrentTick + StormDelay + StormTicks));
                break;
            case SuperweaponKind.Paradrop:
                if (Cargo(world, building) is not { } cargo)
                {
                    return false;
                }

                ParadropSystem.Send(world, building.Owner, building.Cell, command.Target, cargo.Type, cargo.Count);
                break;
        }

        building.ChargeLeft = RechargeOf(building);
        events?.Add(new SuperweaponFired(world.CurrentTick, building.Id, building.Owner, kind, command.Target));
        return true;
    }

    /// <summary>
    /// The superweapon of a building: its definition's — if its owner's country may have it (the USA's paradrop) — a taken building of
    /// the map's by its type; none for the neutral side.
    /// </summary>
    public static SuperweaponKind KindOf(GameWorld world, Building building) =>
        building.Owner.IsNeutral ? SuperweaponKind.None
        : building.Def is not { } def ? building.Type.Superweapon
        : def.SuperweaponCountries.IsDefaultOrEmpty || def.SuperweaponCountries.Contains(world.Player(building.Owner).Country) ? def.Superweapon
        : SuperweaponKind.None;

    /// <summary>The ticks the superweapon of a building charges.</summary>
    public static int RechargeOf(Building building) => building.Def is { } def ? def.Recharge : building.Type.Recharge;

    // What a paradrop drops: the building's own soldiers (the USA's air command), or those of the owner's faction; null without.
    private static (UnitType Type, int Count)? Cargo(GameWorld world, Building building)
    {
        if (building.Def is { Paradrop.Length: > 0 } def)
        {
            return UnitTypes.TryGetById(def.Paradrop, out var own) ? (own, def.ParadropCount) : null;
        }

        var faction = world.Player(building.Owner).Faction;
        return faction.Paradrop.Length > 0 && UnitTypes.TryGetById(faction.Paradrop, out var type) ? (type, faction.ParadropCount) : null;
    }

    /// <summary>Whether the player may have one more of the building: fewer than its build limit, built and in its queues.</summary>
    public static bool UnderLimit(GameWorld world, PlayerId player, int defIndex)
    {
        var def = world.Rules.Database.Buildings[defIndex];
        if (def.BuildLimit <= 0)
        {
            return true;
        }

        var count = 0;
        foreach (var entity in world.Entities)
        {
            if (entity is Building { IsAlive: true } building && building.Owner == player && building.DefIndex == defIndex)
            {
                count++;
            }
        }

        foreach (var item in world.Player(player).Queue(ProductionRules.CategoryOf(world.Rules.Database, ProductionKind.Building, defIndex)))
        {
            count += item.Kind == ProductionKind.Building && item.DefIndex == defIndex ? 1 : 0;
        }

        return count < def.BuildLimit;
    }

    // The units and buildings within 3 x 3 cells round the cell, in id order.
    private static List<Entity> Within(GameWorld world, CellPos centre)
    {
        var found = new List<Entity>();
        foreach (var entity in world.Entities)
        {
            var inside = entity switch
            {
                Unit { IsAlive: true } unit => Near(unit.Position.ToCell(), centre),
                Building { IsAlive: true } building => building.FarCell.X >= centre.X - 1 && building.Cell.X <= centre.X + 1
                    && building.FarCell.Y >= centre.Y - 1 && building.Cell.Y <= centre.Y + 1,
                _ => false,
            };
            if (inside)
            {
                found.Add(entity);
            }
        }

        return found;
    }

    private static bool Near(CellPos cell, CellPos centre) => IntMath.Abs(cell.X - centre.X) <= 1 && IntMath.Abs(cell.Y - centre.Y) <= 1;

    private static void Curtain(GameWorld world, Building source, CellPos centre, List<SimEvent>? events)
    {
        foreach (var entity in Within(world, centre))
        {
            if (entity is Unit { Archetype.Category: UnitCategory.Infantry } soldier)
            {
                DeathSystem.Kill(world, soldier, source.Id, source.Owner, DeathCause.Weapon, -1, events);
                continue;
            }

            entity.InvulnerableUntil = world.CurrentTick + CurtainTicks;
            if (entity is Unit { Parasite: not 0 } host)
            {
                ParasiteSystem.Clear(host);
                events?.Add(new ParasiteKilled(world.CurrentTick, host.Id));
            }
        }
    }

    private static void Missile(GameWorld world, Building silo, int weapon, CellPos target)
    {
        if (weapon < 0)
        {
            return;
        }

        var from = Aim.CentreOf(silo);
        var to = target.Center;
        world.Rules.Database.TryGetProjectile(world.Rules.Database.Weapons[weapon].Projectile, out var projectile);
        var shot = world.Launch(silo.Owner, silo.Id, weapon, null, from, to);
        shot.FlightTicks = IntMath.Max(1, DamageCalculator.Distance(from, to) / IntMath.Max(1, projectile.Speed));
        shot.TicksLeft = shot.FlightTicks;
    }

    private static void Gate(GameWorld world, Building source, CellPos from, CellPos to, List<SimEvent>? events)
    {
        foreach (var entity in Within(world, from))
        {
            if (entity is not Unit unit || unit.Archetype.Flies || unit.IsInvulnerable(world.CurrentTick))
            {
                continue;
            }

            var cell = new CellPos(to.X + unit.Position.ToCell().X - from.X, to.Y + unit.Position.ToCell().Y - from.Y);
            if (unit.Archetype.Category == UnitCategory.Infantry || !world.Map.Contains(cell)
                || !world.Map.CanEnter(cell, unit.Archetype.Locomotor) || !world.Reservations.HasRoomFor(cell, unit))
            {
                DeathSystem.Kill(world, unit, source.Id, source.Owner, DeathCause.Weapon, -1, events);
                continue;
            }

            var was = unit.Position;
            if (world.Contains(was))
            {
                world.Reservations.Leave(was.ToCell(), unit);
            }

            unit.Subcell = IntMath.Max(0, world.Reservations.TryEnter(cell, unit, cell.Center));
            unit.Position = cell.Center;
            UnitOrders.Stop(world, unit);
            UnitOrders.ForgetTarget(unit);
            unit.WarpTicks = GateFreeze;
            if (unit.Parasite != 0 && ParasiteSystem.Paralyzed(world, unit))
            {
                // The squid holding a ship is left behind, and dies.
                ParasiteSystem.Clear(unit);
                events?.Add(new ParasiteKilled(world.CurrentTick, unit.Id));
            }

            events?.Add(new UnitTeleported(world.CurrentTick, unit.Id, was, unit.Position));
        }
    }

    private static void Strike(GameWorld world, Storm storm, CellPos cell, List<SimEvent>? events)
    {
        if (storm.Weapon < 0)
        {
            return;
        }

        world.Hits.Add(new Hit(storm.Source, storm.Source, storm.Owner, storm.Weapon, cell.Center, null));
        events?.Add(new LightningStrike(world.CurrentTick, cell.Center));
    }
}
