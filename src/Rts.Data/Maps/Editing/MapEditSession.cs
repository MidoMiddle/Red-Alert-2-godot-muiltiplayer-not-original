using System;
using System.Collections.Generic;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Math;
using Rts.Simulation.World;

namespace Rts.Data.Maps.Editing;

/// <summary>The tools of the editor (docs/implementation/M7_maps.md, 4.13): brushes of heights and land (step 11), the rest (step 12).</summary>
public enum EditTool
{
    Raise,
    Lower,
    Flatten,
    Smooth,
    Cliff,
    Land,
    Texture,
    Water,
    Road,
    Ore,
    Objects,
    Bridge,
    Building,
    Start,
    Eraser,
}

/// <summary>What the eraser takes away.</summary>
public enum EraseLayer
{
    All,
    Objects,
    Buildings,
    Bridges,
    Ore,
    Starts,
}

/// <summary>
/// The settings of a stroke: the land of the land tool, the material of the texture tool, the depth of the water tool (shallow or deep), the ore value (1–9
/// ore, 10–12 gems), the map object and its density in percent, the bridge and building types, the building's facing, the
/// layer of the eraser.
/// </summary>
public sealed record EditOptions
{
    public LandType Land { get; init; } = LandType.Clear;

    /// <summary>A material of the climate (<c>game/data/climates/&lt;id&gt;/look.yaml</c>): only the look changes.</summary>
    public string Material { get; init; } = "grass";

    public LandType Water { get; init; } = LandType.Shallow;

    public int Ore { get; init; } = 5;

    public string ObjectType { get; init; } = "pine_tree";

    public int Density { get; init; } = 100;

    public string BridgeType { get; init; } = "bridge_wood_low";

    public string BuildingType { get; init; } = "neutral_house_a";

    public int Facing { get; init; }

    public EraseLayer Erase { get; init; } = EraseLayer.All;
}

/// <summary>
/// An editing session (docs/implementation/M7_maps.md, 4.13): the map, its history and the stroke under way. A stroke goes
/// from <see cref="BeginStroke"/> through any number of <see cref="Paint"/> calls to <see cref="EndStroke"/>, which records it
/// as one undo step, its layer values and, when it changed them, the lists of objects, bridges, buildings and starts.
/// Between two dabs of a brush the stroke is filled in along a straight line, so a fast stroke leaves no gaps. Click tools
/// (bridge: press on one shore, let go on the other; building; start) that cannot do what was asked change nothing and say
/// why in <see cref="Message"/>. <see cref="Version"/> grows with every change, so the view knows when to redraw.
/// </summary>
public sealed class MapEditSession
{
    /// <summary>Passes of the smoothing tool at most per dab; enough for any brush of up to 10.</summary>
    private const int SmoothPasses = 32;

    private readonly List<(EditLayer Layer, int Index)> _strokeOrder = new();
    private readonly Dictionary<(EditLayer Layer, int Index), byte> _strokeBefore = new();
    private readonly HashSet<int> _touchedThisStroke = new();
    private EditTool _tool;
    private Brush _brush = new(BrushShape.Circle, 1);
    private EditOptions _options = new();
    private int? _flattenLevel;
    private bool _stroking;
    private int _strokes;
    private MapLists? _listsBefore;
    private (int X, int Y, CellPos Cell)? _first;
    private (int X, int Y, CellPos Cell)? _last;

    public MapEditSession(EditableMap map)
    {
        Map = map;
    }

    public EditableMap Map { get; }

    public UndoStack History { get; } = new();

    public int Version { get; private set; }

    /// <summary>Why the last click tool did nothing, or null when it did what it was asked.</summary>
    public string? Message { get; private set; }

    public void BeginStroke(EditTool tool, Brush brush, LandType land) => BeginStroke(tool, brush, new EditOptions { Land = land });

    public void BeginStroke(EditTool tool, Brush brush, EditOptions? options = null)
    {
        (_tool, _brush, _options, _flattenLevel, _stroking) = (tool, brush, options ?? new EditOptions(), null, true);
        (_first, _last, _listsBefore, Message) = (null, null, null, null);
        _strokeOrder.Clear();
        _strokeBefore.Clear();
        _touchedThisStroke.Clear();
        _strokes++;
    }

