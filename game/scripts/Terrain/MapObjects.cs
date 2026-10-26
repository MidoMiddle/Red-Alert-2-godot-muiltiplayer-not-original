using System.Collections.Generic;
using System.Linq;
using Godot;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;

namespace Rts.Client.Terrain;

/// <summary>
/// Symbolic map objects on the ground (docs/implementation/conventions.md, section 12; M7_maps.md, 4.7): a tree in the shape of
/// the climate's trees (<see cref="TreeShapes"/>: a pine, a broadleaf tree, a palm or a cactus) and a short trunk for its stump, a grey lump for a boulder, a thin brown rail for a fence, a low steel
/// block for an anti-tank hedgehog, a thin dark pipe for a pipeline (ore and gem sources: <see cref="OreSources"/>), a tall
/// stone block for a wall, a dark pole with a yellow lamp for a lamp post, a grey block for every rock cell, and for a low
/// bridge a plank deck just above the water with a rail on each side, or charred stubs where a segment fell; a high bridge of concrete
/// with an asphalt deck at its level (<see cref="AddHighBridge"/>). One MultiMesh
/// per shape (and per direction for bridges). A snowdrift is a bush in summer and a dune in the desert. Rebuilt whenever the map changes
/// (a bridge blown up, the editor) or its climate does (the editor), which is rare. An object destroyed in a match (a lamp post or a
/// fence crushed, a tree felled) is not a rebuild — on a big map that took tens of milliseconds every time a tank drove down a street —
/// but its copies hide and a felled tree's stump shows (<see cref="MapGrid.ObjectDeaths"/>).
/// </summary>
public partial class MapObjects : Node3D
{
    private const float RockHeight = 0.5f;
    private const float TrunkHeight = 0.25f;
    private const float CrownHeight = 0.8f;

    // A copy hidden: a MultiMesh draws nothing of an instance scaled to nothing.
    private static readonly Transform3D Hidden = new(new Basis(Vector3.Zero, Vector3.Zero, Vector3.Zero), Vector3.Zero);

    private MapGrid _map = null!;

    // The copies of each map object by its id (gone when it is destroyed) and the stump of each tree (shown when it is felled).
    private readonly Dictionary<int, List<(MultiMesh Mesh, int Index, Transform3D Place)>> _copies = [];
    private readonly Dictionary<int, List<(MultiMesh Mesh, int Index, Transform3D Place)>> _stumps = [];
    private bool[] _shownDead = [];
    private int _builtDeaths = -1;

    // A point of a shape and the map object it stands for (0: none — a rock, a bridge, a tree of the skirt).
    private readonly record struct Spot(Vector3 At, int Object);
    private int _builtVersion = -1;
    private MapGrid? _builtMap;
    private Climates.ClimateLook? _builtClimate;

    public override void _Process(double delta)
    {
        // A new match or a new map in the editor (another map object), or a change of this one.
        var map = GetParent<TerrainBuilder>().Map;
        var climate = GetParent<TerrainBuilder>().Climate;
        if (!ReferenceEquals(map, _builtMap) || map.LayoutVersion != _builtVersion || !ReferenceEquals(climate, _builtClimate))
        {
            _map = map;
            _builtClimate = climate;
            Rebuild();
        }
        else if (map.ObjectDeaths != _builtDeaths)
        {
            ShowDeaths();
        }
    }

    /// <summary>Hides the copies of the objects destroyed since the last look and shows the stumps of the felled trees.</summary>
    private void ShowDeaths()
    {
        _builtDeaths = _map.ObjectDeaths;
        foreach (var mapObject in _map.Objects)
        {
            if (!mapObject.Destroyed || _shownDead[mapObject.Id - 1])
            {
                continue;
            }

            _shownDead[mapObject.Id - 1] = true;
            if (_copies.TryGetValue(mapObject.Id, out var copies))
            {
                foreach (var (mesh, index, _) in copies)
                {
                    mesh.SetInstanceTransform(index, Hidden);
                }
            }

            if (_stumps.TryGetValue(mapObject.Id, out var stumps))
            {
                foreach (var (mesh, index, place) in stumps)
                {
                    mesh.SetInstanceTransform(index, place);
                }
            }
        }
    }

