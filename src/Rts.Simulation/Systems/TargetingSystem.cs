using System.Collections.Generic;
using Rts.Simulation.Combat;
using Rts.Simulation.Geometry;
using Rts.Simulation.Math;
using Rts.Simulation.Rules;
using Rts.Simulation.World;

namespace Rts.Simulation.Systems;

/// <summary>
/// The choice of targets (docs/implementation/M4_combat.md, 4.10), before <see cref="WeaponSystem"/>. Every armed unit and ready defense
/// looks again once in <see cref="Period"/> ticks, on the tick where <c>(tick + id) % 4 == 0</c>, so the work spreads evenly and the
/// result is the same everywhere. It takes the enemy unit or building (not a wall) within its reach — a unit within the longer of its
/// range and sight, a harvester and a defense within their range — that one of its weapons can hurt, with the best score:
/// <code>
/// threat × 10 + 50 when it fires at me + the share of my weapon against its armor in percent / 2 − its distance in cells × 5
///     + 20 when it is my target already
/// </code>
/// Equal scores go to the smaller id. With nothing in reach it keeps a target it already has while that lives — one out of reach, or
/// one given by hand that is no enemy. Neutral units and buildings look for nothing. The fog of war comes in step 11: until then every
/// enemy counts as seen; chasing a target and the stances come in step 7.
/// </summary>
public static class TargetingSystem
{
    public const int Period = 4;

    public static void Update(GameWorld world)
    {
        var rules = world.Rules;
        var nearby = new List<EntityId>();
        foreach (var entity in world.Entities)
        {
            if ((world.CurrentTick + entity.Id.Value) % Period != 0 || !entity.IsAlive || entity.Owner.IsNeutral)
            {
                continue;
            }

            switch (entity)
            {
                case Unit unit when unit.Weapons.Length > 0:
                    {
                        int[] weapons = [unit.Archetype.Primary, unit.Archetype.Secondary];
                        var reach = Reach(rules, weapons);
                        if (!unit.Archetype.Harvests)
                        {
                            reach = IntMath.Max(reach, unit.Archetype.Sight * WorldPos.LeptonsPerCell);
                        }

                        unit.Target = Best(world, rules, unit, unit.Position, reach, weapons, unit.Target, nearby);
                        break;
                    }

                case Building { IsReady: true } building when building.Weapons.Length > 0:
                    {
                        int[] weapons = [rules.WeaponOfBuilding(building.DefIndex)];
                        building.Target = Best(world, rules, building, Aim.CentreOf(building), Reach(rules, weapons), weapons, building.Target, nearby);
                        break;
                    }
            }
        }
    }

    /// <summary>The longest range of the weapons, in leptons.</summary>
    private static int Reach(GameRules rules, int[] weapons)
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

    private static EntityId? Best(
        GameWorld world, GameRules rules, Entity me, WorldPos from, int reach, int[] weapons, EntityId? current, List<EntityId> nearby)
    {
        world.Spatial.QueryRadius(world, from, reach, nearby);
        foreach (var entity in world.Entities)
        {
            if (entity is Building { IsAlive: true } building && building.Def is not { ExtendsBase: false }
                && DamageCalculator.Distance(from, Aim.NearestPoint(building, from)) <= reach)
            {
                nearby.Add(building.Id);
            }
        }

        nearby.Sort();
        EntityId? best = null;
        var bestScore = int.MinValue;
        foreach (var id in nearby)
        {
            if (!world.TryGet(id, out var candidate) || !candidate.IsAlive || candidate.Owner == me.Owner || candidate.Owner.IsNeutral)
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

        if (best is null && current is { } kept && world.TryGet(kept, out var target) && target.IsAlive)
        {
            return kept;
        }

        return best;
    }

    /// <summary>The best share in percent of the weapons that hit ground against the candidate's armor; 0 when none can hurt it.</summary>
    private static int BestShare(GameRules rules, int[] weapons, Entity candidate)
    {
        var armor = candidate switch
        {
            Unit unit => unit.Archetype.Armor,
            Building { DefIndex: >= 0 } building => rules.ArmorOfBuilding(building.DefIndex),
            _ => 0,
        };
        var best = 0;
        foreach (var index in weapons)
        {
            if (index < 0)
            {
                continue;
            }

            var weapon = rules.Database.Weapons[index];
            if ((weapon.Targets & WeaponTargets.Ground) == 0)
            {
                continue;
            }

            var verses = rules.Verses(rules.Database.IndexOfWarhead(weapon.Warhead), armor);
            best = IntMath.Max(best, (int)((verses.Raw * 100) >> Fixed.FractionalBits));
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
