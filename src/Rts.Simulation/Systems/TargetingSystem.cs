using System.Collections.Generic;
using System.Collections.Immutable;
using Rts.Simulation.Combat;
using Rts.Simulation.Geometry;
using Rts.Simulation.Math;
using Rts.Simulation.Rules;
using Rts.Simulation.Visibility;
using Rts.Simulation.World;

namespace Rts.Simulation.Systems;

/// <summary>
/// The choice of targets (docs/implementation/M4_combat.md, 4.10, 4.11), before <see cref="PursuitSystem"/>. Every armed unit and
/// ready defense looks again once in <see cref="Period"/> ticks, on the tick where <c>(tick + id) % 4 == 0</c>, so the work spreads
/// evenly and the result is the same everywhere. It takes the enemy unit or building (not a wall) within its reach that one of its
/// weapons can hurt, with the best score:
/// <code>
/// threat × 10 + 50 when it fires at me + the share of my weapon against its armor in percent / 2 − its distance in cells × 5
///     + 20 when it is my target already
/// </code>
/// Equal scores go to the smaller id. The reach is the longer of range and sight for an aggressive or guarding unit, the range for one
/// that holds its position, a harvester and a defense; a guarding unit takes nothing farther than <see cref="PursuitSystem.GuardLeash"/>
/// cells and its range from its anchor. With nothing in reach it keeps its target only while that is still in reach: a target that
/// left its sight is dropped. A unit with a target ordered by the player does not look while that stands, nor does a unit on a leg of
/// a plain move: such a move ignores the enemies until it arrives (4.12). Neutral units and buildings look for nothing, nor does a defense off for want of
/// power (<see cref="Defenses.IsOnline"/>, step 10). Only what the owner sees counts (<see cref="Vision.Sees"/>, step 11): a target
/// gone into the fog is dropped, an ordered one too.
/// </summary>
public static class TargetingSystem
{
    public const int Period = 4;

    public static void Update(GameWorld world)
    {
        var rules = world.Rules;
        var nearby = world.Nearby;
        foreach (var entity in world.Entities)
        {
            if (entity is Unit { IsAlive: true, TargetOrder: not TargetOrder.None } ordered)
            {
                EndFallenOrder(world, ordered);
            }

            if (!IsLookTick(world, entity.Id) || !entity.IsAlive || entity.Owner.IsNeutral || entity.ErasedBy is not null
                || entity is Unit { WarpTicks: > 0 })
            {
                continue;
            }

            switch (entity)
            {
                // A passive unit (stage 3b: the demolition trooper) fires only at what it is ordered to.
                case Unit unit when unit.Weapons.Length > 0 && unit.TargetOrder == TargetOrder.None && !IsMoving(unit)
                    && !unit.Archetype.Has(UnitTalents.Passive):
                    {
                        var weapons = rules.WeaponsOf(unit.Type);
                        var reach = Reach(rules, weapons);
                        if (!unit.Archetype.Harvests && unit.Stance != Stance.HoldPosition)
                        {
                            reach = IntMath.Max(reach, unit.Archetype.Sight * WorldPos.LeptonsPerCell);
                        }

                        var leash = !unit.Archetype.Harvests && unit.Stance == Stance.Guard
                            ? (PursuitSystem.GuardLeash * WorldPos.LeptonsPerCell) + Reach(rules, weapons)
                            : -1;
                        unit.Target = Best(world, rules, unit, new Area(unit.Position, reach, unit.Anchor, leash), weapons, unit.Target, nearby);
                        break;
                    }

                case Building { IsReady: true } building when building.Weapons.Length > 0 && !Defenses.IsOnline(world, building):
                    // Off for want of power (step 10): it looks for nothing.
                    building.Target = null;
                    break;

                case Building { IsReady: true } building when building.Weapons.Length > 0:
                    {
                        var weapons = rules.WeaponsOfBuilding(building.DefIndex);
                        var centre = Aim.CentreOf(building);
                        building.Target = Best(world, rules, building, new Area(centre, Reach(rules, weapons), centre, -1), weapons, building.Target, nearby);
                        break;
                    }

                case Building { IsReady: true } building when GarrisonSystem.IsArmed(building):
                    {
                        // The soldiers inside (stage 2e): the weapons of all of them, their range from the wall.
                        var weapons = GarrisonSystem.WeaponsOf(rules, building);
                        var centre = Aim.CentreOf(building);
                        var reach = Reach(rules, weapons) + GarrisonSystem.Wall(building);
                        building.Target = Best(world, rules, building, new Area(centre, reach, centre, -1), weapons, building.Target, nearby);
                        break;
                    }
            }
        }
    }

