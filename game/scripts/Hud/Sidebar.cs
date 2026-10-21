using Godot;
using Rts.Client.App;

namespace Rts.Client.Hud;

/// <summary>
/// The sidebar on the right of the view (docs/08_ui_ux.md, section 4): money, the menu buttons (Menu opens the pause menu, Stats
/// shows the statistics), the minimap, repair and sell, the tabs of the build groups and the item squares. Repair and sell need
/// buildings (M3), so they stay greyed out until then. The money is a placeholder until M3.
/// </summary>
public partial class Sidebar : Control
{
    [Signal]
    public delegate void MenuRequestedEventHandler();

    [Signal]
    public delegate void StatsRequestedEventHandler();

    private Label _credits = null!;
    private Button[] _tabs = null!;
    private StyleBoxFlat _tabStyle = null!;

    /// <summary>The minimap at the top of the sidebar.</summary>
    public Minimap Minimap => GetNode<Minimap>("Column/Minimap");

    public override void _Ready()
    {
        OffsetLeft = -SidebarLayout.Width;

        _credits = GetNode<Label>("Column/CreditsBar/Credits");
        _tabs =
        [
            GetNode<Button>("Column/TabGrid/TabBuildings"),
            GetNode<Button>("Column/TabGrid/TabDefense"),
            GetNode<Button>("Column/TabGrid/TabInfantry"),
            GetNode<Button>("Column/TabGrid/TabVehicles"),
        ];
        // The pressed style of the theme, with its margins and borders; only the colour changes with the faction.
        _tabStyle = (StyleBoxFlat)GetThemeStylebox("pressed", "Button").Duplicate();

        var menu = GetNode<Button>("Column/MenuRow/MenuButton");
        var stats = GetNode<Button>("Column/MenuRow/StatsButton");
        var repair = GetNode<Button>("Column/SellRepairRow/RepairButton");
        var sell = GetNode<Button>("Column/SellRepairRow/SellButton");

        menu.Text = Tr("UI_BUTTON_MENU");
        stats.Text = Tr("UI_BUTTON_STATS");
        repair.Text = Tr("UI_BUTTON_REPAIR");
        sell.Text = Tr("UI_BUTTON_SELL");
        repair.TooltipText = Tr("UI_COMING_SOON");
        sell.TooltipText = Tr("UI_COMING_SOON");

        // The tabs follow BuildGroup in order: buildings, defense, infantry, vehicles.
        for (var i = 0; i < _tabs.Length; i++)
        {
            _tabs[i].Text = Tr(BuildGroups.LocalizationKey((BuildGroup)i));
            _tabs[i].AddThemeStyleboxOverride("pressed", _tabStyle);
            _tabs[i].AddThemeStyleboxOverride("hover_pressed", _tabStyle);
        }

        menu.Pressed += () => EmitSignal(SignalName.MenuRequested);
        stats.Pressed += () => EmitSignal(SignalName.StatsRequested);

        var grid = GetNode<GridContainer>("Column/Items/Grid");
        for (var i = 0; i < SidebarLayout.Slots; i++)
        {
            grid.AddChild(new BuildIcon());
        }
    }

    /// <summary>Shows the money and takes the faction's colour for the selected tab and the minimap frame.</summary>
    public void Setup(Faction faction, int credits)
    {
        var accent = new Color(FactionPalette.AccentHex(faction));
        _credits.Text = HudFormat.Credits(credits);
        _tabStyle.BgColor = accent;
        Minimap.SetFrameColor(accent);
    }
}