    /// <summary>
    /// One dab of the stroke: height tools take the corner nearest the pointer, the others the cell under it. Brush tools fill
    /// in the way from the last dab; click tools only remember where the stroke began and ended.
    /// </summary>
    public void Paint(int cornerX, int cornerY, CellPos cell)
    {
        if (!_stroking)
        {
            throw new InvalidOperationException("Paint outside a stroke.");
        }

        _first ??= (cornerX, cornerY, cell);
        var from = _last;
        _last = (cornerX, cornerY, cell);
        if (_tool is EditTool.Bridge or EditTool.Building or EditTool.Start)
        {
            return;
        }

        if (from is not { } previous)
        {
            Dab(cornerX, cornerY, cell);
            return;
        }

        // The way from the last dab: corners for height tools, cells for the rest, without the dab already made.
        var (x0, y0, x1, y1) = IsHeightTool(_tool)
            ? (previous.X, previous.Y, cornerX, cornerY)
            : (previous.Cell.X, previous.Cell.Y, cell.X, cell.Y);
        var points = Line(x0, y0, x1, y1);
        for (var i = 1; i < points.Count; i++)
        {
            var (x, y) = points[i];
            Dab(x, y, new CellPos(x, y));
        }
    }

    /// <summary>
    /// Ends the stroke: click tools act now; then what it changed becomes one undo step; a stroke that changed nothing leaves no
    /// step.
    /// </summary>
    public void EndStroke()
    {
        if (!_stroking)
        {
            return;
        }

        switch (_tool)
        {
            case EditTool.Bridge when _first is { } a && _last is { } b:
                PlaceBridge(a.Cell, b.Cell);
                break;
            case EditTool.Building when _first is { } at:
                PlaceBuilding(at.Cell);
                break;
            case EditTool.Start when _first is { } at:
                PlaceStart(at.Cell);
                break;
        }

        _stroking = false;
        var changes = new List<LayerChange>();
        foreach (var key in _strokeOrder)
        {
            var after = Map.RawValue(key.Layer, key.Index);
            var before = _strokeBefore[key];
            if (after != before)
            {
                changes.Add(new LayerChange(key.Layer, key.Index, before, after));
            }
        }

        var listsAfter = _listsBefore is null ? null : Map.Lists();
        var listsChanged = _listsBefore is { } listsBefore && !listsBefore.ContentEquals(listsAfter!);
        if (changes.Count > 0 || listsChanged)
        {
            History.Push(new EditRecord(changes, listsChanged ? _listsBefore : null, listsChanged ? listsAfter : null));
        }
    }

    /// <summary>Takes the map to another climate (<see cref="EditableMap.SetClimate"/>); not an undo step, like the time of day.</summary>
    public void SetClimate(string climate)
    {
        if (!ClimateGrounds.IsKnown(climate))
        {
            throw new ArgumentException($"Unknown climate '{climate}'.", nameof(climate));
        }

        if (Map.Climate != climate)
        {
            EndStroke();
            Map.SetClimate(climate);
            Version++;
        }
    }

    /// <summary>Sets the time of day (<see cref="MapLighting.Ids"/>); not an undo step, like the name of the map.</summary>
    public void SetLighting(string lighting)
    {
        if (!MapLighting.IsKnown(lighting))
        {
            throw new ArgumentException($"Unknown lighting '{lighting}'.", nameof(lighting));
        }

        if (Map.Lighting != lighting)
        {
            Map.Lighting = lighting;
            Version++;
        }
    }

    public bool Undo()
    {
        EndStroke();
        return Changed(History.Undo(Map));
    }

    public bool Redo()
    {
        EndStroke();
        return Changed(History.Redo(Map));
    }

    /// <summary>
    /// The map to show: the full map of the rules, or — when an edit made a bridge or a building invalid — the ground and the
    /// map objects without them, so the view never breaks halfway through an edit (<see cref="MapValidator"/> says what is wrong).
    /// </summary>
    public MapGrid ViewGrid()
    {
        var definition = Map.ToDefinition();
        try
        {
            return definition.ToMapGrid();
        }
        catch (ArgumentException)
        {
            return MapChecks.BaseGrid(definition);
        }
    }

    private static bool IsHeightTool(EditTool tool) =>
        tool is EditTool.Raise or EditTool.Lower or EditTool.Flatten or EditTool.Smooth or EditTool.Cliff;

