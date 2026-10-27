using System.Collections.Generic;
using Rts.Data.Maps;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Math;
using Rts.Simulation.World;

namespace Rts.MapGen.Stages;

/// <summary>
/// The dressing of a map by its climate (2026-10-25: the objects of the element documents), after the town and before the forest:
/// <list type="bullet">
/// <item>the town (urban): street things by the roads — benches, bins, mailboxes, bus stops, signs, flowerbeds, lamps beside the road,
/// phone booths, billboards, traffic lights and stalls a cell further — and a harbour by the water: warehouses, cranes, containers;</item>
/// <item>farms (temperate, and fewer in the snow): a farmhouse, a barn, a silo, a windmill or a water tower, a well, hay bales, a
/// tractor and a hedge;</item>
/// <item>villages (desert): adobe houses round a well; ruins, mesas and an arch; dry shrubs;</item>
/// <item>beaches (every climate but the snow, by the water): umbrellas, loungers, showers, nets, driftwood, boats, cabins, a bar,
/// lifeguard towers, a boat shed and a lighthouse — on the coast twice the buildings and more than twice the things;</item>
/// <item>a resort on the coast (the owner, 2026-10-25: «a couple of big buildings, the rest small resort houses»): a hotel a little
/// way in from the sea, with its images, and round it beach cabins, a café and kiosks;</item>
/// <item>barbed wire across the open ground (snow, desert).</item>
/// </list>
/// Every piece goes with its images under the symmetry, or not at all: inside the playable area, on clear ground (or the beach),
/// off roads, cliffs, bridges and what stands already, away from the bases; a thing off the ore zone (the ore and what the drills
/// throw), a building <see cref="GenMap.OreClearance"/> cells clear of it. What stops movers keeps a cell off the roads, so
/// no diagonal step of a road closes.
/// </summary>
internal static class Dressing
{
    private static readonly string[] StreetThings = ["bench", "trash_bin", "mailbox", "bus_stop", "road_sign", "flowerbed", "lamp_post"];
    private static readonly string[] StreetBlockers = ["phone_booth", "billboard", "traffic_light", "street_stall"];
    private static readonly string[] BeachThings =
        ["beach_umbrella", "sun_lounger", "beach_umbrella", "beach_shower", "volleyball_net", "driftwood", "fishing_nets", "beached_boat"];
    private static readonly string[] BeachBuildings = ["neutral_beach_cabin", "neutral_beach_bar", "neutral_lifeguard_tower", "neutral_boat_shed"];
    private static readonly string[] Harbour = ["neutral_port_warehouse", "neutral_container_stack", "neutral_port_crane", "neutral_container_stack"];

    public static void Place(GenMap map)
    {
        var site = new Site(map);
        var climate = map.Settings.Climate;
        if (climate == ClimateGrounds.Urban)
        {
            Street(map, site);
            Shore(map, site, Harbour, [], 6, 0);
        }

        if (climate == ClimateGrounds.Temperate || climate == ClimateGrounds.Winter)
        {
            Farms(map, site, climate == ClimateGrounds.Winter ? 1 : 3);
        }

        if (climate == ClimateGrounds.Desert)
        {
            Villages(map, site);
        }

        if (climate == ClimateGrounds.Coast)
        {
            Resort(map, site);
            // The slopes down to the sea leave few flat cells at the water: the buildings may stand a little further inland.
            Shore(map, site, BeachBuildings, BeachThings, 10, 64, reach: 4);
        }
        else if (climate != ClimateGrounds.Winter)
        {
            Shore(map, site, BeachBuildings, BeachThings, 5, 28);
        }

        if (climate == ClimateGrounds.Winter || climate == ClimateGrounds.Desert)
        {
            Wire(map, site);
        }
    }

    // Street things on the cells beside a road, blockers a cell further.
    private static void Street(GenMap map, Site site)
    {
        var (things, blockers) = (0, 0);
        foreach (var (order, cell) in Candidates(map, 1, cell => !GenMap.IsRoad(map.LandAt(cell)) && Roads(map, cell, 2)))
        {
            if (order % 5 != 0)
            {
                continue;
            }

            if (Roads(map, cell, 1) && things < 36)
            {
                things += site.Object(StreetThings[order / 5 % StreetThings.Length], cell, order % 4, blocking: false) ? 1 : 0;
            }
            else if (!Roads(map, cell, 1) && blockers < 14)
            {
                blockers += site.Object(StreetBlockers[order / 5 % StreetBlockers.Length], cell, 0, blocking: true) ? 1 : 0;
            }
        }
    }

