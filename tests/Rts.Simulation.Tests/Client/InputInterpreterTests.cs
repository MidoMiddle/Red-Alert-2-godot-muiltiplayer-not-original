using Rts.Client.Controls;
using Rts.Simulation.Geometry;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Client;

/// <summary>What each mouse event means in each scheme (docs/implementation/M6_menus_skirmish.md, 4.6).</summary>
public sealed class InputInterpreterTests
{
    private static readonly EntityId Blue = new(7);
    private static readonly WorldPos Spot = new(1000, 2000);
    private static readonly ScreenPoint Here = new(300f, 200f);
    private static readonly ScreenPoint Far = new(300f, 260f);
    private static readonly PointerTarget Ground = new(PointerTargetKind.Ground);
    private static readonly PointerTarget OwnUnit = new(PointerTargetKind.OwnUnit, Blue);
    private static readonly PointerTarget OtherUnit = new(PointerTargetKind.OtherUnit, new EntityId(8));

    [Fact]
    public void ClassicLeftClickSelectsAnOwnUnit()
    {
        var input = new InputInterpreter(ControlScheme.Classic, cameraDrag: true);

        Assert.Empty(input.Press(PointerButton.Left, Here, Context(OwnUnit)));
        Assert.Equal(new Intent[] { new SelectUnit(Blue) }, input.Release(PointerButton.Left));
    }

    [Fact]
    public void ClassicLeftClickOnTheGroundOrdersTheSelection()
    {
        var input = new InputInterpreter(ControlScheme.Classic, cameraDrag: true);

        Assert.Equal(new Intent[] { new OrderMove(Spot) }, LeftClick(input, Context(Ground, hasSelection: true)));
    }

    [Fact]
    public void ClassicLeftClickOnTheGroundWithNothingSelectedDoesNothing()
    {
        var input = new InputInterpreter(ControlScheme.Classic, cameraDrag: true);

        Assert.Empty(LeftClick(input, Context(Ground)));
    }

    [Fact]
    public void ClassicRightClickCancelsTheSelection()
    {
        var input = new InputInterpreter(ControlScheme.Classic, cameraDrag: true);

        Assert.Empty(input.Press(PointerButton.Right, Here, Context(Ground, hasSelection: true)));
        Assert.Equal(new Intent[] { new ClearSelection() }, input.Release(PointerButton.Right));
    }

    [Fact]
    public void ClassicRightDragMovesTheCamera()
    {
        var input = new InputInterpreter(ControlScheme.Classic, cameraDrag: true);
        input.Press(PointerButton.Right, Here, Context(Ground, hasSelection: true));

        Assert.Equal(
            new Intent[] { new CameraDragStarted(Here), new CameraDragMoved(Far) },
            input.Move(Far));
        Assert.Equal(new Intent[] { new CameraDragMoved(Far) }, input.Move(Far));
        Assert.Equal(new Intent[] { new CameraDragEnded() }, input.Release(PointerButton.Right));
    }

    [Fact]
    public void ClassicRightDragWithoutCameraDragDoesNotCancelOrMove()
    {
        var input = new InputInterpreter(ControlScheme.Classic, cameraDrag: false);
        input.Press(PointerButton.Right, Here, Context(Ground, hasSelection: true));

        Assert.Empty(input.Move(Far));
        Assert.Empty(input.Release(PointerButton.Right));
    }

    [Fact]
    public void SmallMovementIsStillAClassicClick()
    {
        var input = new InputInterpreter(ControlScheme.Classic, cameraDrag: true);
        input.Press(PointerButton.Right, Here, Context(Ground, hasSelection: true));

        Assert.Empty(input.Move(new ScreenPoint(Here.X + 3f, Here.Y)));
        Assert.Equal(new Intent[] { new ClearSelection() }, input.Release(PointerButton.Right));
    }

    [Fact]
    public void ModernRightClickOrdersTheSelectionOnTheGround()
    {
        var input = new InputInterpreter(ControlScheme.Modern, cameraDrag: true);

        Assert.Equal(new Intent[] { new OrderMove(Spot) }, input.Press(PointerButton.Right, Here, Context(Ground, hasSelection: true)));
        Assert.Empty(input.Release(PointerButton.Right));
    }

