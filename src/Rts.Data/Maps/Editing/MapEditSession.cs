using System;
using System.Collections.Generic;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Math;

namespace Rts.Data.Maps.Editing;

/// <summary>The tools of step 11 (docs/implementation/M7_maps.md, 4.13); the others come in step 12.</summary>
public enum EditTool
{
    Raise,
    Lower,
    Flatten,
    Smooth,
    Land,
}

/// <summary>
/// An editing session (docs/implementation/M7_maps.md, 4.13): the map, its history and the stroke under way. A stroke goes
/// from <see cref="BeginStroke"/> through any number of <see cref="Paint"/> calls to <see cref="EndStroke"/>, which records it
/// as one undo step. <see cref="Version"/> grows with every change, so the view knows when to redraw.
/// </summary>
public sealed class MapEditSession
{
    /// <summary>Passes of the smoothing tool at most per dab; enough for any brush of up to 10.</summary>
    private const int SmoothPasses = 32;

    private readonly List<(EditLayer Layer, int Index)> _strokeOrder = new();
    private readonly Dictionary<(EditLayer Layer, int Index), byte> _strokeBefore = new();
    private readonly HashSet<int> _raisedThisStroke = new();
    private EditTool _tool;
    private Brush _brush = new(BrushShape.Circle, 1);
    private LandType _land;
    private int? _flattenLevel;
    private bool _stroking;

    public MapEditSession(EditableMap map)
    {
        Map = map;
    }

    public EditableMap Map { get; }

    public UndoStack History { get; } = new();

    public int Version { get; private set; }

    public void BeginStroke(EditTool tool, Brush brush, LandType land = LandType.Clear)
    {
        (_tool, _brush, _land, _flattenLevel, _stroking) = (tool, brush, land, null, true);
        _strokeOrder.Clear();
        _strokeBefore.Clear();
        _raisedThisStroke.Clear();
    }

    /// <summary>
    /// One dab of the stroke: height tools take the corner nearest the pointer, the land tool the cell under it. Raise and
    /// lower change each corner once per stroke; flatten levels to the corner the stroke began on.
    /// </summary>
    public void Paint(int cornerX, int cornerY, CellPos cell)
    {
        if (!_stroking)
        {
            throw new InvalidOperationException("Paint outside a stroke.");
        }

        switch (_tool)
        {
            case EditTool.Raise or EditTool.Lower:
                var step = _tool == EditTool.Raise ? 1 : -1;
                foreach (var (x, y) in _brush.Corners(Map, cornerX, cornerY))
                {
                    var index = Map.CornerIndex(x, y);
                    if (_raisedThisStroke.Add(index))
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
                var code = (byte)Map.CodeFor(TestMapConverter.MaterialOf(_land));
                foreach (var under in _brush.Cells(Map, cell))
                {
                    Set(EditLayer.Land, Map.CellIndex(under), (byte)_land);
                    Set(EditLayer.Texture, Map.CellIndex(under), code);
                }

                break;
        }
    }

    /// <summary>Ends the stroke: what it changed becomes one undo step; a stroke that changed nothing leaves no step.</summary>
    public void EndStroke()
    {
        if (!_stroking)
        {
            return;
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

        if (changes.Count > 0)
        {
            History.Push(new EditRecord(changes));
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
    /// The map to show: the full map of the rules, or — when an edit made a bridge or a building invalid — the layers and the
    /// map objects without them, so the view never breaks halfway through an edit.
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
            var land = new byte[Map.Width * Map.Height];
            for (var i = 0; i < land.Length; i++)
            {
                land[i] = (byte)definition.Land[i];
            }

            var objects = new List<MapObjectState>();
            foreach (var placement in definition.Objects)
            {
                if (MapObjectTypes.TryGetById(placement.Type, out var type))
                {
                    objects.Add(MapObjectState.New(type.Kind, placement.Cell, placement.Variant));
                }
            }

            return MapGrid.FromLayers(Map.Width, Map.Height, land, new byte[land.Length], definition.Heights.AsSpan(), objects);
        }
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
}