    private void Rebuild()
    {
        foreach (var child in GetChildren())
        {
            child.QueueFree();
        }

        _builtMap = _map;
        _builtVersion = _map.LayoutVersion;
        _copies.Clear();
        _stumps.Clear();
        var kind = _builtClimate?.Trees ?? Climates.TreeKind.Pine;
        var trees = new List<(Vector3 At, int Variant, int Object)>();
        var stumps = new List<Spot>();
        var boulders = new List<Spot>();
        var fences = new List<Spot>();
        var walls = new List<Spot>();
        var posts = new List<Spot>();
        var rocks = new List<Spot>();
        var hedgehogs = new List<Spot>();
        var pipes = new List<Spot>();
        var drifts = new List<Spot>();
        var props = new SortedDictionary<string, List<Spot>>(System.StringComparer.Ordinal);
        for (var y = 0; y < _map.Height; y++)
        {
            for (var x = 0; x < _map.Width; x++)
            {
                if (_map.LandAt(new CellPos(x, y)) == LandType.Rock)
                {
                    rocks.Add(new Spot(Ground(x, y), 0));
                }
            }
        }

        // Every object, standing or not: the copies of the destroyed ones hide below (ShowDeaths), as they will in the match.
        foreach (var mapObject in _map.Objects)
        {
            var spot = new Spot(Ground(mapObject.Cell.X, mapObject.Cell.Y), mapObject.Id);
            var list = mapObject.Kind switch
            {
                MapObjectKind.PineTree => stumps,
                MapObjectKind.Boulder => boulders,
                MapObjectKind.FenceWood => fences,
                MapObjectKind.WallStone => walls,
                MapObjectKind.LampPost => posts,
                MapObjectKind.AntiTankHedgehog => hedgehogs,
                MapObjectKind.Pipeline => pipes,
                MapObjectKind.Snowdrift => drifts,
                // Drawn and animated by OreSources.
                MapObjectKind.OreSource or MapObjectKind.GemSource => null,
                // The props of the element documents (PropLooks).
                _ when PropLooks.Known(mapObject.Type.Id) => PropsOf(props, mapObject.Type.Id),
                _ => throw new System.ArgumentOutOfRangeException(nameof(mapObject), mapObject.Kind, "No symbolic look for this kind."),
            };
            list?.Add(spot);
            if (mapObject.Kind == MapObjectKind.PineTree)
            {
                trees.Add((spot.At, mapObject.Variant, mapObject.Id));
            }
        }

        // The pines beyond the edge (MapSkirt): only a picture, the rules never see them.
        foreach (var (x, z) in GetParent<TerrainBuilder>().Skirt.Trees)
        {
            trees.Add((new Vector3(x, WorldCoords.Terrain!.HeightAt(x, z), z), (int)(System.MathF.Abs(x * 7f + z * 13f) % 4), 0));
        }

        AddTrees(kind, trees);

        foreach (var bridge in _map.Bridges)
        {
            AddBridge(bridge);
        }

        AddInstances("Rocks", new BoxMesh { Size = new Vector3(0.98f, RockHeight, 0.98f) }, new Color(0.45f, 0.45f, 0.48f), rocks, RockHeight / 2);
        AddInstances("Stumps", new CylinderMesh { TopRadius = 0.08f, BottomRadius = 0.09f, Height = 0.08f }, new Color(0.42f, 0.3f, 0.18f), stumps, 0.04f, slots: _stumps);
        // The models of the table by the object's id (docs/art/model_standards.md), the primitives without one.
        AddInstances("Boulders", new SphereMesh { Radius = 0.36f, Height = 0.5f }, new Color(0.5f, 0.5f, 0.52f), boulders, 0.2f, "object/boulder");
        AddInstances("Fences", new BoxMesh { Size = new Vector3(0.96f, 0.25f, 0.06f) }, new Color(0.5f, 0.35f, 0.2f), fences, 0.125f, "object/fence_wood");
        AddInstances("Walls", new BoxMesh { Size = new Vector3(0.98f, 0.6f, 0.98f) }, new Color(0.68f, 0.65f, 0.58f), walls, 0.3f, "object/wall_stone");
        if (!AddInstances("LampPosts", new CylinderMesh { TopRadius = 0.025f, BottomRadius = 0.03f, Height = 0.7f }, new Color(0.15f, 0.15f, 0.17f), posts, 0.35f, "object/lamp_post"))
        {
            AddInstances("Lamps", new SphereMesh { Radius = 0.06f, Height = 0.12f }, new Color(1f, 0.85f, 0.35f), posts, 0.72f);
        }

        AddInstances("Hedgehogs", new BoxMesh { Size = new Vector3(0.9f, 0.3f, 0.9f) }, new Color(0.3f, 0.31f, 0.34f), hedgehogs, 0.15f, "object/anti_tank_hedgehog");
        AddInstances("Pipes", new BoxMesh { Size = new Vector3(0.98f, 0.12f, 0.12f) }, new Color(0.22f, 0.25f, 0.28f), pipes, 0.06f, "object/pipeline");
        // A snowdrift of the map is a bush in summer and in town, a dune in the desert.
        var drift = kind switch
        {
            Climates.TreeKind.Broadleaf or Climates.TreeKind.Coast => new Color(0.24f, 0.4f, 0.2f),
            Climates.TreeKind.Desert => new Color(0.85f, 0.74f, 0.52f),
            _ => new Color(0.9f, 0.93f, 0.97f),
        };
        AddInstances("Snowdrifts", new SphereMesh { Radius = 0.45f, Height = 0.22f }, drift, drifts, 0.11f, "object/snowdrift");
        foreach (var (id, points) in props)
        {
            AddProps(id, points);
        }

        // The stumps stand hidden until their trees fall; the destroyed objects of the map hide now.
        foreach (var copies in _stumps.Values)
        {
            foreach (var (mesh, index, _) in copies)
            {
                mesh.SetInstanceTransform(index, Hidden);
            }
        }

        _shownDead = new bool[_map.Objects.Count];
        ShowDeaths();
    }

