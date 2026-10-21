using System.Collections.Generic;
using System.Collections.Immutable;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;

namespace Rts.Client.Controls;

/// <summary>
/// The waypoints the player lays out with Z before sending them (docs/implementation/M2_unit_control.md, 4.14). The next
/// order without Z is the last point. As in RA2, a route whose last point closes the circle on the first point's cell
/// (at least three points) is a patrol: it is sent with <c>loop</c> and without the closing point.
/// </summary>
public sealed class WaypointDraft
{
    private readonly List<WorldPos> _points = new();

    public IReadOnlyList<WorldPos> Points => _points;

    public bool IsEmpty => _points.Count == 0;

    /// <summary>Adds a point. Beyond <see cref="WaypointsCommand.MaxPoints"/> points the extra ones are dropped.</summary>
    public void Add(WorldPos point)
    {
        if (_points.Count < WaypointsCommand.MaxPoints)
        {
            _points.Add(point);
        }
    }

    /// <summary>
    /// Takes the last point and gives the route to send, then empties the draft. Without a last point (it was refused) the
    /// route is the points laid out so far.
    /// </summary>
    public (ImmutableArray<WorldPos> Points, bool Loop) Finish(WorldPos? last)
    {
        var loop = last is { } closing && _points.Count >= 2 && closing.ToCell() == _points[0].ToCell();
        if (!loop && last is { } point)
        {
            Add(point);
        }

        var route = (_points.ToImmutableArray(), loop);
        _points.Clear();
        return route;
    }

    public void Clear() => _points.Clear();
}
