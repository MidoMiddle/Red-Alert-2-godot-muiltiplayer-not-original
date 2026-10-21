using System;

namespace Rts.Client.Controls;

/// <summary>
/// The camera bookmarks F1…F4 (docs/implementation/M2_unit_control.md, 4.14): Ctrl + F remembers the ground point in the
/// middle of the view, F moves the camera back there. They last until the match ends; client state only.
/// </summary>
public sealed class CameraBookmarks
{
    public const int Count = 4;

    private readonly (float X, float Z)?[] _points = new (float X, float Z)?[Count];

    /// <summary>Remembers the point (map cells) under bookmark <paramref name="slot"/> 1…4.</summary>
    public void Save(int slot, float x, float z) => _points[IndexOf(slot)] = (x, z);

    /// <summary>The point of bookmark <paramref name="slot"/> 1…4; false while it is empty.</summary>
    public bool TryGet(int slot, out (float X, float Z) point)
    {
        if (_points[IndexOf(slot)] is { } saved)
        {
            point = saved;
            return true;
        }

        point = default;
        return false;
    }

    private static int IndexOf(int slot) =>
        slot is >= 1 and <= Count ? slot - 1 : throw new ArgumentOutOfRangeException(nameof(slot), slot, "A bookmark is 1…4.");
}
