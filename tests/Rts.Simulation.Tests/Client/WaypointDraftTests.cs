using Rts.Client.Controls;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;

namespace Rts.Simulation.Tests.Client;

/// <summary>Waypoints laid out with Z (docs/implementation/M2_unit_control.md, 4.14): the last point, the patrol circle, the limit.</summary>
public sealed class WaypointDraftTests
{
    private static readonly WorldPos A = new CellPos(2, 2).Center;
    private static readonly WorldPos B = new CellPos(8, 2).Center;
    private static readonly WorldPos C = new CellPos(8, 8).Center;

    [Fact]
    public void TheLastPointEndsTheRoute()
    {
        var draft = new WaypointDraft();
        draft.Add(A);
        draft.Add(B);

        var (points, loop) = draft.Finish(C);

        Assert.Equal([A, B, C], points.ToArray());
        Assert.False(loop);
        Assert.True(draft.IsEmpty);
    }

    [Fact]
    public void ClosingTheCircleOnTheFirstCellIsAPatrol()
    {
        var draft = new WaypointDraft();
        draft.Add(A);
        draft.Add(B);
        draft.Add(C);

        // Anywhere in the first point's cell closes the circle.
        var (points, loop) = draft.Finish(A + new WorldPos(40, -40));

        Assert.Equal([A, B, C], points.ToArray());
        Assert.True(loop);
    }

    [Fact]
    public void TwoPointsInOneCellAreNotAPatrol()
    {
        var draft = new WaypointDraft();
        draft.Add(A);

        var (points, loop) = draft.Finish(A);

        Assert.Equal([A, A], points.ToArray());
        Assert.False(loop);
    }

    [Fact]
    public void ARefusedLastPointSendsThePointsSoFar()
    {
        var draft = new WaypointDraft();
        draft.Add(A);
        draft.Add(B);

        var (points, loop) = draft.Finish(null);

        Assert.Equal([A, B], points.ToArray());
        Assert.False(loop);
    }

    [Fact]
    public void TheRouteNeverExceedsTheCommandLimit()
    {
        var draft = new WaypointDraft();
        for (var i = 0; i < WaypointsCommand.MaxPoints + 5; i++)
        {
            draft.Add(new CellPos(i % 30, i / 30).Center);
        }

        var (points, _) = draft.Finish(C);

        Assert.Equal(WaypointsCommand.MaxPoints, points.Length);
    }
}
