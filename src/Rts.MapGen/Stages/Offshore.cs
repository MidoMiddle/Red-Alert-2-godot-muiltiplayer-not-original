using System.Collections.Generic;
using Rts.Data.Maps;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Math;

namespace Rts.MapGen.Stages;

/// <summary>
/// What stands in the sea (docs/naval_elements; the owner, 2026-11-01: «reefs and floes, platforms and ships»), on the maps of the sea
/// and of islands:
/// <list type="bullet">
/// <item>a cargo ship off every quay (<see cref="Harbour"/>), or fishing boats where the turn of the symmetry would lay the ship
/// across;</item>
/// <item>an offshore platform out in the open sea, its income as an oil derrick's, taken by an engineer a helicopter sets down;</item>
/// <item>a ferry off the coast, fishing boats off the beaches and buoys marking the swimmers' water — on every climate but the snow,
/// where the sea freezes;</item>
/// <item>reef rocks off the coast (the coast climate), ice floes on the sea of the snow;</item>
/// <item>the beach's own (docs/beach_elements, 14–16), on every climate but the snow: wooden piers out from the beaches, a slipway on
/// the sand beside a pier, and breakwaters, short chains of stone blocks along the coast a little off a beach.</item>
/// </list>
/// Every piece but the buoy and the slipway stops ships, and so goes where it closes no way (the buoy keeps the same room, so ships
/// pass it too): all of it on water, and a ring of water a cell wide round it that nothing else stands on — a fleet goes round; a
/// pier has its ring on both sides and open water beyond its end, its root on the beach. It goes with its images under the symmetry or not at all; a building whose images
/// would lie across (a long ship under a quarter turn) does not go. Nothing within the bay of an oil derrick on its islet, where
/// an engineer lands, nor before a quay.
/// </summary>
internal static class Offshore
{
    // How far out the open sea of a platform lies: no land within this many cells of it.
    private const int OpenSea = 4;

    // How near a ferry and the fishing boats keep to the land, and how far the reefs keep from it.
    private const int Inshore = 4;
    private const int ReefOffshore = 2;

    // How far off a beach the buoys float, at most.
    private const int BuoyOff = 3;

    // How many cells a pier runs out into the water, and how far apart the roots of two piers keep at least.
    private const int PierLength = 4;
    private const int PierSpacing = 8;

    // How far off a beach a breakwater lies: no nearer than the first, no farther than the second; the blocks of one.
    private const int BreakwaterNear = 3;
    private const int BreakwaterFar = 5;
    private const int BreakwaterBlocks = 3;

    /// <summary>What stands in the sea; with <paramref name="pieces"/> only some of it (a map that has the rest already).</summary>
    public static void Place(GenMap map, SeaPieces pieces = SeaPieces.All)
    {
        if (map.Settings.Water is not (WaterKind.Sea or WaterKind.Islands))
        {
            return;
        }

        var sea = new Sea(map);
        var climate = map.Settings.Climate;
        var frozen = climate == ClimateGrounds.Winter;
        if (pieces != SeaPieces.All)
        {
            if (!frozen)
            {
                (pieces == SeaPieces.Buoys ? (System.Action<GenMap, Sea>)Buoys : Beach)(map, sea);
            }

            return;
        }

        foreach (var quay in map.Quays)
        {
            // Off the wall, a cell of water between: the cargo ship lies along it, else two fishing boats.
            var along = quay.AlongX != 0;
            if (!(along && sea.Building("neutral_cargo_ship", Corner(quay, 2, -2, 4), 0)))
            {
                sea.Building("neutral_fishing_boat", quay.Cell(2, -2), 0);
                sea.Building("neutral_fishing_boat", quay.Cell(6, -2), 0);
            }
        }

        sea.Place(1, 1, cell => sea.Clear(cell, OpenSea), cell => sea.Building("tech_offshore_platform", cell, 0));
        if (!frozen)
        {
            sea.Place(2, 1, cell => sea.Near(cell, Inshore, LandType.Clear, LandType.Beach), cell => sea.Building("neutral_ferry", cell, 0));
            sea.Place(3, 2, cell => sea.Near(cell, Inshore, LandType.Beach), cell => sea.Building("neutral_fishing_boat", cell, 0));
            Beach(map, sea);
            Buoys(map, sea);
        }

        if (climate == ClimateGrounds.Coast)
        {
            sea.Place(4, map.N / 24, cell => sea.Clear(cell, ReefOffshore), cell => sea.Object("reef_rocks", cell, cell.X + cell.Y));
        }

        if (frozen)
        {
            sea.Place(5, map.N / 12, cell => sea.Clear(cell, 1), cell => sea.Object("ice_floe", cell, cell.X + cell.Y));
        }
    }