    // Buildings by the shore — up to reach cells from the water — and things on the beach.
    private static void Shore(GenMap map, Site site, string[] buildings, string[] things, int buildingCount, int thingCount, int reach = 2)
    {
        var placed = 0;
        // On islands the land at sea level is the landings at the feet of the ramps and the islets: no building there.
        var islands = map.Settings.Water == WaterKind.Islands;
        foreach (var (order, cell) in Candidates(map, 2, cell => !map.IsWater(cell) && Water(map, cell, reach) && !Water(map, cell, 0)
                                                                 && (!islands || map.Corner(cell.X, cell.Y) > 0)))
        {
            if (placed >= buildingCount)
            {
                break;
            }

            placed += site.Structure(buildings[order % buildings.Length], cell, order % 8) ? 1 : 0;
        }

        // A lighthouse a beach has once — not the landing of an island; on the coast, where the shore slopes, a cell further up too.
        if (things.Length > 0 && !islands)
        {
            var coast = map.Settings.Climate == ClimateGrounds.Coast;
            foreach (var (_, cell) in Candidates(map, 3, cell => coast
                ? map.LandAt(cell) is LandType.Beach or LandType.Clear && Water(map, cell, 2)
                : map.LandAt(cell) == LandType.Beach && Water(map, cell, 1)))
            {
                if (site.Structure("neutral_lighthouse", cell, 0))
                {
                    break;
                }
            }
        }

        var count = 0;
        foreach (var (order, cell) in Candidates(map, 4, cell => map.LandAt(cell) == LandType.Beach))
        {
            if (count >= thingCount)
            {
                break;
            }

            if (order % 4 == 0)
            {
                var id = things[order / 4 % things.Length];
                // A boat on the sand stops everyone: none on the landing of an island.
                if (!(islands && id == "beached_boat"))
                {
                    count += site.Object(id, cell, order % 4, blocking: id == "beached_boat") ? 1 : 0;
                }
            }
        }
    }

    // A resort: a hotel three to ten cells from the water, and small houses round it.
    private static void Resort(GenMap map, Site site)
    {
        CellPos? hotel = null;
        foreach (var (_, cell) in Candidates(map, 10, cell => map.LandAt(cell) == LandType.Clear && Water(map, cell, 10) && !Water(map, cell, 3)))
        {
            if (site.Structure("neutral_hotel", cell, 0))
            {
                hotel = cell;
                break;
            }
        }

        if (hotel is not { } at)
        {
            return;
        }

        var small = new[] { "neutral_beach_cabin", "neutral_beach_cabin", "neutral_kiosk", "neutral_beach_cabin", "neutral_cafe", "neutral_beach_cabin" };
        var placed = 0;
        foreach (var (order, cell) in Candidates(map, 11, cell =>
        {
            var (dx, dy) = (cell.X - at.X - 1, cell.Y - at.Y);
            return map.LandAt(cell) is LandType.Clear or LandType.Beach && dx * dx + dy * dy >= 9 && dx * dx + dy * dy <= 64;
        }))
        {
            if (placed >= 12)
            {
                break;
            }

            placed += order % 3 == 0 && site.Structure(small[placed % small.Length], cell, order % 8) ? 1 : 0;
        }
    }

    // Farms: a farmhouse and what goes round it.
    private static void Farms(GenMap map, Site site, int farms)
    {
        var made = 0;
        foreach (var (order, cell) in Candidates(map, 5, cell => map.LandAt(cell) == LandType.Clear && Roads(map, cell, 8) && !Roads(map, cell, 2)))
        {
            if (made >= farms)
            {
                break;
            }

            if (!site.Structure("neutral_farmhouse", cell, 0))
            {
                continue;
            }

            made++;
            var (x, y) = (cell.X, cell.Y);
            site.Structure("neutral_barn", new CellPos(x + 3, y), 0);
            site.Structure(order % 2 == 0 ? "neutral_windmill" : "neutral_water_tower", new CellPos(x - 3, y + 1), 0);
            site.Structure("neutral_silo", new CellPos(x + 3, y + 3), 0);
            site.Object("well", new CellPos(x, y + 3), 0, blocking: true);
            site.Object("farm_tractor", new CellPos(x + 1, y + 3), 0, blocking: true);
            site.Object("hay_bale", new CellPos(x + 5, y + 3), 0, blocking: true);
            site.Object("hay_bale", new CellPos(x + 6, y + 3), 1, blocking: true);
            site.Object("hay_bale", new CellPos(x + 5, y + 4), 2, blocking: true);
            for (var i = -2; i <= 5; i++)
            {
                site.Object("hedge", new CellPos(x + i, y - 2), 0, blocking: true);
            }
        }
    }

