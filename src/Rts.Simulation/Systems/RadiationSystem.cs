using System.Collections.Generic;
using Rts.Simulation.Combat;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Math;
using Rts.Simulation.Visibility;
using Rts.Simulation.World;

namespace Rts.Simulation.Systems;

/// <summary>
/// The toxin on the ground (stage 3b of filling the game: the toxin trooper, RA2 radiation), after the erasure. A deployed unit with a
/// deploy weapon (<see cref="Rules.UnitDef.DeployWeapon"/>) fires it at its own place as often as it reloads; a warhead with
/// <see cref="Rules.WarheadDef.Radiation"/> leaves that level on every cell of its area, up to <see cref="LevelMax"/> — the higher of the
/// old and the new. Every <see cref="LevelDelay"/> ticks each level falls by as much, so a cell clears in about its level in ticks. Every
/// <see cref="ApplicationDelay"/> ticks every unit on the ground of a cell with toxin takes the level divided by <see cref="DamageDivisor"/>,
/// by the verses of the warhead of <see cref="SiteWeapon"/> against its armor — its owner's units too; a flier and a unit immune to it
/// (<see cref="Rules.UnitDef.RadiationImmune"/>) take nothing. The numbers are RA2's (<c>RadLevelMax</c>, <c>RadLevelDelay</c>,
/// <c>RadApplicationDelay</c>, <c>RadLevelFactor</c>); how a level falls is ours.
/// </summary>
public static class RadiationSystem
{
    public const int LevelMax = 500;
    public const int LevelDelay = 90;
    public const int ApplicationDelay = 16;
    public const int DamageDivisor = 5;

    /// <summary>The weapon whose warhead the toxin hurts with, for the verses and for the deaths it causes.</summary>
    public const string SiteWeapon = "toxin_ground";

    /// <summary>A warhead with toxin lands: every cell within its spread gets the level, unless it has more.</summary>
    internal static void Lay(GameWorld world, WorldPos point, Fixed spread, int level)
    {
        var centre = point.ToCell();
        var radius = (int)((spread.Raw + Fixed.OneRaw - 1) >> Fixed.FractionalBits);
        foreach (var offset in SightCircles.Of(radius))
        {
            var cell = new CellPos(centre.X + offset.X, centre.Y + offset.Y);
            if (world.Map.Contains(cell))
            {
                var index = cell.Y * world.WidthCells + cell.X;
                world.Radiation[index] = (ushort)IntMath.Min(LevelMax, IntMath.Max(world.Radiation[index], level));
            }
        }
    }

    /// <summary>The ticks a unit that deploys once stays down after its shot (RA2 <c>UndeployDelay=150</c> frames: Yuri).</summary>
    public const int UndeployDelay = 150;

    public static void Update(GameWorld world, List<SimEvent>? events)
    {
        var rules = world.Rules;
        foreach (var entity in world.Entities)
        {
            // Yuri (RA2): one wave as he deploys, then he stands up after UndeployDelay.
            if (entity is Unit { IsAlive: true, Deployed: true, Archetype.DeployWeapon: >= 0 } once && once.Archetype.Has(UnitTalents.DeployOnce))
            {
                if (once.UndeployIn == 0)
                {
                    world.Hits.Add(new Hit(once.Id, once.Id, once.Owner, once.Archetype.DeployWeapon, once.Position, null));
                    once.UndeployIn = rules.Database.TryGetUnit(once.Archetype.Id, out var def) && def.UndeployDelay > 0 ? def.UndeployDelay : UndeployDelay;
                }
                else if (--once.UndeployIn == 0)
                {
                    once.Deployed = false;
                }

                continue;
            }

            if (entity is Unit { IsAlive: true, Deployed: true, Archetype.DeployWeapon: >= 0 } unit
                && (world.CurrentTick + unit.Id.Value) % IntMath.Max(1, rules.Database.Weapons[unit.Archetype.DeployWeapon].Reload) == 0)
            {
                // Lands in the damage of the next tick, as any shot fired after it.
                world.Hits.Add(new Hit(unit.Id, unit.Id, unit.Owner, unit.Archetype.DeployWeapon, unit.Position, null));
            }
        }

        var radiation = world.Radiation;
        if (world.CurrentTick % LevelDelay == 0)
        {
            for (var i = 0; i < radiation.Length; i++)
            {
                radiation[i] = (ushort)IntMath.Max(0, radiation[i] - LevelDelay);
            }
        }

        var site = rules.Database.IndexOfWeapon(SiteWeapon);
        if (world.CurrentTick % ApplicationDelay != 0 || site < 0)
        {
            return;
        }

        var warhead = rules.Database.IndexOfWarhead(rules.Database.Weapons[site].Warhead);
        foreach (var entity in world.Entities)
        {
            if (entity is not Unit { IsAlive: true } unit || unit.Archetype.Flies || unit.Archetype.Has(UnitTalents.RadiationImmune)
                || !world.Contains(unit.Position))
            {
                continue;
            }

            var cell = unit.Position.ToCell();
            var level = radiation[cell.Y * world.WidthCells + cell.X];
            if (level > 0)
            {
                var amount = DamageCalculator.Direct(level / DamageDivisor, rules.Verses(warhead, unit.Archetype.Armor));
                DamageSystem.Hurt(world, unit, amount, new Hit(EntityId.None, EntityId.None, PlayerId.Neutral, site, unit.Position, unit.Id), events);
            }
        }
    }
}
