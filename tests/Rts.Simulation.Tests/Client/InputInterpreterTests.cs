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
    public void AltClickOnTheGroundSpawnsAUnitInBothSchemes()
    {
        foreach (var scheme in new[] { ControlScheme.Classic, ControlScheme.Modern })
        {
            var input = new InputInterpreter(scheme, cameraDrag: true);

            Assert.Equal(new Intent[] { new SpawnUnit(Spot) }, input.Press(PointerButton.Left, Here, Context(Ground, alt: true)));
            Assert.Empty(input.Release(PointerButton.Left));
        }
    }

    [Fact]
    public void ClickingAnotherUnitDoesNotSelectIt()
    {
        var input = new InputInterpreter(ControlScheme.Classic, cameraDrag: true);

        Assert.Empty(LeftClick(input, Context(OtherUnit, hasSelection: true)));
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

    private static PointerContext Context(PointerTarget target, bool hasSelection = false, bool shift = false, bool alt = false) =>
        new(Spot, target, shift, hasSelection, alt);

    /// <summary>A left click: press and release in place. The left button acts on release.</summary>
    private static IReadOnlyList<Intent> LeftClick(InputInterpreter input, PointerContext context)
    {
        Assert.Empty(input.Press(PointerButton.Left, Here, context));
        return input.Release(PointerButton.Left);
    }
}