    // Villages of adobe round a well; ruins, rocks and dry shrubs about the desert.
    private static void Villages(GenMap map, Site site)
    {
        var made = 0;
        foreach (var (_, cell) in Candidates(map, 6, cell => map.LandAt(cell) == LandType.Clear && Roads(map, cell, 7) && !Roads(map, cell, 2)))
        {
            if (made >= 3)
            {
                break;
            }

            if (!site.Object("well", cell, 0, blocking: true))
            {
                continue;
            }

            made++;
            foreach (var (dx, dy) in new[] { (-3, -3), (2, -3), (-3, 2), (2, 2) })
            {
                site.Structure("neutral_adobe_house", new CellPos(cell.X + dx, cell.Y + dy), 0);
            }
        }

        var rocks = new[] { "neutral_ancient_ruins", "neutral_rock_mesa", "neutral_rock_mesa", "neutral_ancient_ruins", "neutral_rock_arch", "neutral_rock_mesa" };
        var placed = 0;
        foreach (var (order, cell) in Candidates(map, 7, cell => map.LandAt(cell) is LandType.Clear or LandType.Rough && !Roads(map, cell, 3)))
        {
            if (placed >= rocks.Length)
            {
                break;
            }

            placed += site.Structure(rocks[placed], cell, order % 8) ? 1 : 0;
        }

        var shrubs = 0;
        foreach (var (order, cell) in Candidates(map, 8, cell => map.LandAt(cell) is LandType.Clear or LandType.Rough))
        {
            if (shrubs >= 50)
            {
                break;
            }

            shrubs += order % 9 == 0 && site.Object("dry_shrub", cell, order % 4, blocking: false) ? 1 : 0;
        }
    }

    // Three short lines of barbed wire across the open ground.
    private static void Wire(GenMap map, Site site)
    {
        var lines = 0;
        foreach (var (order, cell) in Candidates(map, 9, cell => map.LandAt(cell) == LandType.Clear && !Roads(map, cell, 3)))
        {
            if (lines >= 3)
            {
                break;
            }

            var along = order % 2 == 0;
            var cells = new[] { cell, along ? new CellPos(cell.X + 1, cell.Y) : new CellPos(cell.X, cell.Y + 1), along ? new CellPos(cell.X + 2, cell.Y) : new CellPos(cell.X, cell.Y + 2) };
            if (System.Array.TrueForAll(cells, one => site.Free(one, blocking: true)))
            {
                foreach (var one in cells)
                {
                    site.Object("barbed_wire", one, 0, blocking: true);
                }

                lines++;
            }
        }
    }

