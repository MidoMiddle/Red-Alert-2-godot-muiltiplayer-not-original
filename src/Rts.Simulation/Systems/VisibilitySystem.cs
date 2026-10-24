using Rts.Simulation.Geometry;
using Rts.Simulation.Visibility;
using Rts.Simulation.World;

namespace Rts.Simulation.Systems;

/// <summary>
/// Keeps the <see cref="VisibilityGrid"/> of every player up to date (docs/implementation/M4_combat.md, 4.14), at the end of the tick
/// before the dead leave, and at its start for what appeared between ticks (the buildings of the map, the units a test places). Every living unit and building of a player sees a circle: a unit from its cell, <see cref="SightRules.SightOf"/>
/// cells; a building — growing or ready — from the middle cell of its footprint, its <c>sight</c> plus the height bonus there. An entity
/// remembers the circle it added (<see cref="Entity.SightCell"/>, <see cref="Entity.SightRadius"/>): when it changes, the old circle
/// is taken away and the new one added; when the entity dies, its circle goes. An unchanged entity costs nothing. Neutral entities and
/// shots see nothing.
/// <para>
/// Stage 2d: a ready and online veil generator (<see cref="Rules.BuildingDef.VeilRadius"/>) adds its veil to its owner's grid the same
/// way, and takes it away when it falls, goes offline or is sold; an orbital uplink (<see cref="Rules.BuildingDef.RevealsMap"/>) keeps
/// its owner's whole map explored while it stands ready and online.
/// </para>
/// </summary>
public static class VisibilitySystem
{
    public static void Update(GameWorld world)
    {
        // The sensors of the moment (stage 3e).
        world.Sensors.Clear();
        foreach (var entity in world.Entities)
        {
            if (entity is Unit { IsAlive: true, Archetype.Sensors: > 0 } sensor && !sensor.Owner.IsNeutral)
            {
                world.Sensors.Add((sensor.Owner, sensor.Position, sensor.Archetype.Sensors * WorldPos.LeptonsPerCell));
            }
        }

        foreach (var entity in world.Entities)
        {
            if (entity is not (Unit or Building))
            {
                continue;
            }

            var (cell, radius) = entity.IsAlive ? SightOf(world, entity) : (null, 0);
            if (cell == entity.SightCell && radius == entity.SightRadius)
            {
                continue;
            }

            if (entity.SightCell is { } old)
            {
                world.VisionOf(entity.Owner).Hide(old, entity.SightRadius);
            }

            if (cell is { } now)
            {
                world.VisionOf(entity.Owner).Reveal(now, radius);
            }

            entity.SightCell = cell;
            entity.SightRadius = radius;
        }

        foreach (var entity in world.Entities)
        {
            if (entity is not Building { Def: { } def } building || building.Owner.IsNeutral || building.Owner.Value > PlayerId.MaxPlayers)
            {
                continue;
            }

            var working = building.IsAlive && building.IsReady && Combat.Defenses.IsOnline(world, building);
            if (def.RevealsMap && working)
            {
                world.VisionOf(building.Owner).ExploreAll();
            }

            var veil = def.VeilRadius > 0 && working ? def.VeilRadius : 0;
            if (veil != building.VeilRadius)
            {
                var middle = MiddleOf(building);
                var grid = world.VisionOf(building.Owner);
                if (building.VeilRadius > 0)
                {
                    grid.Veil(middle, building.VeilRadius, -1);
                }

                if (veil > 0)
                {
                    grid.Veil(middle, veil, 1);
                }
                building.VeilRadius = veil;
            }
        }
    }

    /// <summary>The cell and radius the entity sees from now; no cell when it sees nothing.</summary>
    public static (CellPos? Cell, int Radius) SightOf(GameWorld world, Entity entity)
    {
        if (entity.Owner.IsNeutral || entity.Owner.Value > PlayerId.MaxPlayers)
        {
            return (null, 0);
        }

        switch (entity)
        {
            case Unit unit when world.Contains(unit.Position):
                return (unit.Position.ToCell(), SightRules.SightOf(unit, world.Map));
            case Building { Def: { Sight: > 0 } def } building:
                var middle = MiddleOf(building);
                return (middle, SightRules.SightAt(world.Map, middle, def.Sight));
            default:
                return (null, 0);
        }
    }

    private static CellPos MiddleOf(Building building) => new(building.Cell.X + building.Width / 2, building.Cell.Y + building.Height / 2);
}