    private void Dab(int cornerX, int cornerY, CellPos cell)
    {
        switch (_tool)
        {
            case EditTool.Raise or EditTool.Lower or EditTool.Cliff:
                var step = _tool switch { EditTool.Raise => 1, EditTool.Lower => -1, _ => 2 };
                foreach (var (x, y) in _brush.Corners(Map, cornerX, cornerY))
                {
                    var index = Map.CornerIndex(x, y);
                    if (_touchedThisStroke.Add(index))
                    {
                        Set(EditLayer.Height, index, (byte)IntMath.Min(IntMath.Max(Map.CornerLevel(x, y) + step, 0), CornerHeightGrid.MaxLevel));
                    }
                }

                break;
            case EditTool.Flatten:
                if (!Map.ContainsCorner(cornerX, cornerY))
                {
                    break;
                }

                _flattenLevel ??= Map.CornerLevel(cornerX, cornerY);
                foreach (var (x, y) in _brush.Corners(Map, cornerX, cornerY))
                {
                    Set(EditLayer.Height, Map.CornerIndex(x, y), (byte)_flattenLevel.Value);
                }

                break;
            case EditTool.Smooth:
                Smooth(_brush.Corners(Map, cornerX, cornerY));
                break;
            case EditTool.Land:
                PaintLand(_brush.Cells(Map, cell), _options.Land);
                break;
            case EditTool.Texture:
                // Only the look: the land of the rules stays (M7 4.13, «Текстура»).
                var material = (byte)Map.CodeFor(_options.Material);
                foreach (var under in _brush.Cells(Map, cell))
                {
                    Set(EditLayer.Texture, Map.CellIndex(under), material);
                }

                break;
            case EditTool.Road:
                // A road is as wide as the brush, square whatever its shape; it stops at water (bridges cross it).
                PaintLand((_brush with { Shape = BrushShape.Square }).Cells(Map, cell).FindAll(c => !IsWater(Map.LandAt(c))), LandType.Road);
                break;
            case EditTool.Water:
                PaintWater(_brush.Cells(Map, cell));
                break;
            case EditTool.Ore:
                var sources = SourceCells();
                foreach (var under in _brush.Cells(Map, cell))
                {
                    if (!IsWaterOrRock(Map.LandAt(under)) && !sources.Contains(under))
                    {
                        Set(EditLayer.Ore, Map.CellIndex(under), (byte)_options.Ore);
                    }
                }

                break;
            case EditTool.Objects:
                PlaceObjects(_brush.Cells(Map, cell));
                break;
            case EditTool.Eraser:
                Erase(_brush.Cells(Map, cell));
                break;
        }
    }

    private void PaintLand(List<CellPos> cells, LandType land)
    {
        var code = (byte)Map.CodeFor(ClimateGrounds.MaterialOf(Map.Climate, land));
        foreach (var under in cells)
        {
            Set(EditLayer.Land, Map.CellIndex(under), (byte)land);
            Set(EditLayer.Texture, Map.CellIndex(under), code);
        }
    }