    // Buoys a little off the beaches, marking the swimmers' water; they stop nobody.
    private static void Buoys(GenMap map, Sea sea) =>
        sea.Place(6, map.N / 16, cell => sea.Near(cell, BuoyOff, LandType.Beach) && !sea.Near(cell, 1, LandType.Beach, LandType.Clear),
            cell => sea.Object("buoy", cell, cell.X + cell.Y));

    // The beach's own (docs/beach_elements, 14–16): the piers, each with a slipway beside it where the sand has room, and the
    // breakwaters. The piers first, so the buoys of a map float round them.
    private static void Beach(GenMap map, Sea sea)
    {
        sea.Place(7, IntMath.Max(1, map.N / 48), cell => Root(map, cell) is not null, cell => sea.Pier(cell, Root(map, cell)!.Value, PierLength, PierSpacing));
        sea.Place(8, IntMath.Max(1, map.N / 64),
            cell => sea.Near(cell, BreakwaterFar, LandType.Beach) && !sea.Near(cell, BreakwaterNear - 1, LandType.Beach, LandType.Clear),
            cell => sea.Chain("breakwater", cell, AlongCoast(map, cell), BreakwaterBlocks));
    }

    // The way from the beach out to sea for a pier rooted on the cell beside it: the one side of a water cell that is beach, with dry
    // land behind it, the water ahead; null for any other cell.
    private static (int Dx, int Dy)? Root(GenMap map, CellPos cell)
    {
        (int, int)? found = null;
        foreach (var (dx, dy) in (System.ReadOnlySpan<(int, int)>)[(1, 0), (-1, 0), (0, 1), (0, -1)])
        {
            var (shore, behind, ahead) = (new CellPos(cell.X - dx, cell.Y - dy), new CellPos(cell.X - 2 * dx, cell.Y - 2 * dy), new CellPos(cell.X + dx, cell.Y + dy));
            if (!map.Contains(behind) || !map.Contains(ahead))
            {
                continue;
            }

            if (map.LandAt(shore) == LandType.Beach && !map.IsWater(behind) && map.IsWater(ahead))
            {
                if (found is not null)
                {
                    return null;
                }

                found = (dx, dy);
            }
            else if (!map.IsWater(shore))
            {
                return null;
            }
        }

        return found;
    }

    // Along the coast at the cell: across the way to the nearest beach.
    private static (int Dx, int Dy) AlongCoast(GenMap map, CellPos cell)
    {
        for (var reach = 1; reach <= BreakwaterFar; reach++)
        {
            for (var d = -reach; d <= reach; d++)
            {
                foreach (var (x, y, alongX) in (System.ReadOnlySpan<(int, int, bool)>)[(d, -reach, true), (d, reach, true), (-reach, d, false), (reach, d, false)])
                {
                    var near = new CellPos(cell.X + x, cell.Y + y);
                    if (map.Contains(near) && map.LandAt(near) == LandType.Beach)
                    {
                        return alongX ? (1, 0) : (0, 1);
                    }
                }
            }
        }

        return (1, 0);
    }

    // The north-west cell of a ship <paramref name="length"/> cells along the wall of a quay from (u, v) on.
    private static CellPos Corner(Quay quay, int u, int v, int length)
    {
        var (first, last) = (quay.Cell(u, v), quay.Cell(u + length - 1, v));
        return new CellPos(IntMath.Min(first.X, last.X), IntMath.Min(first.Y, last.Y));
    }