    /// <summary>
    /// The deck of a standing segment lies flat just above the water across all its cells (three tanks side by side), so units on it
    /// seem to drive on the planks; the rails run along its outer edges.
    /// </summary>
    private void AddBridge(Bridge bridge)
    {
        if (bridge.Type.High)
        {
            AddHighBridge(bridge);
            return;
        }

        // A bridge of one cell runs across the cells beside it.
        var alongX = bridge.From != bridge.To ? bridge.From.Y == bridge.To.Y : !System.Linq.Enumerable.Any(bridge.Segments[0].Cells, cell => cell.X != bridge.From.X);
        var decks = new List<Spot>();
        var rails = new List<Spot>();
        var stubs = new List<Spot>();
        var side = alongX ? new Vector3(0, 0, 0.44f) : new Vector3(0.44f, 0, 0);
        foreach (var segment in bridge.Segments)
        {
            // The outer cells of the span across, for the rails.
            var (first, last) = (segment.Cell, segment.Cell);
            foreach (var cell in segment.Cells)
            {
                decks.Add(new Spot(Ground(cell.X, cell.Y), 0));
                (first, last) = alongX
                    ? (cell.Y < first.Y ? cell : first, cell.Y > last.Y ? cell : last)
                    : (cell.X < first.X ? cell : first, cell.X > last.X ? cell : last);
            }

            if (!segment.IsIntact)
            {
                decks.RemoveRange(decks.Count - segment.Cells.Length, segment.Cells.Length);
                stubs.Add(new Spot(Ground(first.X, first.Y) - side, 0));
                stubs.Add(new Spot(Ground(last.X, last.Y) + side, 0));
                continue;
            }

            rails.Add(new Spot(Ground(first.X, first.Y) - side, 0));
            rails.Add(new Spot(Ground(last.X, last.Y) + side, 0));
        }

        var name = $"Bridge{bridge.Id}";
        var deck = alongX ? new Vector3(1f, 0.05f, 0.9f) : new Vector3(0.9f, 0.05f, 1f);
        var rail = alongX ? new Vector3(1f, 0.1f, 0.04f) : new Vector3(0.04f, 0.1f, 1f);
        AddInstances(name + "Deck", new BoxMesh { Size = deck }, new Color(0.55f, 0.4f, 0.24f), decks, 0.03f);
        AddInstances(name + "Rails", new BoxMesh { Size = rail }, new Color(0.38f, 0.26f, 0.15f), rails, 0.1f);
        AddInstances(name + "Stubs", new BoxMesh { Size = new Vector3(0.12f, 0.12f, 0.12f) }, new Color(0.2f, 0.16f, 0.13f), stubs, 0.02f);
    }

