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
    }

    [Fact]
    public void WithASelectionAnEnemyShowsAttackAndCtrlAForcedAttack()
    {
        var building = new PointerTarget(PointerTargetKind.OtherBuilding, Building: new EntityId(5));
        Assert.Equal(CursorKind.Attack, CursorChoice.For(Context(OtherUnit, selected: true), anyCanEnter: true));
        Assert.Equal(CursorKind.Attack, CursorChoice.For(Context(building, selected: true), anyCanEnter: true));
        Assert.Equal(CursorKind.ForceAttack, CursorChoice.For(Context(OwnUnit, selected: true) with { Ctrl = true }, anyCanEnter: true));
        Assert.Equal(CursorKind.ForceAttack, CursorChoice.For(Context(Ground, selected: true) with { Ctrl = true }, anyCanEnter: false));
        Assert.Equal(CursorKind.Normal, CursorChoice.For(Context(OtherUnit, selected: false), anyCanEnter: true));
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

    [Fact]
    public void The_sell_mode_shows_the_dollar_over_an_own_building_only()
    {
        Assert.Equal(CursorKind.Sell, CursorChoice.For(Context(Ground, selected: false), true, PointerTool.Sell, new PointerSpot(OwnBuilding: true)));
        Assert.Equal(CursorKind.Forbidden, CursorChoice.For(Context(Ground, selected: false), true, PointerTool.Sell));
        Assert.Equal(CursorKind.Forbidden, CursorChoice.For(Context(OwnUnit, selected: true), true, PointerTool.Sell));
    }

    [Fact]
    public void The_repair_mode_shows_the_wrench_over_an_own_damaged_building_only()
    {
        Assert.Equal(CursorKind.Repair, CursorChoice.For(Context(Ground, selected: false), true, PointerTool.Repair, new PointerSpot(OwnBuilding: true, Damaged: true)));
        Assert.Equal(CursorKind.Forbidden, CursorChoice.For(Context(Ground, selected: false), true, PointerTool.Repair, new PointerSpot(OwnBuilding: true)));
    }

    [Fact]
    public void Placing_keeps_the_arrow_the_preview_shows_the_rest()
    {
        Assert.Equal(CursorKind.Normal, CursorChoice.For(Context(OwnUnit, selected: true), false, PointerTool.Build, new PointerSpot(Ore: true, Harvesters: true)));
    }

    [Fact]
    public void A_selected_mcv_shows_deploy_where_it_fits_and_forbidden_where_not()
    {
        var mcv = Context(OwnUnit, selected: true) with { TargetDeploys = true };

        Assert.Equal(CursorKind.Deploy, CursorChoice.For(mcv, true, spot: new PointerSpot(DeployFits: true)));
        Assert.Equal(CursorKind.Forbidden, CursorChoice.For(mcv, true));
        // Shift adds and takes out of the selection; Z gives a waypoint.
        Assert.Equal(CursorKind.Select, CursorChoice.For(mcv with { Shift = true }, true, spot: new PointerSpot(DeployFits: true)));
        Assert.Equal(CursorKind.Waypoint, CursorChoice.For(mcv with { WaypointMode = true }, true, spot: new PointerSpot(DeployFits: true)));
    }

    [Fact]
    public void Ore_shows_the_harvest_cursor_when_harvesters_are_selected()
    {
        Assert.Equal(CursorKind.Harvest, CursorChoice.For(Context(Ground, selected: true), true, spot: new PointerSpot(Ore: true, Harvesters: true)));
        Assert.Equal(CursorKind.Move, CursorChoice.For(Context(Ground, selected: true), true, spot: new PointerSpot(Ore: true)));
        Assert.Equal(CursorKind.AttackMove, CursorChoice.For(Context(Ground, selected: true) with { Ctrl = true, Shift = true }, true, spot: new PointerSpot(Ore: true, Harvesters: true)));
        Assert.Equal(CursorKind.Normal, CursorChoice.For(Context(Ground, selected: false), true, spot: new PointerSpot(Ore: true, Harvesters: true)));
    }

    [Fact]
    public void An_own_building_with_room_shows_the_enter_cursor_to_selected_soldiers()
    {
        // Stage 2e: the building's cells are closed to the soldiers, yet an order there sends them in.
        Assert.Equal(CursorKind.Enter, CursorChoice.For(Context(Ground, selected: true), anyCanEnter: false, spot: new PointerSpot(OwnBuilding: true, Garrison: true)));
        Assert.Equal(CursorKind.Forbidden, CursorChoice.For(Context(Ground, selected: true), anyCanEnter: false, spot: new PointerSpot(OwnBuilding: true)));
        Assert.Equal(CursorKind.ForceAttack, CursorChoice.For(Context(Ground, selected: true) with { Ctrl = true }, false, spot: new PointerSpot(Garrison: true)));
        Assert.Equal(CursorKind.Waypoint, CursorChoice.For(Context(Ground, selected: true) with { WaypointMode = true }, true, spot: new PointerSpot(Garrison: true)));
        Assert.Equal(CursorKind.Normal, CursorChoice.For(Context(Ground, selected: false), false, spot: new PointerSpot(Garrison: true)));
    }

    private static PointerContext Context(PointerTarget target, bool selected) => new(Spot, target, false, selected);
}