    // The canonical cells that pass the test, in an order of the seed (sub picks a stream of it).
    private static List<(int Order, CellPos Cell)> Candidates(GenMap map, int sub, System.Func<CellPos, bool> test)
    {
        var candidates = new List<(int Order, CellPos Cell)>();
        for (var y = 1; y < map.N - 1; y++)
        {
            for (var x = 1; x < map.N - 1; x++)
            {
                var cell = new CellPos(x, y);
                if (map.Symmetry.CanonicalCell(cell) == cell && map.Area.Contains(cell) && test(cell))
                {
                    candidates.Add(((int)(IntNoise.Hash(x, y, map.Seed, Channels.Dressing * 16 + sub) % 100_000), cell));
                }
            }
        }

        candidates.Sort((a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : a.Cell.Y != b.Cell.Y ? a.Cell.Y.CompareTo(b.Cell.Y) : a.Cell.X.CompareTo(b.Cell.X));
        return candidates;
    }

    // Whether a road lies within reach cells of the cell (0: the cell is a road).
    private static bool Roads(GenMap map, CellPos cell, int reach) => Any(map, cell, reach, near => GenMap.IsRoad(map.LandAt(near)));

    // Whether water lies within reach cells of the cell.
    private static bool Water(GenMap map, CellPos cell, int reach) => Any(map, cell, reach, map.IsWater);

    private static bool Any(GenMap map, CellPos cell, int reach, System.Func<CellPos, bool> test)
    {
        for (var dy = -reach; dy <= reach; dy++)
        {
            for (var dx = -reach; dx <= reach; dx++)
            {
                var near = new CellPos(cell.X + dx, cell.Y + dy);
                if (map.Contains(near) && test(near))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>What stands where: the cells taken, and the ground for the checks of the buildings.</summary>
    private sealed class Site
    {
        private readonly GenMap _map;
        private readonly MapGrid _ground;
        private readonly HashSet<CellPos> _taken = [];

        public Site(GenMap map)
        {
            _map = map;
            _ground = MapChecks.BaseGrid(map.ToDefinition("work", ""), map.Types);
            foreach (var structure in map.Structures)
            {
                _taken.UnionWith(MapChecks.Footprint(structure, map.Types));
            }

            foreach (var mapObject in map.Objects)
            {
                _taken.Add(mapObject.Cell);
            }

            foreach (var bridge in map.Bridges)
            {
                _taken.UnionWith(MapChecks.BridgeCells(bridge));
            }

            // On islands and by the sea an engineer lands by an oil derrick: the cells round it stay free.
            if (map.Settings.Water is WaterKind.Islands or WaterKind.Sea)
            {
                foreach (var structure in map.Structures)
                {
                    if (structure.Type == "tech_oil_derrick")
                    {
                        for (var dy = -3; dy <= 4; dy++)
                        {
                            for (var dx = -3; dx <= 4; dx++)
                            {
                                _taken.Add(new CellPos(structure.Cell.X + dx, structure.Cell.Y + dy));
                            }
                        }
                    }
                }
            }
        }

        /// <summary>Whether a thing may stand on the cell: what stops movers keeps a cell off the roads.</summary>
        public bool Free(CellPos cell, bool blocking) =>
            _map.Contains(cell) && _map.Area.Contains(cell) && _map.LandAt(cell) is LandType.Clear or LandType.Rough or LandType.Beach
            && !_map.IsCliff(cell) && !_taken.Contains(cell) && !_map.NearOre(cell, 0)
            && _map.StartDistanceSquared(cell) > (Starts.FlatRadius + 2) * (Starts.FlatRadius + 2)
            && (!blocking || !Roads(_map, cell, 1));

        /// <summary>The object on the cell and its images, when every one of them is free.</summary>
        public bool Object(string id, CellPos cell, int variant, bool blocking)
        {
            var images = new List<CellPos>();
            for (var k = 0; k < _map.Symmetry.Order; k++)
            {
                var image = _map.Symmetry.Cell(k, cell);
                if (!images.Contains(image))
                {
                    images.Add(image);
                }
            }

            if (!images.TrueForAll(image => Free(image, blocking)))
            {
                return false;
            }

            foreach (var image in images)
            {
                _map.Objects.Add(new MapObjectPlacement(id, image, variant));
                _taken.Add(image);
            }

            return true;
        }

        /// <summary>The building with its north-west cell on <paramref name="corner"/> and its images, when every one fits.</summary>
        public bool Structure(string id, CellPos corner, int facing)
        {
            var images = new List<StructurePlacement>();
            for (var k = 0; k < _map.Symmetry.Order; k++)
            {
                // The north-west cell of the turned footprint is its smallest corner after the turn.
                var (minX, minY) = (int.MaxValue, int.MaxValue);
                foreach (var cell in MapChecks.Footprint(new StructurePlacement(id, corner, 0, 0), _map.Types))
                {
                    var image = _map.Symmetry.Cell(k, cell);
                    (minX, minY) = (IntMath.Min(minX, image.X), IntMath.Min(minY, image.Y));
                }

                var placement = new StructurePlacement(id, new CellPos(minX, minY), 0, facing);
                if (!images.Exists(other => other.Cell == placement.Cell))
                {
                    images.Add(placement);
                }
            }

            var placed = new List<StructurePlacement>(_map.Structures);
            foreach (var placement in images)
            {
                var footprint = MapChecks.Footprint(placement, _map.Types);
                // A building blocks: none of its cells by a road, as for a blocking thing, nor near the ore (GenMap.OreClearance).
                if (!footprint.TrueForAll(cell => Free(cell, blocking: true) && !_map.NearOre(cell, GenMap.OreClearance))
                    || MapChecks.BuildingProblem(_ground, placement, placed) is not null)
                {
                    return false;
                }

                placed.Add(placement);
            }

            foreach (var placement in images)
            {
                _map.Structures.Add(placement);
                _taken.UnionWith(MapChecks.Footprint(placement, _map.Types));
            }

            return true;
        }
    }
}