    [Theory]
    [InlineData(ControlScheme.Classic)]
    [InlineData(ControlScheme.Modern)]
    public void A_left_click_on_the_construction_yard_already_selected_packs_it_up(ControlScheme scheme)
    {
        // RA2: the yard selected, a click on it packs it into its MCV, as D does; with Shift it is a pick.
        var input = new InputInterpreter(scheme, cameraDrag: true);
        var yard = new EntityId(31);

        Assert.Equal(new Intent[] { new DeployUnit(yard) }, LeftClick(input, Context(Ground, hasSelection: true) with { OwnFactory = yard, YardPacks = true }));
        Assert.Equal(new Intent[] { new SelectBuilding(yard) }, LeftClick(input, Context(Ground) with { OwnFactory = yard }));
    }

    [Theory]
    [InlineData(ControlScheme.Classic)]
    [InlineData(ControlScheme.Modern)]
    public void A_left_click_on_an_own_building_that_produces_selects_it_and_an_order_on_the_ground_follows(ControlScheme scheme)
    {
        var input = new InputInterpreter(scheme, cameraDrag: true);
        var barracks = new EntityId(30);

        Assert.Equal(new Intent[] { new SelectBuilding(barracks) }, LeftClick(input, Context(Ground) with { OwnFactory = barracks }));
        Assert.Equal(new Intent[] { new SelectBuilding(barracks) }, LeftClick(input, Context(Ground, hasSelection: true) with { OwnFactory = barracks }));
        // A double click on it makes it the primary one too (RA2).
        Assert.Equal(new Intent[] { new SelectBuilding(barracks), new MakePrimary(barracks) },
            LeftClick(input, Context(Ground) with { OwnFactory = barracks, DoubleClick = true }));

        // The rally point: an order on the ground with the building selected, by the scheme's order button.
        var order = scheme == ControlScheme.Modern
            ? input.Press(PointerButton.Right, Here, Context(Ground, hasSelection: true))
            : LeftClick(input, Context(Ground, hasSelection: true));
        Assert.Equal(new Intent[] { new OrderMove(Spot) }, order);
    }

    [Theory]
    [InlineData(ControlScheme.Classic)]
    [InlineData(ControlScheme.Modern)]
    public void A_left_click_on_an_own_defense_selects_it_and_an_order_on_an_enemy_is_its_target(ControlScheme scheme)
    {
        // RA2 (the owner, 2026-10-26): a Prism Tower clicked shows its range and fires where it is told.
        var input = new InputInterpreter(scheme, cameraDrag: true);
        var tower = new EntityId(31);
        var enemy = new EntityId(40);

        Assert.Equal(new Intent[] { new SelectBuilding(tower) }, LeftClick(input, Context(Ground) with { OwnDefense = tower }));
        Assert.Equal(new Intent[] { new SelectBuilding(tower) }, LeftClick(input, Context(Ground, hasSelection: true) with { OwnDefense = tower }));

        var onEnemy = Context(new PointerTarget(PointerTargetKind.OtherUnit, enemy), hasSelection: true);
        var order = scheme == ControlScheme.Modern ? input.Press(PointerButton.Right, Here, onEnemy) : LeftClick(input, onEnemy);
        Assert.Equal(new Intent[] { new OrderAttack(enemy) }, order);
    }

    [Theory]
    [InlineData(ControlScheme.Classic)]
    [InlineData(ControlScheme.Modern)]
    public void Soldiers_that_can_go_into_an_own_held_building_are_sent_in_not_the_building_picked(ControlScheme scheme)
    {
        // The owner (2026-10-30): one soldier in a house — it fires, so it is a defense — and the next ones could not follow.
        var input = new InputInterpreter(scheme, cameraDrag: true);
        var house = new EntityId(31);
        var held = Context(Ground, hasSelection: true) with { OwnDefense = house, GroundEnters = true };

        var order = scheme == ControlScheme.Modern ? input.Press(PointerButton.Right, Here, held) : LeftClick(input, held);
        Assert.Equal(new Intent[] { new OrderMove(Spot) }, order);
        // The game sends a move onto a building the selection can go into as an order to go in.
        Assert.Equal(new Intent[] { new OrderMove(Spot) },
            scheme == ControlScheme.Modern
                ? input.Press(PointerButton.Right, Here, held with { OwnDefense = null, OwnFactory = house })
                : LeftClick(input, held with { OwnDefense = null, OwnFactory = house }));
        // Without such soldiers a click still picks it.
        Assert.Equal(new Intent[] { new SelectBuilding(house) }, LeftClick(input, held with { GroundEnters = false }));
    }

