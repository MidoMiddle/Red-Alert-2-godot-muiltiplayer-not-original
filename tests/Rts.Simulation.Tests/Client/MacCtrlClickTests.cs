using Rts.Client.Controls;

namespace Rts.Simulation.Tests.Client;

/// <summary>
/// macOS gives Ctrl + left click as a right click (2026-10-26: Grizzlies ordered with Ctrl at a bridge only lost their selection). In the
/// classic scheme on a Mac it is the left button's forced attack again; elsewhere the buttons stay.
/// </summary>
public sealed class MacCtrlClickTests
{
    [Fact]
    public void On_a_mac_in_the_classic_scheme_a_right_click_with_ctrl_is_the_left_one_its_release_and_holding_too()
    {
        var click = new MacCtrlClick(mac: true, ControlScheme.Classic);

        Assert.Equal(PointerButton.Left, click.Press(PointerButton.Right, ctrl: true));
        Assert.Equal((true, false), click.Held(false, true));
        Assert.Equal(PointerButton.Left, click.Release(PointerButton.Right));
        Assert.Equal((false, true), click.Held(false, true));

        // Without Ctrl it is the right button, and so is its release.
        Assert.Equal(PointerButton.Right, click.Press(PointerButton.Right, ctrl: false));
        Assert.Equal(PointerButton.Right, click.Release(PointerButton.Right));
        Assert.Equal(PointerButton.Left, click.Press(PointerButton.Left, ctrl: true));
    }

    [Fact]
    public void A_press_whose_release_never_came_does_not_turn_the_next_right_click()
    {
        var click = new MacCtrlClick(mac: true, ControlScheme.Classic);
        click.Press(PointerButton.Right, ctrl: true);

        Assert.Equal(PointerButton.Right, click.Press(PointerButton.Right, ctrl: false));
        Assert.Equal(PointerButton.Right, click.Release(PointerButton.Right));
    }

    [Theory]
    [InlineData(false, ControlScheme.Classic)]
    [InlineData(true, ControlScheme.Modern)]
    public void Elsewhere_the_buttons_stay(bool mac, ControlScheme scheme)
    {
        var click = new MacCtrlClick(mac, scheme);

        Assert.Equal(PointerButton.Right, click.Press(PointerButton.Right, ctrl: true));
        Assert.Equal((false, true), click.Held(false, true));
        Assert.Equal(PointerButton.Right, click.Release(PointerButton.Right));
    }
}
