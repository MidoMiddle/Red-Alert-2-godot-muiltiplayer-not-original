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
/// its owner's whole map explored while it stands ready, with or without power (RA2); when the player has none any more — lost, sold or
/// taken — what it does not see now is unexplored again (RA2: the shroud comes back).
/// </para>
/// </summary>
public static class VisibilitySystem
{
    public static void Update(GameWorld world)
    {
        Detectors(world);
        Reshroud(world);

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
            // The spy satellite reveals without power (RA2).
            if (def.RevealsMap && building.IsAlive && building.IsReady)
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

    /// <summary>
    /// The sensors of the moment (stage 3e), and the psychic sensors that stand ready with power (RA2): rebuilt with the sight, and
    /// again once the power of the tick is known, so a sensor that just lost its power unmasks nobody in that tick.
    /// </summary>
    public static void Detectors(GameWorld world)
    {
        world.Sensors.Clear();
        world.DisguiseDetectors.Clear();
        world.IntentReaders.Clear();
        foreach (var entity in world.Entities)
        {
            if (entity is Unit { IsAlive: true, Archetype.Sensors: > 0 } sensor && !sensor.Owner.IsNeutral)
            {
                world.Sensors.Add((sensor.Owner, sensor.Position, sensor.Archetype.Sensors * WorldPos.LeptonsPerCell));
            }
            else if (entity is Building { IsAlive: true, IsReady: true, Def: { } def } building && !building.Owner.IsNeutral
                     && (def.Sensors | def.DetectsDisguiseRadius | def.RevealsIntentRadius) > 0 && Combat.Defenses.IsOnline(world, building))
            {
                var centre = Combat.Aim.CentreOf(building);
                if (def.Sensors > 0)
                {
                    world.Sensors.Add((building.Owner, centre, def.Sensors * WorldPos.LeptonsPerCell));
                }

                if (def.DetectsDisguiseRadius > 0)
                {
                    world.DisguiseDetectors.Add((building.Owner, centre, def.DetectsDisguiseRadius * WorldPos.LeptonsPerCell));
                }

                if (def.RevealsIntentRadius > 0)
                {
                    world.IntentReaders.Add((building.Owner, centre, def.RevealsIntentRadius * WorldPos.LeptonsPerCell));
                }
            }
        }
    }

    // A player that had a spy satellite and has none now: the shroud comes back over what it does not see (RA2).
    private static void Reshroud(GameWorld world)
    {
        foreach (var player in world.Players)
        {
            var revealing = false;
            foreach (var entity in world.Entities)
            {
                if (entity is Building { IsAlive: true, IsReady: true, Def.RevealsMap: true } building && building.Owner == player.Id)
                {
                    revealing = true;
                    break;
                }
            }

            if (player.MapRevealed && !revealing)
            {
                world.VisionOf(player.Id).ForgetExplored();
            }

            player.MapRevealed = revealing;
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
            // A civilian building with soldiers inside sees round it (RA2: 4 cells; the bunker of RA2 its own 6).
            case Building { Def: null, Occupants.Count: > 0 } held:
                var inside = MiddleOf(held);
                return (inside, SightRules.SightAt(world.Map, inside, held.Type.Sight > 0 ? held.Type.Sight : GarrisonSystem.Sight));
            // A taken building of the map that sees (the outpost).
            case Building { Def: null } taken when BuildingTypes.IsKnown(taken.Kind) && taken.Type.Sight > 0:
                var centre = MiddleOf(taken);
                return (centre, SightRules.SightAt(world.Map, centre, taken.Type.Sight));
            default:
                return (null, 0);
        }
    }

    private static CellPos MiddleOf(Building building) => new(building.Cell.X + building.Width / 2, building.Cell.Y + building.Height / 2);
}