    [Fact]
    public void ModernRightClickWithNothingSelectedDoesNothing()
    {
        var input = new InputInterpreter(ControlScheme.Modern, cameraDrag: true);

        Assert.Empty(input.Press(PointerButton.Right, Here, Context(Ground)));
        Assert.Empty(input.Release(PointerButton.Right));
    }

    [Fact]
    public void ModernLeftClickOnEmptyGroundCancelsTheSelection()
    {
        var input = new InputInterpreter(ControlScheme.Modern, cameraDrag: true);

        Assert.Equal(new Intent[] { new ClearSelection() }, LeftClick(input, Context(Ground, hasSelection: true)));
    }

    [Fact]
    public void ModernMiddleDragMovesTheCamera()
    {
        var input = new InputInterpreter(ControlScheme.Modern, cameraDrag: true);

        Assert.Equal(new Intent[] { new CameraDragStarted(Here) }, input.Press(PointerButton.Middle, Here, Context(Ground)));
        Assert.Equal(new Intent[] { new CameraDragMoved(Far) }, input.Move(Far));
        Assert.Equal(new Intent[] { new CameraDragEnded() }, input.Release(PointerButton.Middle));
    }

    [Fact]
    public void TheSameLeftClickMeansDifferentThingsInEachScheme()
    {
        var classic = new InputInterpreter(ControlScheme.Classic, cameraDrag: true);
        var modern = new InputInterpreter(ControlScheme.Modern, cameraDrag: true);

        Assert.Equal(new Intent[] { new OrderMove(Spot) }, LeftClick(classic, Context(Ground, hasSelection: true)));
        Assert.Equal(new Intent[] { new ClearSelection() }, LeftClick(modern, Context(Ground, hasSelection: true)));
    }

    [Fact]
    public void TheSameRightClickMeansDifferentThingsInEachScheme()
    {
        var classic = new InputInterpreter(ControlScheme.Classic, cameraDrag: true);
        var modern = new InputInterpreter(ControlScheme.Modern, cameraDrag: true);

        classic.Press(PointerButton.Right, Here, Context(Ground, hasSelection: true));
        Assert.Equal(new Intent[] { new ClearSelection() }, classic.Release(PointerButton.Right));
        Assert.Equal(new Intent[] { new OrderMove(Spot) }, modern.Press(PointerButton.Right, Here, Context(Ground, hasSelection: true)));
    }

    [Fact]
    public void AltAndAnOrderOnAnEnemyIsAForcedMoveInBothSchemes()
    {
        // RA2: Alt moves to the point, over an enemy too (to crush it), and attacks nothing; the debug spawn is B's alone.
        var classic = new InputInterpreter(ControlScheme.Classic, cameraDrag: true);
        Assert.Equal(new Intent[] { new OrderMove(Spot) }, LeftClick(classic, Context(OtherUnit, hasSelection: true, alt: true)));
        Assert.Empty(LeftClick(classic, Context(Ground, alt: true)));

        var modern = new InputInterpreter(ControlScheme.Modern, cameraDrag: true);
        Assert.Equal(new Intent[] { new OrderMove(Spot) }, modern.Press(PointerButton.Right, Here, Context(OtherUnit, hasSelection: true, alt: true)));
        Assert.Empty(modern.Release(PointerButton.Right));

        modern.ArmSpawn();
        Assert.Equal(new Intent[] { new SpawnUnit(Spot) }, modern.Press(PointerButton.Left, Here, Context(Ground)));
    }

