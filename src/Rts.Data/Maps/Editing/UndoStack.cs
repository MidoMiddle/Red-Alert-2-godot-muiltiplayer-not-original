using System.Collections.Generic;

namespace Rts.Data.Maps.Editing;

/// <summary>What an edit changes: a corner height, or the land or texture of a cell.</summary>
public enum EditLayer : byte
{
    Height,
    Land,
    Texture,
}

/// <summary>One value of one layer: the index of the corner or cell, before and after.</summary>
public readonly record struct LayerChange(EditLayer Layer, int Index, byte Before, byte After);

/// <summary>One stroke of the editor (from pressing the button to letting it go): every value it changed, in order.</summary>
public sealed record EditRecord(IReadOnlyList<LayerChange> Changes);

/// <summary>
/// Undo and redo of the editor (docs/implementation/M7_maps.md, 4.13): one record per stroke, at most <see cref="Depth"/>; the
/// oldest goes first. A new stroke clears what could be redone.
/// </summary>
public sealed class UndoStack
{
    public const int Depth = 200;

    private readonly LinkedList<EditRecord> _done = new();
    private readonly Stack<EditRecord> _undone = new();

    public int UndoCount => _done.Count;

    public int RedoCount => _undone.Count;

    public void Push(EditRecord record)
    {
        _done.AddLast(record);
        if (_done.Count > Depth)
        {
            _done.RemoveFirst();
        }

        _undone.Clear();
    }

    /// <summary>Takes back the last stroke; false when there is none.</summary>
    public bool Undo(EditableMap map)
    {
        if (_done.Last is not { } last)
        {
            return false;
        }

        _done.RemoveLast();
        var changes = last.Value.Changes;
        for (var i = changes.Count - 1; i >= 0; i--)
        {
            map.SetRaw(changes[i].Layer, changes[i].Index, changes[i].Before);
        }

        _undone.Push(last.Value);
        return true;
    }

    /// <summary>Does the last undone stroke again; false when there is none.</summary>
    public bool Redo(EditableMap map)
    {
        if (!_undone.TryPop(out var record))
        {
            return false;
        }

        foreach (var change in record.Changes)
        {
            map.SetRaw(change.Layer, change.Index, change.After);
        }

        _done.AddLast(record);
        return true;
    }
}
