using Godot;

namespace Rts.Client.Hud;

/// <summary>
/// Game statistics, opened by the Stats button: how many buildings the player has built and how many it has captured
/// (docs/08_ui_ux.md, section 4). Built comes with M3, captured with M11. Until then both show 0.
/// </summary>
public partial class StatsPanel : PanelContainer
{
    private Label _built = null!;
    private Label _captured = null!;

    public override void _Ready()
    {
        Visible = false;
        GetNode<Label>("Column/Title").Text = Tr("UI_STATS_TITLE");
        _built = GetNode<Label>("Column/BuiltLabel");
        _captured = GetNode<Label>("Column/CapturedLabel");

        var close = GetNode<Button>("Column/CloseButton");
        close.Text = Tr("UI_BUTTON_CLOSE");
        close.Pressed += () => Visible = false;
    }

    /// <summary>Sets the two numbers the panel shows.</summary>
    public void Setup(int built, int captured)
    {
        _built.Text = string.Format(Tr("UI_STATS_BUILT"), built);
        _captured.Text = string.Format(Tr("UI_STATS_CAPTURED"), captured);
    }

    /// <summary>Shows the panel when it is hidden, hides it when it is shown.</summary>
    public void Toggle() => Visible = !Visible;
}