    [Fact]
    public void ClickingAnEnemyWithNothingSelectedLooksAtIt()
    {
        // RA2: an enemy clicked with nothing selected shows its health; it is not taken into the selection, so no order reaches it.
        var classic = new InputInterpreter(ControlScheme.Classic, cameraDrag: true);
        var modern = new InputInterpreter(ControlScheme.Modern, cameraDrag: true);
        var building = Context(new PointerTarget(PointerTargetKind.OtherBuilding, Building: new EntityId(40)));

        Assert.Equal(new Intent[] { new InspectEnemy(OtherUnit.Unit) }, LeftClick(classic, Context(OtherUnit, hasSelection: false)));
        Assert.Equal(new Intent[] { new InspectEnemy(new EntityId(40)) }, LeftClick(classic, building));
        Assert.Equal(new Intent[] { new InspectEnemy(OtherUnit.Unit) }, LeftClick(modern, Context(OtherUnit, hasSelection: false)));
    }

    [Fact]
    public void ShiftClickOnAnOwnUnitTogglesItInBothSchemes()
    {
        foreach (var scheme in new[] { ControlScheme.Classic, ControlScheme.Modern })
        {
            var input = new InputInterpreter(scheme, cameraDrag: true);

            Assert.Equal(new Intent[] { new ToggleUnit(Blue) }, LeftClick(input, Context(OwnUnit, hasSelection: true, shift: true)));
        }
    }

    [Fact]
    public void LeftDragDrawsABoxAndSelectsOnRelease()
    {
        foreach (var scheme in new[] { ControlScheme.Classic, ControlScheme.Modern })
        {
            var input = new InputInterpreter(scheme, cameraDrag: true);
            var end = new ScreenPoint(380f, 260f);

            Assert.Empty(input.Press(PointerButton.Left, Here, Context(Ground, hasSelection: true)));
            Assert.Empty(input.Move(new ScreenPoint(Here.X + 3f, Here.Y + 2f)));
            Assert.Equal(new Intent[] { new BoxStarted(Here), new BoxMoved(Here, Far) }, input.Move(Far));
            Assert.Equal(new Intent[] { new BoxMoved(Here, end) }, input.Move(end));
            Assert.Equal(new Intent[] { new BoxSelect(Here, end, Add: false) }, input.Release(PointerButton.Left));
        }
    }

    [Fact]
    public void ShiftBoxAddsToTheSelection()
    {
        var input = new InputInterpreter(ControlScheme.Classic, cameraDrag: true);

        input.Press(PointerButton.Left, Here, Context(OwnUnit, hasSelection: true, shift: true));
        input.Move(Far);

        Assert.Equal(new Intent[] { new BoxSelect(Here, Far, Add: true) }, input.Release(PointerButton.Left));
    }

    [Fact]
    public void A_box_never_orders_a_move()
    {
        var input = new InputInterpreter(ControlScheme.Classic, cameraDrag: true);

        input.Press(PointerButton.Left, Here, Context(Ground, hasSelection: true));
        input.Move(Far);

        Assert.DoesNotContain(input.Release(PointerButton.Left), intent => intent is OrderMove);
    }

    [Fact]
    public void EscapeCancelsABoxAndTheNextClickWorks()
    {
        var input = new InputInterpreter(ControlScheme.Classic, cameraDrag: true);
        input.Press(PointerButton.Left, Here, Context(Ground, hasSelection: true));
        input.Move(Far);

        Assert.Equal(new Intent[] { new BoxCancelled() }, input.EscapePressed(hasSelection: true));
        Assert.Empty(input.Release(PointerButton.Left));
        Assert.Equal(new Intent[] { new OrderMove(Spot) }, LeftClick(input, Context(Ground, hasSelection: true)));
    }

    [Fact]
    public void EscapeClearsTheSelectionOrOpensTheMenuInBothSchemes()
    {
        foreach (var scheme in new[] { ControlScheme.Classic, ControlScheme.Modern })
        {
            var input = new InputInterpreter(scheme, cameraDrag: true);

            Assert.Equal(new Intent[] { new ClearSelection() }, input.EscapePressed(hasSelection: true));
            Assert.Equal(new Intent[] { new OpenMenu() }, input.EscapePressed(hasSelection: false));
        }
    }

