using System.Collections.Generic;
using Rts.Simulation.Events;
using Rts.Simulation.World;

namespace Rts.Simulation.Systems;

/// <summary>
/// The growing of new buildings (docs/implementation/M3_base_economy.md, 4.8): a placed building stands and blocks its cells for the
/// <c>build_up</c> of its definition, then it works. Runs after power (4.15), so a building that finishes gives power from the next
/// tick on; the technologies, worked out at the end of the tick, open the same tick. A refinery of the owner's faction that has grown
/// gives a free harvester at its dock (4.13).
/// </summary>
public static class BuildUpSystem
{
    /// <summary>The free harvester of a grown refinery: at the dock, or the nearest free cell, and it looks for ore at once.</summary>
    private static void GiveHarvester(GameWorld world, Building building, List<SimEvent>? events)
    {
        if (building.Owner.IsNeutral || building.Def is not { DockCell: not null } def)
        {
            return;
        }

        var faction = world.Player(building.Owner).Faction;
        if (def.Id != faction.Refinery || !UnitTypes.TryGetById(faction.Harvester, out var type))
        {
            return;
        }

        var archetype = world.Rules.Unit(type);
        var dock = HarvesterSystem.DockOf(building);
        var cell = world.Map.CanEnter(dock, archetype.Locomotor) && world.Reservations.HasRoomFor(dock, archetype.Placement, EntityId.None)
            ? dock
            : ProductionSystem.FreeCellNear(world, dock, dock, archetype);
        if (cell is { } at)
        {
            var harvester = world.SpawnUnit(building.Owner, type, at.Center, snapToSubcell: true);
            events?.Add(new UnitProduced(world.CurrentTick, building.Owner, harvester.Id, building.Id));
        }
    }

    public static void Update(GameWorld world, List<SimEvent>? events)
    {
        List<Building>? grown = null;
        foreach (var entity in world.Entities)
        {
            if (entity is Building { IsAlive: true, BuildUpLeft: > 0 } building)
            {
                building.BuildUpLeft--;
                if (building.BuildUpLeft == 0)
                {
                    world.CompleteBuildUp(building);
                    events?.Add(new BuildUpComplete(world.CurrentTick, building.Id));
                    (grown ??= []).Add(building);
                }
            }
        }

        // After the walk over the entities: a free harvester is a new one.
        foreach (var building in grown ?? [])
        {
            GiveHarvester(world, building, events);
        }
    }
}
