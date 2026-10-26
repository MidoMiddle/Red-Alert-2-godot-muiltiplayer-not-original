using System.Collections.Generic;
using System.Linq;
using Godot;
using Rts.Client.App;
using Rts.Client.Controls;
using Rts.Simulation.World;

namespace Rts.Client.Hud;

/// <summary>
/// The sidebar on the right of the view (docs/08_ui_ux.md, section 4; docs/implementation/M3_base_economy.md, 4.17): money, the menu
/// buttons (Menu opens the pause menu, Stats shows the statistics), the minimap, repair and sell, the tabs of the build groups, the
/// item squares and the power strip on the left edge. Since M3 step 12 it shows the local player's economy every frame through
/// <see cref="SidebarModel"/>, and its clicks order, pause, cancel and place through the host and the input.
/// </summary>
public partial class Sidebar : Control
{
    [Signal]
    public delegate void MenuRequestedEventHandler();

    [Signal]
    public delegate void StatsRequestedEventHandler();

    private readonly List<BuildIcon> _icons = [];
    private Label _credits = null!;
    private Button[] _tabs = null!;
    private Button _repair = null!;
    private Button _sell = null!;
    private PowerBar _power = null!;

    // The superweapons of the player (stage 2d): a button each, above repair and sell.
    private readonly Dictionary<EntityId, Button> _superweapons = new();
    private VBoxContainer _superweaponColumn = null!;
    private StyleBoxFlat _tabStyle = null!;
    private SimulationHost? _host;
    private PlayerInput? _input;
    private BuildGroup _group = BuildGroup.Buildings;
    private int _shownCredits = int.MinValue;

    /// <summary>The minimap at the top of the sidebar.</summary>
    public Minimap Minimap => GetNode<Minimap>("Column/Minimap");

    public override void _Ready()
    {
        OffsetLeft = -SidebarLayout.Width;

        _credits = GetNode<Label>("Column/CreditsBar/Credits");
        _power = GetNode<PowerBar>("PowerBar");
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
        _repair = GetNode<Button>("Column/SellRepairRow/RepairButton");
        _superweaponColumn = new VBoxContainer { Name = "Superweapons" };
        var column = GetNode<Control>("Column");
        column.AddChild(_superweaponColumn);
        column.MoveChild(_superweaponColumn, GetNode("Column/SellRepairRow").GetIndex());
        _sell = GetNode<Button>("Column/SellRepairRow/SellButton");

        menu.Text = Tr("UI_BUTTON_MENU");
        stats.Text = Tr("UI_BUTTON_STATS");
        _repair.Text = Tr("UI_BUTTON_REPAIR");
        _sell.Text = Tr("UI_BUTTON_SELL");
        _repair.TooltipText = Tr("UI_SIDEBAR_REPAIR_HINT");
        _sell.TooltipText = Tr("UI_SIDEBAR_SELL_HINT");
        _repair.AddThemeStyleboxOverride("pressed", _tabStyle);
        _sell.AddThemeStyleboxOverride("pressed", _tabStyle);

        // The tabs follow BuildGroup in order: buildings, defense, infantry, vehicles.
        for (var i = 0; i < _tabs.Length; i++)
        {
            var group = (BuildGroup)i;
            _tabs[i].Text = Tr(BuildGroups.LocalizationKey(group));
            _tabs[i].AddThemeStyleboxOverride("pressed", _tabStyle);
            _tabs[i].AddThemeStyleboxOverride("hover_pressed", _tabStyle);
            _tabs[i].Toggled += pressed =>
            {
                if (pressed)
                {
                    _group = group;
                }
            };
        }

        menu.Pressed += () => EmitSignal(SignalName.MenuRequested);
        stats.Pressed += () => EmitSignal(SignalName.StatsRequested);
        _repair.Pressed += () => _input?.ToggleRepair();
        _sell.Pressed += () => _input?.ToggleSell();

        var grid = GetNode<GridContainer>("Column/Items/Grid");
        for (var i = 0; i < SidebarLayout.Slots; i++)
        {
            var icon = new BuildIcon();
            icon.Clicked += OnIconClicked;
            grid.AddChild(icon);
            _icons.Add(icon);
        }
    }

    /// <summary>Takes the faction's colour for the selected tab, the squares and the minimap frame, and shows the starting money.</summary>
    public void Setup(Faction faction, int credits)
    {
        var accent = new Color(FactionPalette.AccentHex(faction));
        _credits.Text = HudFormat.Credits(credits);
        _tabStyle.BgColor = accent;
        Minimap.SetFrameColor(accent);
        foreach (var icon in _icons)
        {
            icon.SetAccent(accent);
        }
    }

    /// <summary>Connects the sidebar to the match: it reads the local player's economy and sends its clicks.</summary>
    public void Bind(SimulationHost host, PlayerInput input)
    {
        input.TabRequested += tab =>
        {
            // A tab without a factory stays shut.
            if (tab < 0 || tab >= _tabs.Length || _tabs[tab].Disabled)
            {
                return;
            }

            _tabs[tab].ButtonPressed = true;

            // With a building of the tab ready, its key takes it up for placing, as a click on its square does.
            if (_host is { IsPlayback: false } match && _input is { } placing
                && SidebarModel.ReadyToPlace(SidebarModel.Cameos(match.Simulation.World, SimulationHost.LocalPlayer, (BuildGroup)tab, NameOf)) is { } ready)
            {
                placing.ArmPlacement(ready);
            }
        };
        _host = host;
        _input = input;
    }