    [Fact]
    public void EscapeEndsACameraDragAndTheNextClickWorks()
    {
        var input = new InputInterpreter(ControlScheme.Classic, cameraDrag: true);
        input.Press(PointerButton.Right, Here, Context(Ground, hasSelection: true));
        input.Move(Far);

        Assert.Equal(new Intent[] { new CameraDragEnded(), new OpenMenu() }, input.EscapePressed(hasSelection: false));
        input.Press(PointerButton.Right, Here, Context(Ground, hasSelection: true));
        Assert.Equal(new Intent[] { new ClearSelection() }, input.Release(PointerButton.Right));
    }

    [Fact]
    public void MiddleButtonDoesNothingInClassic()
    {
        var input = new InputInterpreter(ControlScheme.Classic, cameraDrag: true);

        Assert.Empty(input.Press(PointerButton.Middle, Here, Context(Ground)));
        Assert.Empty(input.Move(Far));
        Assert.Empty(input.Release(PointerButton.Middle));
    }

    // M2 step 13: the RA2 orders (docs/implementation/M2_unit_control.md, 4.14).

    [Fact]
    public void ADoubleClickOnAnOwnUnitSelectsItsTypeAndShiftAdds()
    {
        foreach (var scheme in new[] { ControlScheme.Classic, ControlScheme.Modern })
        {
            var input = new InputInterpreter(scheme, cameraDrag: true);

            Assert.Equal(new Intent[] { new SelectSameType(Blue, false) }, LeftClick(input, Context(OwnUnit) with { DoubleClick = true }));
            Assert.Equal(
                new Intent[] { new SelectSameType(Blue, true) },
                LeftClick(input, Context(OwnUnit, hasSelection: true, shift: true) with { DoubleClick = true }));
        }
    }

    [Fact]
    public void ADoubleClickOnTheGroundIsAnOrdinaryClick()
    {
        var input = new InputInterpreter(ControlScheme.Classic, cameraDrag: true);

        Assert.Equal(new Intent[] { new OrderMove(Spot) }, LeftClick(input, Context(Ground, hasSelection: true) with { DoubleClick = true }));
    }

    [Fact]
    public void ShiftQueuesAMoveAndCtrlShiftAttackMovesInBothSchemes()
    {
        var classic = new InputInterpreter(ControlScheme.Classic, cameraDrag: true);
        var modern = new InputInterpreter(ControlScheme.Modern, cameraDrag: true);
        var queued = Context(Ground, hasSelection: true, shift: true);
        var attack = queued with { Ctrl = true };

        Assert.Equal(new Intent[] { new OrderMove(Spot, Queue: true) }, LeftClick(classic, queued));
        Assert.Equal(new Intent[] { new OrderAttackMove(Spot) }, LeftClick(classic, attack));
        Assert.Equal(new Intent[] { new OrderMove(Spot, Queue: true) }, modern.Press(PointerButton.Right, Here, queued));
        Assert.Equal(new Intent[] { new OrderAttackMove(Spot) }, modern.Press(PointerButton.Right, Here, attack));
    }

    [Fact]
    public void CtrlShiftOverAUnitStillAttackMovesInClassic()
    {
        var input = new InputInterpreter(ControlScheme.Classic, cameraDrag: true);

        Assert.Equal(
            new Intent[] { new OrderAttackMove(Spot) },
            LeftClick(input, Context(OwnUnit, hasSelection: true, shift: true) with { Ctrl = true }));
    }

    [Fact]
    public void CtrlAloneForcesAnAttackOnTheGroundAnOwnUnitOrABuildingInBothSchemes()
    {
        var classic = new InputInterpreter(ControlScheme.Classic, cameraDrag: true);
        var modern = new InputInterpreter(ControlScheme.Modern, cameraDrag: true);
        var ground = Context(Ground, hasSelection: true) with { Ctrl = true };
        var own = Context(OwnUnit, hasSelection: true) with { Ctrl = true };
        var wall = Context(new PointerTarget(PointerTargetKind.Ground, Building: new EntityId(30)), hasSelection: true) with { Ctrl = true };

        Assert.Equal(new Intent[] { new OrderForceAttack(null, Spot) }, LeftClick(classic, ground));
        Assert.Equal(new Intent[] { new OrderForceAttack(OwnUnit.Unit, Spot) }, LeftClick(classic, own));
        Assert.Equal(new Intent[] { new OrderForceAttack(new EntityId(30), Spot) }, LeftClick(classic, wall));
        Assert.Equal(new Intent[] { new OrderForceAttack(OwnUnit.Unit, Spot) }, modern.Press(PointerButton.Right, Here, own));

        // Without a selection Ctrl and a click still select.
        Assert.Equal(new Intent[] { new SelectUnit(OwnUnit.Unit) }, LeftClick(classic, Context(OwnUnit, hasSelection: false) with { Ctrl = true }));
    }

