using Rts.Client.Controls;
using Rts.Simulation.Geometry;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Client;

/// <summary>Which cursor shows where (docs/implementation/M2_unit_control.md, 4.14).</summary>
public sealed class CursorChoiceTests
{
    private static readonly WorldPos Spot = new(1000, 2000);
    private static readonly PointerTarget Ground = new(PointerTargetKind.Ground);
    private static readonly PointerTarget OwnUnit = new(PointerTargetKind.OwnUnit, new EntityId(1));
    private static readonly PointerTarget OtherUnit = new(PointerTargetKind.OtherUnit, new EntityId(2));

    [Fact]
    public void WithoutASelectionTheGroundShowsTheArrowAndAnOwnUnitTheSelectCursor()
    {
        Assert.Equal(CursorKind.Normal, CursorChoice.For(Context(Ground, selected: false), anyCanEnter: true));
        Assert.Equal(CursorKind.Normal, CursorChoice.For(Context(Ground, selected: false), anyCanEnter: false));
        Assert.Equal(CursorKind.Select, CursorChoice.For(Context(OwnUnit, selected: false), anyCanEnter: true));
        Assert.Equal(CursorKind.Select, CursorChoice.For(Context(OwnUnit, selected: true), anyCanEnter: true));
    }

    [Fact]
    public void WithASelectionTheGroundShowsMoveWhereSomeoneCanGo()
    {
        Assert.Equal(CursorKind.Move, CursorChoice.For(Context(Ground, selected: true), anyCanEnter: true));
        Assert.Equal(CursorKind.Move, CursorChoice.For(Context(OtherUnit, selected: true), anyCanEnter: true));
    }

    [Fact]
    public void RockWaterAndOffTheMapAreForbidden()
    {
        Assert.Equal(CursorKind.Forbidden, CursorChoice.For(Context(Ground, selected: true), anyCanEnter: false));
        Assert.Equal(CursorKind.Forbidden, CursorChoice.For(Context(Ground, selected: true) with { Ground = null }, anyCanEnter: true));
        Assert.Equal(CursorKind.Forbidden, CursorChoice.For(Context(Ground, selected: true) with { WaypointMode = true }, anyCanEnter: false));
    }

    [Fact]
    public void CtrlShiftShowsAttackMoveAndZTheWaypointEvenOverAnOwnUnit()
    {
        var attack = Context(OwnUnit, selected: true) with { Ctrl = true, Shift = true };
        var waypoint = Context(OwnUnit, selected: true) with { WaypointMode = true };

        Assert.Equal(CursorKind.AttackMove, CursorChoice.For(attack, anyCanEnter: true));
        Assert.Equal(CursorKind.Waypoint, CursorChoice.For(waypoint, anyCanEnter: true));
        Assert.Equal(CursorKind.Move, CursorChoice.For(Context(Ground, selected: true) with { Shift = true }, anyCanEnter: true));
    }

    private static PointerContext Context(PointerTarget target, bool selected) => new(Spot, target, false, selected);
}