    // The colours of a high bridge (owner 2026-10-26: "the bridge is concrete, asphalt on top").
    private static readonly Color Asphalt = new(0.2f, 0.2f, 0.22f);
    private static readonly Color Marking = new(0.86f, 0.85f, 0.78f);
    private static readonly Color Concrete = new(0.7f, 0.69f, 0.66f);
    private static readonly Color Rubble = new(0.55f, 0.54f, 0.52f);

    private const float DeckThickness = 0.1f;
    private const float BeamDepth = 0.28f;

    /// <summary>
    /// A high bridge (docs/implementation/M7_maps.md, 4.6): an asphalt deck at its level with a light dashed line along the middle,
    /// concrete curbs along its outer edges, a concrete beam under every cell and concrete piers from the ground up under every third
    /// segment. A fallen segment leaves concrete rubble on the ground below and the stumps of its piers.
    /// </summary>
    private void AddHighBridge(Bridge bridge)
    {
        var alongX = bridge.From.Y == bridge.To.Y;
        var height = _map.DeckLevel(bridge.Segments[0].Cell) * TerrainHeights.LevelHeight;
        var side = alongX ? new Vector3(0, 0, 0.46f) : new Vector3(0.46f, 0, 0);
        var decks = new List<Spot>();
        var marks = new List<Spot>();
        var curbs = new List<Spot>();
        var beams = new List<Spot>();
        var rubble = new List<Spot>();
        var piers = new List<Transform3D>();
        foreach (var segment in bridge.Segments)
        {
            var (first, last) = (segment.Cell, segment.Cell);
            foreach (var cell in segment.Cells)
            {
                (first, last) = alongX
                    ? (cell.Y < first.Y ? cell : first, cell.Y > last.Y ? cell : last)
                    : (cell.X < first.X ? cell : first, cell.X > last.X ? cell : last);
            }

            var end = segment.Index == 0 || segment.Index == bridge.Segments.Count - 1;
            var pier = !end && segment.Index % 3 == 2;
            if (!segment.IsIntact)
            {
                foreach (var cell in segment.Cells)
                {
                    rubble.Add(new Spot(Ground(cell.X, cell.Y), 0));
                }

                if (pier)
                {
                    piers.Add(Pier(first, -side, Ground(first.X, first.Y).Y + 0.45f));
                    piers.Add(Pier(last, side, Ground(last.X, last.Y).Y + 0.45f));
                }

                continue;
            }

            foreach (var cell in segment.Cells)
            {
                var top = new Vector3(cell.X + 0.5f, height, cell.Y + 0.5f);
                decks.Add(new Spot(top, 0));
                beams.Add(new Spot(top, 0));
            }

            // The middle lane is the first cell of a segment; every other cell of it gets a dash.
            if (segment.Index % 2 == 0)
            {
                marks.Add(new Spot(new Vector3(segment.Cell.X + 0.5f, height, segment.Cell.Y + 0.5f), 0));
            }

            curbs.Add(new Spot(new Vector3(first.X + 0.5f, height, first.Y + 0.5f) - side, 0));
            curbs.Add(new Spot(new Vector3(last.X + 0.5f, height, last.Y + 0.5f) + side, 0));
            if (pier)
            {
                piers.Add(Pier(first, -side, height - DeckThickness - BeamDepth));
                piers.Add(Pier(last, side, height - DeckThickness - BeamDepth));
            }
        }

        var name = $"Bridge{bridge.Id}";
        var across = (Vector3 size) => alongX ? size : new Vector3(size.Z, size.Y, size.X);
        AddInstances(name + "Deck", new BoxMesh { Size = new Vector3(1f, DeckThickness, 1f) }, Asphalt, decks, -DeckThickness / 2);
        AddInstances(name + "Marks", new BoxMesh { Size = across(new Vector3(0.5f, 0.01f, 0.07f)) }, Marking, marks, 0.005f);
        AddInstances(name + "Curbs", new BoxMesh { Size = across(new Vector3(1f, 0.14f, 0.08f)) }, Concrete, curbs, 0.07f);
        AddInstances(name + "Beams", new BoxMesh { Size = across(new Vector3(1f, BeamDepth, 0.8f)) }, Concrete, beams, -DeckThickness - BeamDepth / 2);
        AddInstances(name + "Rubble", new BoxMesh { Size = new Vector3(0.55f, 0.16f, 0.45f) }, Rubble, rubble, 0.08f);
        if (piers.Count > 0)
        {
            var mesh = LowPoly.Of(new BoxMesh { Size = Vector3.One });
            mesh.Material = new StandardMaterial3D { AlbedoColor = Concrete };
            var multiMesh = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = mesh, InstanceCount = piers.Count };
            for (var i = 0; i < piers.Count; i++)
            {
                multiMesh.SetInstanceTransform(i, piers[i]);
            }

            AddChild(new MultiMeshInstance3D { Name = name + "Piers", Multimesh = multiMesh });
        }
    }

    // A square concrete pier under the outer edge of a cell, from the ground up to <paramref name="top"/>: a unit box stretched.
    private static Transform3D Pier(CellPos cell, Vector3 outward, float top)
    {
        var foot = Ground(cell.X, cell.Y) + outward * 0.5f;
        var tall = Mathf.Max(0.05f, top - foot.Y + 0.1f);
        return new Transform3D(Basis.Identity.Scaled(new Vector3(0.32f, tall, 0.32f)), new Vector3(foot.X, foot.Y - 0.1f + tall / 2, foot.Z));
    }

    private static Vector3 Ground(int x, int y) => new(x + 0.5f, WorldCoords.Terrain!.HeightAt(x + 0.5f, y + 0.5f), y + 0.5f);

    /// <summary>
    /// The trees of the map and the skirt: the models of a shape from the table (<c>tree/&lt;shape&gt;</c>, <see cref="TreeShapes.NameOf"/>),
    /// each tree the kind, size and turn of its cell, one MultiMesh for every mesh of every kind; or else every part of the shape of
    /// <see cref="TreeShapes"/> one MultiMesh.
    /// </summary>
    private void AddTrees(Climates.TreeKind kind, List<(Vector3 At, int Variant, int Object)> trees)
    {
        var parts = new Dictionary<string, (TreePart Part, List<Transform3D> Places, List<int> Objects)>();
        var modelled = new Dictionary<string, List<Spot>>();
        foreach (var (at, variant, id) in trees)
        {
            var shape = TreeShapes.NameOf(kind, variant);
            if (Models.ModelLibrary.Find("tree/" + shape) is not null)
            {
                if (!modelled.TryGetValue(shape, out var points))
                {
                    points = [];
                    modelled[shape] = points;
                }

                points.Add(new Spot(at, id));
                continue;
            }

            foreach (var part in TreeShapes.Of(kind, variant))
            {
                var key = part.Key + "/" + part.Color.ToHtml();
                if (!parts.TryGetValue(key, out var entry))
                {
                    entry = (part, [], []);
                    parts[key] = entry;
                }

                entry.Places.Add(new Transform3D(Basis.FromEuler(new Vector3(0, Mathf.DegToRad(part.YawDegrees), 0)), at + part.Offset));
                entry.Objects.Add(id);
            }
        }

        foreach (var (key, (part, places, objects)) in parts)
        {
            var mesh = LowPoly.Of(part.Mesh());
            mesh.Material = new StandardMaterial3D { AlbedoColor = part.Color };
            var multiMesh = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = mesh, InstanceCount = places.Count };
            for (var i = 0; i < places.Count; i++)
            {
                multiMesh.SetInstanceTransform(i, places[i]);
                Remember(_copies, objects[i], multiMesh, i, places[i]);
            }

            AddChild(new MultiMeshInstance3D { Name = "Trees" + part.Key + parts.Keys.ToList().IndexOf(key), Multimesh = multiMesh });
        }

        foreach (var (shape, points) in modelled)
        {
            AddModelInstances("TreeModel" + shape, Models.ModelLibrary.Instances("tree/" + shape, null, PointsOf(points))!, points, _copies);
        }
    }

    private static List<Spot> PropsOf(SortedDictionary<string, List<Spot>> props, string id)
    {
        if (!props.TryGetValue(id, out var points))
        {
            points = [];
            props[id] = points;
        }

        return points;
    }

    /// <summary>
    /// The props of one kind: its model of the table (<c>object/&lt;id&gt;</c>) when there is one for the climate, or else a MultiMesh
    /// for every part of its look (<see cref="PropLooks"/>) in the civilian colours.
    /// </summary>
    private void AddProps(string id, List<Spot> points)
    {
        if (Models.ModelLibrary.Instances("object/" + id, _builtClimate?.Id, PointsOf(points)) is { } groups)
        {
            AddModelInstances("Prop" + id, groups, points, _copies);
            return;
        }

        var parts = PropLooks.Of(id);
        for (var p = 0; p < parts.Count; p++)
        {
            var (mesh, place) = BuildingLookView.MeshOf(parts[p], BuildingLookView.ColorOf(parts[p].Tint, LookFaction.Civilian));
            var multiMesh = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = mesh, InstanceCount = points.Count };
            for (var i = 0; i < points.Count; i++)
            {
                var placed = new Transform3D(Basis.Identity, points[i].At) * place;
                multiMesh.SetInstanceTransform(i, placed);
                Remember(_copies, points[i].Object, multiMesh, i, placed);
            }

            AddChild(new MultiMeshInstance3D { Name = "Prop" + id + p, Multimesh = multiMesh });
        }
    }

    /// <summary>A MultiMesh for every mesh of the copies of a model (<see cref="Models.ModelLibrary.Instances"/>).</summary>
    private void AddModelInstances(
        string name,
        List<(Mesh Mesh, List<Transform3D> Places, List<int> Points)> groups,
        List<Spot> spots,
        Dictionary<int, List<(MultiMesh Mesh, int Index, Transform3D Place)>> slots)
    {
        for (var g = 0; g < groups.Count; g++)
        {
            var (mesh, places, points) = groups[g];
            var multiMesh = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = mesh, InstanceCount = places.Count };
            for (var i = 0; i < places.Count; i++)
            {
                multiMesh.SetInstanceTransform(i, places[i]);
                Remember(slots, spots[points[i]].Object, multiMesh, i, places[i]);
            }

            AddChild(new MultiMeshInstance3D { Name = name + g, Multimesh = multiMesh });
        }
    }

    /// <summary>
    /// One MultiMesh of a kind of map object: its model of the table (<paramref name="key"/>, pivot on the ground) when there is one
    /// for the climate — a MultiMesh for every mesh of every kind of it, each copy the kind, size and turn of its cell — or else the
    /// primitive raised by <paramref name="lift"/>. True for a model.
    /// </summary>
    private bool AddInstances(
        string name,
        PrimitiveMesh mesh,
        Color color,
        List<Spot> positions,
        float lift,
        string? key = null,
        Dictionary<int, List<(MultiMesh Mesh, int Index, Transform3D Place)>>? slots = null)
    {
        slots ??= _copies;
        if (positions.Count == 0)
        {
            return false;
        }

        if (key is not null && Models.ModelLibrary.Instances(key, _builtClimate?.Id, PointsOf(positions)) is { } groups)
        {
            AddModelInstances(name + "Model", groups, positions, slots);
            return true;
        }

        mesh = LowPoly.Of(mesh);
        mesh.Material = new StandardMaterial3D { AlbedoColor = color };
        var multiMesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = mesh,
            InstanceCount = positions.Count,
        };
        for (var i = 0; i < positions.Count; i++)
        {
            var place = new Transform3D(Basis.Identity, positions[i].At + new Vector3(0, lift, 0));
            multiMesh.SetInstanceTransform(i, place);
            Remember(slots, positions[i].Object, multiMesh, i, place);
        }

        AddChild(new MultiMeshInstance3D { Name = name, Multimesh = multiMesh });
        return false;
    }

    private static List<Vector3> PointsOf(List<Spot> spots) => spots.ConvertAll(spot => spot.At);

    // A copy of a map object (none for 0), to hide or show when it is destroyed.
    private static void Remember(Dictionary<int, List<(MultiMesh Mesh, int Index, Transform3D Place)>> slots, int id, MultiMesh mesh, int index, Transform3D place)
    {
        if (id == 0)
        {
            return;
        }

        if (!slots.TryGetValue(id, out var copies))
        {
            copies = [];
            slots[id] = copies;
        }

        copies.Add((mesh, index, place));
    }
}