    /// <summary>The water and what stands on it: the cells taken, and the rings that stay open.</summary>
    private sealed class Sea
    {
        private readonly GenMap _map;
        private readonly HashSet<CellPos> _taken = [];

        public Sea(GenMap map)
        {
            _map = map;
            foreach (var structure in map.Structures)
            {
                _taken.UnionWith(MapChecks.Footprint(structure, map.Types));
                // The bay of an oil derrick on its islet: an engineer lands there.
                if (structure.Type == "tech_oil_derrick")
                {
                    Square(structure.Cell, 4);
                }
            }

            foreach (var mapObject in map.Objects)
            {
                _taken.Add(mapObject.Cell);
            }

            foreach (var bridge in map.Bridges)
            {
                _taken.UnionWith(MapChecks.BridgeCells(bridge));
            }

            // Before a quay, for its ship and the way to it.
            foreach (var quay in map.Quays)
            {
                for (var v = -Harbour.Front - 2; v < 0; v++)
                {
                    for (var u = -2; u < Harbour.Length + 2; u++)
                    {
                        foreach (var image in map.Symmetry.Images(quay.Cell(u, v)))
                        {
                            _reserved.Add(image);
                        }
                    }
                }
            }
        }

        // The water before the quays: only their own ships.
        private readonly HashSet<CellPos> _reserved = [];

        private void Square(CellPos cell, int reach)
        {
            for (var dy = -reach; dy <= reach + 1; dy++)
            {
                for (var dx = -reach; dx <= reach + 1; dx++)
                {
                    _taken.Add(new CellPos(cell.X + dx, cell.Y + dy));
                }
            }
        }

        /// <summary>
        /// Up to <paramref name="count"/> pieces (each with its images) on the canonical cells of water that pass the test, in an order of
        /// the seed (<paramref name="stream"/> picks a stream of it).
        /// </summary>
        public void Place(int stream, int count, System.Func<CellPos, bool> test, System.Func<CellPos, bool> put)
        {
            var candidates = new List<(int Order, CellPos Cell)>();
            for (var y = 1; y < _map.N - 1; y++)
            {
                for (var x = 1; x < _map.N - 1; x++)
                {
                    var cell = new CellPos(x, y);
                    if (_map.Symmetry.CanonicalCell(cell) == cell && _map.Area.Contains(cell) && _map.IsWater(cell) && !_reserved.Contains(cell) && test(cell))
                    {
                        candidates.Add(((int)(IntNoise.Hash(x, y, _map.Seed, Channels.Offshore * 16 + stream) % 100_000), cell));
                    }
                }
            }

            candidates.Sort((a, b) => a.Order != b.Order ? a.Order.CompareTo(b.Order) : a.Cell.Y != b.Cell.Y ? a.Cell.Y.CompareTo(b.Cell.Y) : a.Cell.X.CompareTo(b.Cell.X));
            var placed = 0;
            foreach (var (_, cell) in candidates)
            {
                if (placed >= count)
                {
                    break;
                }

                placed += put(cell) ? 1 : 0;
            }
        }