    [Fact]
    public void AnOrderOnAnEnemyUnitOrBuildingAttacksItInBothSchemes()
    {
        var classic = new InputInterpreter(ControlScheme.Classic, cameraDrag: true);
        var modern = new InputInterpreter(ControlScheme.Modern, cameraDrag: true);
        var building = Context(new PointerTarget(PointerTargetKind.OtherBuilding, Building: new EntityId(40)), hasSelection: true);

        Assert.Equal(new Intent[] { new OrderAttack(OtherUnit.Unit) }, LeftClick(classic, Context(OtherUnit, hasSelection: true)));
        Assert.Equal(new Intent[] { new OrderAttack(new EntityId(40)) }, LeftClick(classic, building));
        Assert.Equal(new Intent[] { new OrderAttack(OtherUnit.Unit) }, modern.Press(PointerButton.Right, Here, Context(OtherUnit, hasSelection: true)));

        // The left button of Modern only selects; Ctrl + Shift is still an attack-move, even over an enemy.
        Assert.Empty(LeftClick(modern, Context(OtherUnit, hasSelection: true)));
        Assert.Equal(new Intent[] { new OrderAttackMove(Spot) },
            LeftClick(classic, Context(OtherUnit, hasSelection: true) with { Ctrl = true, Shift = true }));
    }

    [Fact]
    public void OrdersWithZAddWaypointsAndTheNextOrderWithoutZSendsThem()
    {
        var classic = new InputInterpreter(ControlScheme.Classic, cameraDrag: true);
        var modern = new InputInterpreter(ControlScheme.Modern, cameraDrag: true);
        var laying = Context(Ground, hasSelection: true) with { WaypointMode = true };
        var finishing = Context(Ground, hasSelection: true) with { HasWaypoints = true };

        Assert.Equal(new Intent[] { new AddWaypoint(Spot) }, LeftClick(classic, laying));
        Assert.Equal(new Intent[] { new AddWaypoint(Spot) }, LeftClick(classic, laying with { HasWaypoints = true, Target = OwnUnit }));
        Assert.Equal(new Intent[] { new FinishWaypoints(Spot) }, LeftClick(classic, finishing));
        Assert.Equal(new Intent[] { new AddWaypoint(Spot) }, modern.Press(PointerButton.Right, Here, laying));
        Assert.Equal(new Intent[] { new FinishWaypoints(Spot) }, modern.Press(PointerButton.Right, Here, finishing));
    }

    [Fact]
    public void WaypointsNeedASelectionAndTheGround()
    {
        var input = new InputInterpreter(ControlScheme.Classic, cameraDrag: true);

        Assert.Empty(LeftClick(input, Context(Ground) with { WaypointMode = true }));
        Assert.Empty(LeftClick(input, new PointerContext(null, Ground, false, true, WaypointMode: true)));
    }

    [Fact]
    public void EscapeFirstDropsTheWaypointsThenTheSelection()
    {
        var input = new InputInterpreter(ControlScheme.Classic, cameraDrag: true);

        Assert.Equal(new Intent[] { new CancelWaypoints() }, input.EscapePressed(hasSelection: true, hasWaypoints: true));
        Assert.Equal(new Intent[] { new ClearSelection() }, input.EscapePressed(hasSelection: true, hasWaypoints: false));
    }

    private static PointerContext Context(PointerTarget target, bool hasSelection = false, bool shift = false, bool alt = false) =>
        new(Spot, target, shift, hasSelection, alt);

    /// <summary>A left click: press and release in place. The left button acts on release.</summary>
    private static IReadOnlyList<Intent> LeftClick(InputInterpreter input, PointerContext context)
    {
        Assert.Empty(input.Press(PointerButton.Left, Here, context));
        return input.Release(PointerButton.Left);
    }
}