    /// <summary>Whether <paramref name="id"/> looks again on this tick: once in <see cref="Period"/> ticks, spread by id.</summary>
    public static bool IsLookTick(GameWorld world, EntityId id) => (world.CurrentTick + id.Value) % Period == 0;


    /// <summary>The layer a weapon must fire at to hit <paramref name="target"/> (stage 2b): the air for a flier, else the ground.</summary>
    public static WeaponTargets LayerOf(Entity? target) => target is Unit { Archetype.Flies: true } ? WeaponTargets.Air : WeaponTargets.Ground;

    /// <summary>Whether one of the weapons fires at <paramref name="layer"/> (a forced attack: the ground, or a flier in the air).</summary>
    internal static bool Hits(GameRules rules, ImmutableArray<int> weapons, WeaponTargets layer)
    {
        foreach (var weapon in weapons)
        {
            if (weapon >= 0 && (rules.Database.Weapons[weapon].Targets & layer) != 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether a unit of <paramref name="type"/> can hurt <paramref name="target"/> — what an <c>AttackCommand</c> asks of every unit it
    /// orders; a bot asks it before it orders one (M5, step 8).
    /// </summary>
    public static bool CanHurt(GameRules rules, UnitType type, Entity target) => BestShare(rules, rules.WeaponsOf(type), target) > 0;

    /// <summary>The share in percent of the damage a unit of <paramref name="type"/> does to <paramref name="target"/> with its best weapon (the bots, M8 step 5).</summary>
    public static int ShareAgainst(GameRules rules, UnitType type, Entity target) => BestShare(rules, rules.WeaponsOf(type), target);

    /// <summary>
    /// The best share in percent of the weapons that fire at the candidate's layer — the air for a flier, the ground for the rest
    /// (stage 2b) — against its armor; 0 when none can hurt it.
    /// </summary>
    internal static int BestShare(GameRules rules, ImmutableArray<int> weapons, Entity candidate)
    {
        var armor = candidate switch
        {
            Unit unit => unit.Archetype.Armor,
            Building { DefIndex: >= 0 } building => rules.ArmorOfBuilding(building.DefIndex),
            _ => 0,
        };
        var best = 0;
        var layer = LayerOf(candidate);
        foreach (var index in weapons)
        {
            if (index < 0)
            {
                continue;
            }

            var weapon = rules.Database.Weapons[index];
            if ((weapon.Targets & layer) == 0)
            {
                continue;
            }

            var verses = rules.Verses(rules.Database.IndexOfWarhead(weapon.Warhead), armor);
            best = IntMath.Max(best, (int)((verses.Raw * 100) >> Fixed.FractionalBits));
        }

        return best;
    }

    /// <summary>Whether <paramref name="me"/> takes the candidate, a disguised unit (stage 3b), for one of its own: all but the dogs do.</summary>
    internal static bool Fooled(Entity me, Entity candidate) =>
        candidate is Unit disguised && disguised.Archetype.Has(UnitTalents.Disguised)
        && !(me is Unit watcher && watcher.Archetype.Has(UnitTalents.DetectsDisguise));

    /// <summary>A unit on a leg of a plain move: it does not look for enemies until it arrives.</summary>
    internal static bool IsMoving(Unit unit) => unit.Orders.Length > 0 && !unit.IsAttackMoving;

    /// <summary>
    /// The ordered target fell or went out of sight (step 11, "follow it while it is seen"): the unit chooses by itself again. A forced
    /// attack on the ground lasts until another order.
    /// </summary>
    private static void EndFallenOrder(GameWorld world, Unit unit)
    {
        if (unit.TargetPoint is null
            && (unit.Target is not { } id || !world.TryGet(id, out var target) || !target.IsAlive || !Vision.Sees(world, unit.Owner, target)))
        {
            unit.Target = null;
            unit.TargetOrder = TargetOrder.None;
        }
    }

    /// <summary>Where a unit or defense looks: within <c>Reach</c> of <c>From</c>, and within <c>Leash</c> of <c>Anchor</c> when that is not -1.</summary>
    private readonly record struct Area(WorldPos From, int Reach, WorldPos Anchor, int Leash)
    {
        public bool Holds(Entity candidate) =>
            DamageCalculator.Distance(From, Aim.NearestPoint(candidate, From)) <= Reach
            && (Leash < 0 || DamageCalculator.Distance(Anchor, Aim.NearestPoint(candidate, Anchor)) <= Leash);
    }

    /// <summary>The longest range of the weapons, in leptons.</summary>
    private static int Reach(GameRules rules, ImmutableArray<int> weapons)
    {
        var reach = 0;
        foreach (var weapon in weapons)
        {
            if (weapon >= 0)
            {
                reach = IntMath.Max(reach, Aim.Leptons(rules.Database.Weapons[weapon].Range));
            }
        }

        return reach;
    }

    private static EntityId? Best(GameWorld world, GameRules rules, Entity me, Area area, ImmutableArray<int> weapons, EntityId? current, List<EntityId> nearby)
    {
        var from = area.From;
        world.Spatial.QueryRadius(world, from, area.Reach, nearby);
        foreach (var entity in world.Entities)
        {
            if (entity is Building { IsAlive: true } building && building.Def is not { Wall: true }
                && DamageCalculator.Distance(from, Aim.NearestPoint(building, from)) <= area.Reach)
            {
                nearby.Add(building.Id);
            }
        }

        nearby.Sort();
        EntityId? best = null;
        var bestScore = int.MinValue;
        foreach (var id in nearby)
        {
            // One held by another's temporal beam (stage 3a) is out of time: only that shooter keeps it.
            if (!world.TryGet(id, out var candidate) || !candidate.IsAlive || world.AreAllies(candidate.Owner, me.Owner) || candidate.Owner.IsNeutral
                || (candidate.ErasedBy is { } holder && holder != me.Id)
                // A disguised unit (stage 3b: the infiltrator) passes for one of ours, but not before a dog; one with a bomb on it is
                // left by those that only plant bombs.
                || Fooled(me, candidate)
                || (candidate.BombTicks > 0 && BombSystem.PlantsOnly(rules, weapons))
                || !area.Holds(candidate) || !Vision.Sees(world, me.Owner, candidate))
            {
                continue;
            }

            var share = BestShare(rules, weapons, candidate);
            if (share <= 0)
            {
                continue;
            }

            var distance = DamageCalculator.Distance(from, Aim.NearestPoint(candidate, from));
            var score = (ThreatOf(candidate) * 10) + (TargetOf(candidate) == me.Id ? 50 : 0) + (share / 2)
                - (distance / WorldPos.LeptonsPerCell * 5) + (current == id ? 20 : 0);
            if (score > bestScore)
            {
                (best, bestScore) = (id, score);
            }
        }

        if (best is null && current is { } kept && world.TryGet(kept, out var target) && target.IsAlive && area.Holds(target)
            && Vision.Sees(world, me.Owner, target))
        {
            return kept;
        }

        return best;
    }

    private static int ThreatOf(Entity entity) => entity switch
    {
        Unit unit => unit.Archetype.Threat,
        Building { Def: { } def } => def.Threat,
        _ => 0,
    };

    private static EntityId? TargetOf(Entity entity) => entity switch
    {
        Unit unit => unit.Target,
        Building building => building.Target,
        _ => null,
    };
}