        /// <summary>Whether all within <paramref name="reach"/> cells of the cell is water.</summary>
        public bool Clear(CellPos cell, int reach)
        {
            for (var dy = -reach; dy <= reach; dy++)
            {
                for (var dx = -reach; dx <= reach; dx++)
                {
                    var near = new CellPos(cell.X + dx, cell.Y + dy);
                    if (!_map.Contains(near) || !_map.IsWater(near))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>Whether land of one of the kinds lies within <paramref name="reach"/> cells of the cell.</summary>
        public bool Near(CellPos cell, int reach, params LandType[] lands)
        {
            for (var dy = -reach; dy <= reach; dy++)
            {
                for (var dx = -reach; dx <= reach; dx++)
                {
                    var near = new CellPos(cell.X + dx, cell.Y + dy);
                    if (_map.Contains(near) && System.Array.IndexOf(lands, _map.LandAt(near)) >= 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>The object on the cell and its images, when each fits.</summary>
        public bool Object(string id, CellPos cell, int variant)
        {
            var images = new List<List<CellPos>>();
            foreach (var image in _map.Symmetry.Images(cell))
            {
                images.Add([image]);
            }

            if (!Fits(images))
            {
                return false;
            }

            foreach (var image in Distinct(images))
            {
                _map.Objects.Add(new MapObjectPlacement(id, image[0], ((variant % 4) + 4) % 4));
                _taken.Add(image[0]);
            }

            return true;
        }

        /// <summary>A chain of <paramref name="count"/> objects from the cell on along (dx, dy), and its images, when each fits.</summary>
        public bool Chain(string id, CellPos cell, (int Dx, int Dy) along, int count)
        {
            var images = new List<List<CellPos>>();
            for (var k = 0; k < _map.Symmetry.Order; k++)
            {
                var image = new List<CellPos>(count);
                for (var i = 0; i < count; i++)
                {
                    image.Add(_map.Symmetry.Cell(k, new CellPos(cell.X + along.Dx * i, cell.Y + along.Dy * i)));
                }

                images.Add(image);
            }

            if (!Fits(images))
            {
                return false;
            }

            foreach (var image in Distinct(images))
            {
                foreach (var block in image)
                {
                    _map.Objects.Add(new MapObjectPlacement(id, block, ((block.X + block.Y) % 4 + 4) % 4));
                    _taken.Add(block);
                }
            }

            return true;
        }

        /// <summary>
        /// A pier from the water cell <paramref name="root"/> on (beside the beach) out along <paramref name="way"/>, <paramref name="length"/>
        /// cells, and its images, when each fits: the water on both sides of it and beyond its end free, a ring as round the other pieces,
        /// and three cells of open water beyond the end, its root <paramref name="spacing"/> cells or more from another pier's. A slipway goes
        /// on the sand two cells to one side of its root, where there is one.
        /// </summary>
        public bool Pier(CellPos root, (int Dx, int Dy) way, int length, int spacing)
        {
            var images = new List<(List<CellPos> Deck, CellPos Shore)>();
            for (var k = 0; k < _map.Symmetry.Order; k++)
            {
                var deck = new List<CellPos>(length);
                for (var i = 0; i < length; i++)
                {
                    deck.Add(_map.Symmetry.Cell(k, new CellPos(root.X + way.Dx * i, root.Y + way.Dy * i)));
                }

                images.Add((deck, _map.Symmetry.Cell(k, new CellPos(root.X - way.Dx, root.Y - way.Dy))));
            }

            var distinct = new List<(List<CellPos> Deck, CellPos Shore)>();
            var cells = new HashSet<CellPos>();
            foreach (var image in images)
            {
                if (distinct.Exists(other => other.Deck[0] == image.Deck[0]))
                {
                    continue;
                }

                if (!PierFits(image.Deck, image.Shore, cells)
                    || _map.Bridges.Exists(other => IntMath.Max(IntMath.Abs(other.From.X - image.Deck[0].X), IntMath.Abs(other.From.Y - image.Deck[0].Y)) < spacing))
                {
                    return false;
                }

                distinct.Add(image);
                cells.UnionWith(image.Deck);
            }

            foreach (var (deck, shore) in distinct)
            {
                _map.Bridges.Add(new BridgePlacement("pier_wood", deck[0], deck[^1]));
                _taken.UnionWith(deck);
                var (dx, dy) = (deck[1].X - deck[0].X, deck[1].Y - deck[0].Y);
                foreach (var side in (System.ReadOnlySpan<int>)[2, -2])
                {
                    var sand = new CellPos(shore.X + dy * side, shore.Y + dx * side);
                    var water = new CellPos(sand.X + dx, sand.Y + dy);
                    if (_map.Contains(sand) && _map.Contains(water) && _map.LandAt(sand) == LandType.Beach && _map.IsWater(water)
                        && !_taken.Contains(sand) && _map.Ore[sand.Y * _map.N + sand.X] == 0)
                    {
                        _map.Objects.Add(new MapObjectPlacement("slipway", sand, 0));
                        _taken.Add(sand);
                        break;
                    }
                }
            }

            return true;
        }

        // The deck on water in the playable area, its root by the shore, nothing taken on it nor round it but the beach behind; three
        // cells of water beyond its end, and none of it beside another image's.
        private bool PierFits(List<CellPos> deck, CellPos shore, HashSet<CellPos> others)
        {
            var (dx, dy) = (deck[1].X - deck[0].X, deck[1].Y - deck[0].Y);
            var tip = deck[^1];
            if (!_map.Contains(shore) || _map.LandAt(shore) != LandType.Beach || _taken.Contains(shore) || !Clear(new CellPos(tip.X + 2 * dx, tip.Y + 2 * dy), 1))
            {
                return false;
            }

            for (var i = 0; i < deck.Count; i++)
            {
                var cell = deck[i];
                if (!_map.Area.Contains(cell) || !_map.IsWater(cell) || _reserved.Contains(cell))
                {
                    return false;
                }

                for (var a = -1; a <= 1; a++)
                {
                    for (var b = (i == 0 ? 0 : -1); b <= 1; b++)
                    {
                        // a across, b along: the cells beside, and before the root only the beach.
                        var ring = new CellPos(cell.X + dy * a + dx * b, cell.Y + dx * a + dy * b);
                        if (!_map.Contains(ring) || !_map.IsWater(ring) || _taken.Contains(ring) || others.Contains(ring))
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        /// <summary>The building with its north-west cell on <paramref name="corner"/> and its images, when each fits and none lies across.</summary>
        public bool Building(string id, CellPos corner, int facing)
        {
            var footprint = MapChecks.Footprint(new StructurePlacement(id, corner, 0, facing), _map.Types);
            var images = new List<List<CellPos>>();
            for (var k = 0; k < _map.Symmetry.Order; k++)
            {
                var image = footprint.ConvertAll(cell => _map.Symmetry.Cell(k, cell));
                var (minX, minY) = (int.MaxValue, int.MaxValue);
                image.ForEach(cell => (minX, minY) = (IntMath.Min(minX, cell.X), IntMath.Min(minY, cell.Y)));
                var own = MapChecks.Footprint(new StructurePlacement(id, new CellPos(minX, minY), 0, facing), _map.Types);
                if (!own.TrueForAll(image.Contains))
                {
                    return false;
                }

                images.Add(own);
            }

            if (!Fits(images))
            {
                return false;
            }

            foreach (var image in Distinct(images))
            {
                _map.Structures.Add(new StructurePlacement(id, image[0], 0, facing));
                _taken.UnionWith(image);
            }

            return true;
        }

        // The images that differ (an image the symmetry maps onto itself is one piece).
        private static List<List<CellPos>> Distinct(List<List<CellPos>> images)
        {
            var distinct = new List<List<CellPos>>();
            foreach (var image in images)
            {
                if (!distinct.Exists(other => other[0] == image[0]))
                {
                    distinct.Add(image);
                }
            }

            return distinct;
        }

        // Every image on water in the playable area, on nothing taken, its ring water with nothing on it; the images apart or one.
        private bool Fits(List<List<CellPos>> images)
        {
            var cells = new HashSet<CellPos>();
            foreach (var image in Distinct(images))
            {
                foreach (var cell in image)
                {
                    if (!_map.Area.Contains(cell) || !_map.IsWater(cell) || _taken.Contains(cell))
                    {
                        return false;
                    }

                    for (var dy = -1; dy <= 1; dy++)
                    {
                        for (var dx = -1; dx <= 1; dx++)
                        {
                            var ring = new CellPos(cell.X + dx, cell.Y + dy);
                            if (!_map.Contains(ring) || !_map.IsWater(ring) || _taken.Contains(ring) || (cells.Contains(ring) && !image.Contains(ring)))
                            {
                                return false;
                            }
                        }
                    }
                }

                foreach (var cell in image)
                {
                    if (!cells.Add(cell))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}

/// <summary>Which of what stands in the sea the generator lays (<see cref="MapGenerator.AddSea"/>): all of it, or one part for a map that has the rest.</summary>
public enum SeaPieces
{
    All,

    /// <summary>The buoys off the beaches (docs/beach_elements/13_buoy).</summary>
    Buoys,

    /// <summary>The piers, slipways and breakwaters (docs/beach_elements, 14–16).</summary>
    Beach,
}