    public override void _Process(double delta)
    {
        using var frameTimer = Rts.Client.Debug.PerfLog.Time("Sidebar.process");
        if (_host is null || _input is null)
        {
            return;
        }

        var world = _host.Simulation.World;
        var player = SimulationHost.LocalPlayer;
        var state = world.Player(player);

        var credits = _shownCredits == int.MinValue ? state.Credits : CreditsRoll.Next(_shownCredits, state.Credits);
        if (credits != _shownCredits)
        {
            _shownCredits = credits;
            _credits.Text = HudFormat.Credits(credits);
        }

        _power.Show(state.PowerOutput, state.PowerDrain, state.LowPower);
        Minimap.Online = MinimapRule.Online(world, player, _host.RadarRule);
        UpdateTabs(world, player);

        var cameos = SidebarModel.Cameos(world, player, _group, NameOf);
        for (var i = 0; i < _icons.Count; i++)
        {
            _icons[i].Cameo = i < cameos.Count && SidebarModel.TabOpen(world, player, _group) ? cameos[i] : null;
        }

        UpdateSuperweapons(world, player);
        _sell.SetPressedNoSignal(_input.Tool == PointerTool.Sell);
        _repair.SetPressedNoSignal(_input.Tool == PointerTool.Repair);
        _sell.Disabled = _host.IsPlayback;
        _repair.Disabled = _host.IsPlayback;
    }

    /// <summary>The name on screen of a unit or building: RA2's while the setting says so (DisplayNames).</summary>
    private string NameOf(string id, string name) =>
        DisplayNames.Of(id, name, AppSettings.Instance?.Values.Ra2Names ?? true, key => Tr(key));

    /// <summary>
    /// A button for every ready superweapon of the player (stage 2d): its name and the time it still charges, or Ready; pressed, it arms
    /// the superweapon for a click on the map.
    /// </summary>
    private void UpdateSuperweapons(GameWorld world, PlayerId player)
    {
        var seen = new HashSet<EntityId>();
        foreach (var entity in world.Entities)
        {
            if (entity is not Building { IsAlive: true, IsReady: true } building || building.Owner != player
                || Rts.Simulation.Systems.SuperweaponSystem.KindOf(world, building) == Rts.Simulation.Rules.SuperweaponKind.None)
            {
                continue;
            }

            seen.Add(building.Id);
            if (!_superweapons.TryGetValue(building.Id, out var button))
            {
                var id = building.Id;
                button = new Button { ToggleMode = true, TooltipText = Tr("UI_SIDEBAR_SUPERWEAPON_HINT"), ClipText = true };
                button.Pressed += () => _input?.ToggleSuperweapon(id);
                _superweaponColumn.AddChild(button);
                _superweapons.Add(id, button);
            }

            var ready = building.ChargeLeft == 0;
            var seconds = (building.ChargeLeft + Rts.Simulation.SimulationSettings.DefaultTicksPerSecond - 1) / Rts.Simulation.SimulationSettings.DefaultTicksPerSecond;
            var name = building.Def is { } named ? NameOf(named.Id, named.Name) : Tr("UI_SIDEBAR_PARADROP");
            button.Text = $"{name}  {(ready ? Tr("UI_SIDEBAR_SUPERWEAPON_READY") : $"{seconds / 60}:{seconds % 60:00}")}";
            button.Disabled = !ready || _host!.IsPlayback;
            button.SetPressedNoSignal(_input!.Tool == PointerTool.Superweapon && _input.Superweapon.Building == building.Id);
        }

        foreach (var id in _superweapons.Keys.Where(id => !seen.Contains(id)).ToList())
        {
            _superweapons[id].QueueFree();
            _superweapons.Remove(id);
        }
    }

    /// <summary>
    /// Greys out the tabs without a factory. When the shown tab closes, or none was open and one opens, the first open tab is shown.
    /// </summary>
    private void UpdateTabs(GameWorld world, PlayerId player)
    {
        var firstOpen = -1;
        for (var i = 0; i < _tabs.Length; i++)
        {
            var open = SidebarModel.TabOpen(world, player, (BuildGroup)i);
            _tabs[i].Disabled = !open;
            if (open && firstOpen < 0)
            {
                firstOpen = i;
            }
        }

        if (_tabs[(int)_group].Disabled && firstOpen >= 0)
        {
            _tabs[firstOpen].ButtonPressed = true;
        }
    }

    private void OnIconClicked(Cameo cameo, bool right, bool shift)
    {
        if (_host is null || _input is null || _host.IsPlayback)
        {
            return;
        }

        var action = right ? SidebarModel.RightClick(cameo) : SidebarModel.LeftClick(cameo, shift);
        switch (action.Kind)
        {
            case CameoActionKind.Produce:
                _host.IssueProduce(action.Item, action.DefIndex, action.Count);
                break;
            case CameoActionKind.Pause:
                _host.IssuePause(action.Item, action.DefIndex);
                break;
            case CameoActionKind.Cancel:
                _host.IssueCancel(action.Item, action.DefIndex);
                break;
            case CameoActionKind.Place:
                _input.ArmPlacement(action.DefIndex);
                break;
        }
    }
}
