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

        Assert.Equal(new Intent[] { new SelectUnit(Blue) }, input.Press(PointerButton.Left, Here, Context(OwnUnit)));
    }

    [Fact]
    public void ClassicLeftClickOnTheGroundOrdersTheSelection()
    {
        var input = new InputInterpreter(ControlScheme.Classic, cameraDrag: true);

        Assert.Equal(new Intent[] { new OrderMove(Spot) }, input.Press(PointerButton.Left, Here, Context(Ground, hasSelection: true)));
    }

    [Fact]
    public void ClassicLeftClickOnTheGroundWithNothingSelectedDoesNothing()
    {
        var input = new InputInterpreter(ControlScheme.Classic, cameraDrag: true);

        Assert.Empty(input.Press(PointerButton.Left, Here, Context(Ground)));
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

        Assert.Equal(new Intent[] { new ClearSelection() }, input.Press(PointerButton.Left, Here, Context(Ground, hasSelection: true)));
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

        Assert.Equal(new Intent[] { new OrderMove(Spot) }, classic.Press(PointerButton.Left, Here, Context(Ground, hasSelection: true)));
        Assert.Equal(new Intent[] { new ClearSelection() }, modern.Press(PointerButton.Left, Here, Context(Ground, hasSelection: true)));
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
    public void ShiftClickOnTheGroundSpawnsAUnitInBothSchemes()
    {
        foreach (var scheme in new[] { ControlScheme.Classic, ControlScheme.Modern })
        {
            var input = new InputInterpreter(scheme, cameraDrag: true);

            Assert.Equal(new Intent[] { new SpawnUnit(Spot) }, input.Press(PointerButton.Left, Here, Context(Ground, shift: true)));
        }
    }

    [Fact]
    public void ClickingAnotherUnitDoesNotSelectIt()
    {
        var input = new InputInterpreter(ControlScheme.Classic, cameraDrag: true);

        Assert.Empty(input.Press(PointerButton.Left, Here, Context(OtherUnit, hasSelection: true)));
    }

    [Fact]
    public void EscapeClearsTheSelectionInBothSchemes()
    {
        foreach (var scheme in new[] { ControlScheme.Classic, ControlScheme.Modern })
        {
            var input = new InputInterpreter(scheme, cameraDrag: true);

            Assert.Equal(new Intent[] { new ClearSelection() }, input.CancelSelection());
        }
    }

    [Fact]
    public void MiddleButtonDoesNothingInClassic()
    {
        var input = new InputInterpreter(ControlScheme.Classic, cameraDrag: true);

        Assert.Empty(input.Press(PointerButton.Middle, Here, Context(Ground)));
        Assert.Empty(input.Move(Far));
        Assert.Empty(input.Release(PointerButton.Middle));
    }

    private static PointerContext Context(PointerTarget target, bool hasSelection = false, bool shift = false) =>
        new(Spot, target, shift, hasSelection);
}
