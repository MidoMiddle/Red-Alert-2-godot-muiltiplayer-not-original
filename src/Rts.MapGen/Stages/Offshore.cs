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
/// <item>a ferry off the coast, fishing boats off the beaches — on every climate but the snow, where the sea freezes;</item>
/// <item>reef rocks off the coast (the coast climate), ice floes on the sea of the snow.</item>
/// </list>
/// Every piece stops ships, and so goes where it closes no way: all of it on water, and a ring of water a cell wide round it that
/// nothing else stands on — a fleet goes round. It goes with its images under the symmetry or not at all; a building whose images
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

    public static void Place(GenMap map)
    {
        if (map.Settings.Water is not (WaterKind.Sea or WaterKind.Islands))
        {
            return;
        }

        var sea = new Sea(map);
        var climate = map.Settings.Climate;
        var frozen = climate == ClimateGrounds.Winter;
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
