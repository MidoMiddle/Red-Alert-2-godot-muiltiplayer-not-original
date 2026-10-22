using System.Collections.Generic;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Math;
using Rts.Simulation.World;

namespace Rts.Simulation.Systems;

/// <summary>
/// Ore grows and spreads (docs/implementation/M3_base_economy.md, 4.12), once every <c>ore_growth_interval</c> ticks, late in the tick
/// (4.15). Growth: every cell of ore below the last stage, in index order, grows a stage by <c>ore_growth_chance</c>. Spread: the cells
/// of ore from <c>ore_spread_stage</c> on — taken before any seeding, so ore seeded this round does not seed further — each seeds, by
/// <c>ore_spread_chance</c>, one of its 8 neighbours picked at random, when that cell can take ore. Gems neither grow nor spread. The
/// rolls come from the world's random generator in a fixed order, so every peer grows the same ore.
/// </summary>
public static class OreGrowthSystem
{
    // N, NE, E, SE, S, SW, W, NW.
    private static readonly (int Dx, int Dy)[] Neighbours = [(0, -1), (1, -1), (1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1)];

    public static void Update(GameWorld world)
    {
        var economy = world.Rules.Economy;
        if (world.CurrentTick == 0 || world.CurrentTick % economy.OreGrowthInterval != 0)
        {
            return;
        }

        var map = world.Map;
        var growth = Permille(economy.OreGrowthChance);
        var spread = Permille(economy.OreSpreadChance);
        var sources = new List<CellPos>();
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var cell = new CellPos(x, y);
                var ore = map.OreAt(cell);
                if (ore.Kind != OreKind.Ore)
                {
                    continue;
                }

                if (ore.Stage >= economy.OreSpreadStage)
                {
                    sources.Add(cell);
                }

                if (ore.Stage < OreCell.MaxStage && world.Random.NextInt(1000) < growth)
                {
                    map.SetOre(cell, ore with { Stage = ore.Stage + 1 });
                }
            }
        }

        foreach (var source in sources)
        {
            if (world.Random.NextInt(1000) >= spread)
            {
                continue;
            }

            var (dx, dy) = Neighbours[world.Random.NextInt(Neighbours.Length)];
            var target = new CellPos(source.X + dx, source.Y + dy);
            if (CanTakeOre(map, target))
            {
                map.SetOre(target, new OreCell(OreKind.Ore, 0));
            }
        }
    }

    /// <summary>
    /// True for a cell new ore may grow on: on the map, clear or rough land, not a cliff, with no ore, building, obstacle, standing map
    /// object or bridge.
    /// </summary>
    public static bool CanTakeOre(MapGrid map, CellPos cell) =>
        map.Contains(cell)
        && map.LandAt(cell) is LandType.Clear or LandType.Rough
        && map.Heights.ShapeAt(cell) != CellShape.Cliff
        && map.OreAt(cell).IsEmpty
        && !map.HasBuilding(cell)
        && !map.IsBlocked(cell)
        && map.BridgeAt(cell) is null
        && !HasStandingObject(map, cell);

    private static bool HasStandingObject(MapGrid map, CellPos cell)
    {
        foreach (var mapObject in map.ObjectsAt(cell))
        {
            if (mapObject.IsAlive)
            {
                return true;
            }
        }

        return false;
    }

    private static int Permille(Fixed fraction) => (int)IntMath.DivRound(fraction.Raw * 1000, Fixed.OneRaw);
}
