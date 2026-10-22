using System.Collections.Generic;
using Rts.Simulation.Geometry;
using Rts.Simulation.Visibility;
using Rts.Simulation.World;

namespace Rts.Client.Terrain;

/// <summary>
/// The buildings of the players as one player knows them (docs/implementation/M4_combat.md, 4.15, step 12): its own always; another's
/// as it stands while seen, as last seen — a ghost — while in the fog, even after it fell there, until its cells are seen again; never
/// one that was never seen. Only a picture: the simulation does not know of ghosts and takes no order on them. With the fog revealed
/// every building shows as it stands. Pure, tested in Rts.Simulation.Tests.
/// </summary>
public sealed class BuildingGhosts
{
    private readonly Dictionary<EntityId, Building> _seen = new();
    private readonly Dictionary<EntityId, int> _growth = new();
    private readonly List<Shown> _shown = [];
    private GameWorld? _world;

    /// <summary>A building to draw: <see cref="BuildUpLeft"/> as it is now, or as it was last seen for a ghost.</summary>
    public readonly record struct Shown(Building Building, int BuildUpLeft, bool Ghost);

    /// <summary>What <paramref name="viewer"/> knows of the player buildings now; the same list is reused.</summary>
    public IReadOnlyList<Shown> Update(GameWorld world, PlayerId viewer, bool revealed = false)
    {
        if (!ReferenceEquals(world, _world))
        {
            // A new session numbers its entities again: nothing seen before is known.
            _seen.Clear();
            _growth.Clear();
            _world = world;
        }

        _shown.Clear();
        foreach (var entity in world.Entities)
        {
            if (entity is not Building { IsAlive: true, Def: not null } building)
            {
                continue;
            }

            if (revealed || building.Owner == viewer)
            {
                _shown.Add(new Shown(building, building.BuildUpLeft, Ghost: false));
            }
            else if (Vision.Sees(world, viewer, building))
            {
                _seen[building.Id] = building;
                _growth[building.Id] = building.BuildUpLeft;
                _shown.Add(new Shown(building, building.BuildUpLeft, Ghost: false));
            }
            else if (_seen.ContainsKey(building.Id))
            {
                _shown.Add(new Shown(building, _growth[building.Id], Ghost: true));
            }
        }

        if (revealed)
        {
            return _shown;
        }

        // Fallen ones that were seen: ghosts until their cells are seen again.
        List<EntityId>? forgotten = null;
        foreach (var (id, building) in _seen)
        {
            if (building.IsAlive && world.TryGet(id, out var entity) && ReferenceEquals(entity, building))
            {
                continue;
            }

            if (AnyCellVisible(world.VisionOf(viewer), building))
            {
                (forgotten ??= []).Add(id);
            }
            else
            {
                _shown.Add(new Shown(building, _growth[id], Ghost: true));
            }
        }

        foreach (var id in forgotten ?? [])
        {
            _seen.Remove(id);
            _growth.Remove(id);
        }

        return _shown;
    }

    private static bool AnyCellVisible(VisibilityGrid grid, Building building)
    {
        for (var y = building.Cell.Y; y <= building.FarCell.Y; y++)
        {
            for (var x = building.Cell.X; x <= building.FarCell.X; x++)
            {
                if (grid.IsVisible(new CellPos(x, y)))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
