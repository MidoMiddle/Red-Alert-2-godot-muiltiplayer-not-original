using Rts.Client.Controls;
using Rts.Simulation.Geometry;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Client;

/// <summary>
/// Gestures whose release can be lost (a control takes it), the armed debug spawn, and the Escape key that disarms it first
/// (docs/implementation/M2_unit_control.md, 4.14; the command bar plan).
/// </summary>
public sealed class InputGestureTests
{
    private static readonly EntityId Blue = new(7);
    private static readonly WorldPos Spot = new(1000, 2000);
    private static readonly ScreenPoint Here = new(300f, 200f);
    private static readonly ScreenPoint Far = new(300f, 260f);
    private static readonly PointerTarget Ground = new(PointerTargetKind.Ground);
    private static readonly PointerTarget OwnUnit = new(PointerTargetKind.OwnUnit, Blue);

    [Fact]
    public void A_press_after_a_lost_release_starts_fresh()
    {
        var input = new InputInterpreter(ControlScheme.Classic, cameraDrag: true);
        input.Press(PointerButton.Left, Here, Context(Ground, hasSelection: true));

        Assert.Empty(input.Press(PointerButton.Left, Here, Context(OwnUnit)));
        Assert.Equal([new SelectUnit(Blue)], input.Release(PointerButton.Left));
    }

    [Fact]
    public void A_press_after_a_lost_release_of_a_box_cancels_the_box_and_starts_over()
    {
        var input = new InputInterpreter(ControlScheme.Classic, cameraDrag: true);
        input.Press(PointerButton.Left, Here, Context(Ground));
        input.Move(Far);

        Assert.Equal([new BoxCancelled()], input.Press(PointerButton.Left, Here, Context(OwnUnit)));
        Assert.Equal([new SelectUnit(Blue)], input.Release(PointerButton.Left));
    }

    [Fact]
    public void SyncButtons_ends_a_left_press_whose_release_was_lost_and_no_box_follows_the_pointer()
    {
        var input = new InputInterpreter(ControlScheme.Classic, cameraDrag: true);
        input.Press(PointerButton.Left, Here, Context(OwnUnit));

        Assert.Equal([new SelectUnit(Blue)], input.SyncButtons(leftHeld: false, rightHeld: false, middleHeld: false));
        Assert.Empty(input.Move(Far));
    }

    [Fact]
    public void SyncButtons_keeps_a_button_that_is_still_held()
    {
        var input = new InputInterpreter(ControlScheme.Classic, cameraDrag: true);
        input.Press(PointerButton.Left, Here, Context(OwnUnit));

        Assert.Empty(input.SyncButtons(leftHeld: true, rightHeld: false, middleHeld: false));
        Assert.Equal([new SelectUnit(Blue)], input.Release(PointerButton.Left));
    }

    [Fact]
    public void An_armed_spawn_places_units_on_the_ground_until_escape()
    {
        var input = new InputInterpreter(ControlScheme.Modern, cameraDrag: true);
        input.ArmSpawn();

        Assert.Equal([new SpawnUnit(Spot)], input.Press(PointerButton.Left, Here, Context(Ground)));
        Assert.Empty(input.Release(PointerButton.Left));
        Assert.Equal([new SpawnUnit(Spot)], input.Press(PointerButton.Left, Here, Context(Ground)));
        Assert.True(input.SpawnArmed);
    }

    [Fact]
    public void Escape_disarms_the_spawn_before_it_clears_the_selection()
    {
        var input = new InputInterpreter(ControlScheme.Modern, cameraDrag: true);
        input.ArmSpawn();

        Assert.Empty(input.EscapePressed(hasSelection: true));
        Assert.False(input.SpawnArmed);
        Assert.Equal([new ClearSelection()], input.EscapePressed(hasSelection: true));
    }

    [Fact]
    public void An_armed_build_places_buildings_until_escape_and_replaces_an_armed_spawn()
    {
        var input = new InputInterpreter(ControlScheme.Modern, cameraDrag: true);
        input.ArmSpawn();
        input.ArmBuild();

        Assert.False(input.SpawnArmed);
        Assert.Equal([new PlaceBuilding(Spot)], input.Press(PointerButton.Left, Here, Context(Ground)));
        Assert.Empty(input.Release(PointerButton.Left));
        Assert.True(input.BuildArmed);

        Assert.Empty(input.EscapePressed(hasSelection: true));
        Assert.False(input.BuildArmed);
    }

    [Theory]
    [InlineData(ControlScheme.Classic)]
    [InlineData(ControlScheme.Modern)]
    public void A_right_click_leaves_the_placement_and_nothing_else_happens(ControlScheme scheme)
    {
        var input = new InputInterpreter(scheme, cameraDrag: true);
        input.ArmBuild();

        Assert.Empty(input.Press(PointerButton.Right, Here, Context(Ground, hasSelection: true)));
        Assert.Empty(input.Release(PointerButton.Right));
        Assert.Equal(PointerTool.None, input.Tool);
    }

    [Fact]
    public void The_sell_and_repair_modes_act_on_every_click_until_a_right_click_or_escape()
    {
        var input = new InputInterpreter(ControlScheme.Modern, cameraDrag: true);
        input.ArmSell();

        Assert.Equal([new SellBuilding(Spot)], input.Press(PointerButton.Left, Here, Context(Ground)));
        Assert.Empty(input.Release(PointerButton.Left));
        Assert.Equal([new SellBuilding(Spot)], input.Press(PointerButton.Left, Here, Context(OwnUnit)));
        Assert.Empty(input.Release(PointerButton.Left));
        Assert.Empty(input.Press(PointerButton.Right, Here, Context(Ground)));
        Assert.Equal(PointerTool.None, input.Tool);

        input.ArmRepair();
        Assert.Equal([new RepairBuilding(Spot)], input.Press(PointerButton.Left, Here, Context(Ground)));
        Assert.Empty(input.Release(PointerButton.Left));
        Assert.Empty(input.EscapePressed(hasSelection: true));
        Assert.Equal(PointerTool.None, input.Tool);
    }

    [Theory]
    [InlineData(ControlScheme.Classic)]
    [InlineData(ControlScheme.Modern)]
    public void A_click_on_a_selected_mcv_deploys_it_and_on_another_unit_selects(ControlScheme scheme)
    {
        var input = new InputInterpreter(scheme, cameraDrag: true);

        input.Press(PointerButton.Left, Here, Context(OwnUnit, hasSelection: true) with { TargetDeploys = true });
        Assert.Equal([new DeployUnit(Blue)], input.Release(PointerButton.Left));
        input.Press(PointerButton.Left, Here, Context(OwnUnit, hasSelection: true));
        Assert.Equal([new SelectUnit(Blue)], input.Release(PointerButton.Left));
        input.Press(PointerButton.Left, Here, Context(OwnUnit, hasSelection: true) with { TargetDeploys = true, Shift = true });
        Assert.Equal([new ToggleUnit(Blue)], input.Release(PointerButton.Left));
    }

    [Fact]
    public void One_tool_at_a_time()
    {
        var input = new InputInterpreter(ControlScheme.Modern, cameraDrag: true);
        input.ArmSell();
        input.ArmBuild();

        Assert.True(input.BuildArmed);
        Assert.Equal([new PlaceBuilding(Spot)], input.Press(PointerButton.Left, Here, Context(Ground)));
    }

    private static PointerContext Context(PointerTarget target, bool hasSelection = false) =>
        new(Spot, target, false, hasSelection);
}
