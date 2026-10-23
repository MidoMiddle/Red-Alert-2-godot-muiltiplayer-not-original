using System.Collections.Generic;
using Rts.Simulation.Geometry;
using Rts.Simulation.Visibility;
using Rts.Simulation.World;

namespace Rts.Client.Terrain;

/// <summary>
/// The buildings of the players as one player knows them (docs/implementation/M4_combat.md, 4.15, step 12): its own and its allies'
/// always (M6, A6); another's as it stands while its team sees it, as last seen — a ghost — while in the fog, even after it fell there,
/// until its team sees its cells again; never
/// one that was never seen. Only a picture: the simulation does not know of ghosts and takes no order on them. With the fog revealed
/// every building shows as it stands. Each viewer remembers on its own (M4 step 16): with the debug switch of the player (F11) a
/// building that fell while another player looked stays a ghost for the one that saw it. Pure, tested in Rts.Simulation.Tests.
/// </summary>
public sealed class BuildingGhosts
{
    private readonly Dictionary<PlayerId, Memory> _memories = new();
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
            _memories.Clear();
            _world = world;
        }

        if (!_memories.TryGetValue(viewer, out var memory))
        {
            memory = new Memory();
            _memories[viewer] = memory;
        }

        var (seen, growth) = (memory.Seen, memory.Growth);
        _shown.Clear();
        foreach (var entity in world.Entities)
        {
            if (entity is not Building { IsAlive: true, Def: not null } building)
            {
                continue;
            }

            if (revealed || world.AreAllies(viewer, building.Owner))
            {
                _shown.Add(new Shown(building, building.BuildUpLeft, Ghost: false));
            }
            else if (Vision.Sees(world, viewer, building))
            {
                seen[building.Id] = building;
                growth[building.Id] = building.BuildUpLeft;
                _shown.Add(new Shown(building, building.BuildUpLeft, Ghost: false));
            }
            else if (seen.ContainsKey(building.Id))
            {
                _shown.Add(new Shown(building, growth[building.Id], Ghost: true));
            }
        }

        if (revealed)
        {
            return _shown;
        }

        // Fallen ones that were seen: ghosts until their cells are seen again.
        List<EntityId>? forgotten = null;
        foreach (var (id, building) in seen)
        {
            if (building.IsAlive && world.TryGet(id, out var entity) && ReferenceEquals(entity, building))
            {
                continue;
            }

            if (AnyCellVisible(world, viewer, building))
            {
                (forgotten ??= []).Add(id);
            }
            else
            {
                _shown.Add(new Shown(building, growth[id], Ghost: true));
            }
        }

        foreach (var id in forgotten ?? [])
        {
            seen.Remove(id);
            growth.Remove(id);
        }

        return _shown;
    }

    /// <summary>What one viewer saw of the buildings of others: the building and its growth when last seen.</summary>
    private sealed class Memory
    {
        public Dictionary<EntityId, Building> Seen { get; } = new();

        public Dictionary<EntityId, int> Growth { get; } = new();
    }

    private static bool AnyCellVisible(GameWorld world, PlayerId viewer, Building building)
    {
        for (var y = building.Cell.Y; y <= building.FarCell.Y; y++)
        {
            for (var x = building.Cell.X; x <= building.FarCell.X; x++)
            {
                if (Vision.IsVisible(world, viewer, new CellPos(x, y)))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
