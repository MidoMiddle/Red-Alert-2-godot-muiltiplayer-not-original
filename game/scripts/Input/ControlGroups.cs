using System;
using System.Collections.Generic;
using Rts.Simulation.World;

namespace Rts.Client.Controls;

/// <summary>
/// The control groups 0…9 (docs/implementation/M2_unit_control.md, 4.14): Ctrl + digit assigns the selection, the digit
/// selects the group, Shift + digit adds it, the same digit twice within <see cref="DoubleTapMs"/> also moves the camera to
/// the group. A unit can be in several groups. Client state only; the simulation never sees it.
/// </summary>
public sealed class ControlGroups
{
    public const int Count = 10;

    /// <summary>Milliseconds within which a second press of the same digit is a double tap.</summary>
    public const ulong DoubleTapMs = DoubleTap.WindowMs;

    private readonly List<EntityId>[] _groups = new List<EntityId>[Count];
    private readonly DoubleTap _taps = new();

    public ControlGroups()
    {
        for (var i = 0; i < Count; i++)
        {
            _groups[i] = new List<EntityId>();
        }
    }

    /// <summary>Makes <paramref name="units"/> the group <paramref name="digit"/>; the old members leave it.</summary>
    public void Assign(int digit, IEnumerable<EntityId> units)
    {
        var group = _groups[CheckDigit(digit)];
        group.Clear();
        foreach (var unit in units)
        {
            if (!group.Contains(unit))
            {
                group.Add(unit);
            }
        }
    }

    /// <summary>The members of the group that are still alive, in the order they were assigned. The dead leave the group.</summary>
    public IReadOnlyList<EntityId> Members(int digit, Func<EntityId, bool> isAlive)
    {
        var group = _groups[CheckDigit(digit)];
        group.RemoveAll(unit => !isAlive(unit));
        return group;
    }

    /// <summary>
    /// A press of the digit to select the group at <paramref name="nowMs"/>. True when it is the second press of the same
    /// digit within <see cref="DoubleTapMs"/>; a third press starts over.
    /// </summary>
    public bool Tap(int digit, ulong nowMs)
    {
        CheckDigit(digit);
        return _taps.Press(digit, nowMs);
    }

    private static int CheckDigit(int digit) =>
        digit is >= 0 and < Count ? digit : throw new ArgumentOutOfRangeException(nameof(digit), digit, "A group is a digit 0…9.");
}