    /// <summary>Water of the chosen depth under the brush, and a beach on the dry land around it (clear, rough or ice; roads and rock stay).</summary>
    private void PaintWater(List<CellPos> cells)
    {
        var depth = _options.Water == LandType.Deep ? LandType.Deep : LandType.Shallow;
        PaintLand(cells, depth);
        var beach = new List<CellPos>();
        foreach (var under in cells)
        {
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    var next = new CellPos(under.X + dx, under.Y + dy);
                    if (Map.ContainsCell(next) && Map.LandAt(next) is LandType.Clear or LandType.Rough or LandType.Ice)
                    {
                        beach.Add(next);
                    }
                }
            }
        }

        PaintLand(beach, LandType.Beach);
    }

    /// <summary>
    /// Objects of the chosen type on the free cells under the brush, each cell with a chance of the density; which cells and
    /// which model come from a hash of the cell and the stroke number, so the same stroke always gives the same wood. An ore or gem
    /// source takes the ore off its cell: no harvester would reach it there.
    /// </summary>
    private void PlaceObjects(List<CellPos> cells)
    {
        if (!MapObjectTypes.TryGetById(_options.ObjectType, out var type))
        {
            Message = $"unknown map object '{_options.ObjectType}'";
            return;
        }

        var taken = TakenCells();
        foreach (var under in cells)
        {
            var hash = Hash(under, _strokes);
            if (!_touchedThisStroke.Add(Map.CellIndex(under)) || (int)(hash % 100) >= _options.Density || taken.Contains(under)
                || IsWaterOrRock(Map.LandAt(under)))
            {
                continue;
            }

            TouchLists();
            Map.ObjectList.Add(new MapObjectPlacement(_options.ObjectType, under, (int)(hash / 100 % 4)));
            taken.Add(under);
            if (IsSource(type))
            {
                Set(EditLayer.Ore, Map.CellIndex(under), 0);
            }

            Version++;
        }
    }

    /// <summary>Press on one shore, let go on the other, in a straight line with only water between: a bridge over that water.</summary>
    private void PlaceBridge(CellPos from, CellPos to)
    {
        if ((from.X != to.X && from.Y != to.Y) || from == to)
        {
            Message = "a bridge runs along X or along Y from one shore to the other";
            return;
        }

        var (dx, dy) = (IntMath.Sign(to.X - from.X), IntMath.Sign(to.Y - from.Y));
        var start = new CellPos(from.X + dx, from.Y + dy);
        var end = new CellPos(to.X - dx, to.Y - dy);
        if (start == to || IsWater(Map.LandAt(from)) || IsWater(Map.LandAt(to)))
        {
            Message = "press on one shore and let go on the other, with water between";
            return;
        }

        for (var cell = start; ; cell = new CellPos(cell.X + dx, cell.Y + dy))
        {
            if (!IsWater(Map.LandAt(cell)))
            {
                Message = $"cell {cell.X},{cell.Y} between the shores is not water";
                return;
            }

            if (cell == end)
            {
                break;
            }
        }

        var bridge = new BridgePlacement(_options.BridgeType, start, end);
        if (MapChecks.BridgeProblem(MapChecks.BaseGrid(Map.ToDefinition()), bridge, Map.Bridges) is { } problem)
        {
            Message = problem;
            return;
        }

        TouchLists();
        Map.BridgeList.Add(bridge);
        Version++;
    }

    private void PlaceBuilding(CellPos cell)
    {
        var structure = new StructurePlacement(_options.BuildingType, cell, 0, _options.Facing);
        if (MapChecks.BuildingProblem(MapChecks.BaseGrid(Map.ToDefinition()), structure, Map.Structures) is { } problem)
        {
            Message = problem;
            return;
        }

        TouchLists();
        Map.StructureList.Add(structure);
        Version++;
    }

    /// <summary>A start with the lowest free number 1…players on the cell.</summary>
    private void PlaceStart(CellPos cell)
    {
        if (!Map.ContainsCell(cell) || Map.Starts.Count >= Map.Players || Map.StartList.Exists(s => s.Cell == cell))
        {
            Message = Map.Starts.Count >= Map.Players ? $"all {Map.Players} starts are placed" : "there is a start on this cell";
            return;
        }

        var slot = 1;
        while (Map.StartList.Exists(s => s.Slot == slot))
        {
            slot++;
        }

        TouchLists();
        Map.StartList.Add(new MapStart(slot, cell));
        Version++;
    }

    /// <summary>Takes away what the eraser's layer names on the cells under the brush.</summary>
    private void Erase(List<CellPos> cells)
    {
        var under = new HashSet<CellPos>(cells);
        var layer = _options.Erase;
        bool Wants(EraseLayer wanted) => layer == EraseLayer.All || layer == wanted;

        if (Wants(EraseLayer.Ore))
        {
            foreach (var cell in cells)
            {
                Set(EditLayer.Ore, Map.CellIndex(cell), 0);
            }
        }

        var removed = 0;
        if (Wants(EraseLayer.Objects))
        {
            removed += Remove(Map.ObjectList, o => under.Contains(o.Cell));
        }

        if (Wants(EraseLayer.Buildings))
        {
            removed += Remove(Map.StructureList, s => MapChecks.Footprint(s).Exists(under.Contains) || under.Contains(s.Cell));
        }

        if (Wants(EraseLayer.Bridges))
        {
            removed += Remove(Map.BridgeList, b => MapChecks.BridgeCells(b).Exists(under.Contains) || under.Contains(b.From));
        }

        if (Wants(EraseLayer.Starts))
        {
            removed += Remove(Map.StartList, s => under.Contains(s.Cell));
        }

        if (removed > 0)
        {
            Version++;
        }
    }

    private int Remove<T>(List<T> list, Predicate<T> match)
    {
        if (!list.Exists(match))
        {
            return 0;
        }

        TouchLists();
        return list.RemoveAll(match);
    }

    /// <summary>Cells where nothing more can stand: map objects, bridge segments, building footprints.</summary>
    /// <summary>The cells of the ore and gem sources: the ore brush leaves them bare.</summary>
    private HashSet<CellPos> SourceCells()
    {
        var cells = new HashSet<CellPos>();
        foreach (var mapObject in Map.Objects)
        {
            if (MapObjectTypes.TryGetById(mapObject.Type, out var type) && IsSource(type))
            {
                cells.Add(mapObject.Cell);
            }
        }

        return cells;
    }

    private static bool IsSource(MapObjectType type) => type.Kind is MapObjectKind.OreSource or MapObjectKind.GemSource;

    private HashSet<CellPos> TakenCells()
    {
        var taken = new HashSet<CellPos>();
        foreach (var mapObject in Map.Objects)
        {
            taken.Add(mapObject.Cell);
        }

        foreach (var bridge in Map.Bridges)
        {
            taken.UnionWith(MapChecks.BridgeCells(bridge));
        }

        foreach (var structure in Map.Structures)
        {
            taken.UnionWith(MapChecks.Footprint(structure));
        }

        return taken;
    }

    /// <summary>
    /// Brings every corner under the brush within one level of its eight neighbours where it can (or to their mean where it
    /// cannot), pass after pass in row order until nothing moves: a cliff becomes a ramp.
    /// </summary>
    private void Smooth(List<(int X, int Y)> corners)
    {
        for (var pass = 0; pass < SmoothPasses; pass++)
        {
            var moved = false;
            foreach (var (x, y) in corners)
            {
                var (low, high, sum, count) = (int.MinValue, int.MaxValue, 0, 0);
                for (var dy = -1; dy <= 1; dy++)
                {
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        if ((dx != 0 || dy != 0) && Map.ContainsCorner(x + dx, y + dy))
                        {
                            var level = Map.CornerLevel(x + dx, y + dy);
                            (low, high, sum, count) = (IntMath.Max(low, level - 1), IntMath.Min(high, level + 1), sum + level, count + 1);
                        }
                    }
                }

                var current = Map.CornerLevel(x, y);
                var target = low <= high
                    ? IntMath.Min(IntMath.Max(current, low), high)
                    : (int)IntMath.DivRound(sum, count);
                if (target != current)
                {
                    Set(EditLayer.Height, Map.CornerIndex(x, y), (byte)target);
                    moved = true;
                }
            }

            if (!moved)
            {
                break;
            }
        }
    }

    private void TouchLists() => _listsBefore ??= Map.Lists();

    private void Set(EditLayer layer, int index, byte value)
    {
        var current = Map.RawValue(layer, index);
        if (current == value)
        {
            return;
        }

        if (_strokeBefore.TryAdd((layer, index), current))
        {
            _strokeOrder.Add((layer, index));
        }

        Map.SetRaw(layer, index, value);
        Version++;
    }

    private bool Changed(bool changed)
    {
        if (changed)
        {
            Version++;
        }

        return changed;
    }

    private static bool IsWater(LandType land) => land is LandType.Shallow or LandType.Deep;

    private static bool IsWaterOrRock(LandType land) => IsWater(land) || land == LandType.Rock;

    // FNV-1a of the cell and the stroke number: the same stroke gives the same objects on every machine.
    private static uint Hash(CellPos cell, int stroke)
    {
        var hash = 2166136261u;
        foreach (var value in (ReadOnlySpan<int>)[cell.X, cell.Y, stroke])
        {
            for (var shift = 0; shift < 32; shift += 8)
            {
                hash = (hash ^ (uint)((value >> shift) & 0xFF)) * 16777619u;
            }
        }

        return hash;
    }

    // Bresenham from (x0, y0) to (x1, y1), both ends included.
    private static List<(int X, int Y)> Line(int x0, int y0, int x1, int y1)
    {
        var points = new List<(int X, int Y)>();
        var (dx, dy) = (IntMath.Abs(x1 - x0), -IntMath.Abs(y1 - y0));
        var (sx, sy) = (x0 < x1 ? 1 : -1, y0 < y1 ? 1 : -1);
        var error = dx + dy;
        while (true)
        {
            points.Add((x0, y0));
            if (x0 == x1 && y0 == y1)
            {
                return points;
            }

            var doubled = 2 * error;
            if (doubled >= dy)
            {
                error += dy;
                x0 += sx;
            }

            if (doubled <= dx)
            {
                error += dx;
                y0 += sy;
            }
        }
    }
}
