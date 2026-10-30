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

    private StyleBoxFlat _tabStyle = null!;

    // The tabs not shown are dark, the shown one filled with the faction's colour (the owner, 2026-10-30); a tab with a building ready
    // to place, not shown, blinks between dark and a light fill.
    private StyleBoxFlat _tabIdle = null!;
    private StyleBoxFlat _tabBlink = null!;
    private readonly bool[] _tabLit = new bool[4];

    // A blink: lit and dark this long each.
    private const ulong BlinkMs = 400;
    private SimulationHost? _host;
    private PlayerInput? _input;
    private BuildGroup _group = BuildGroup.Buildings;

    // The pictures of the tabs, in the order of BuildGroup: a house, a shield, a soldier, a tank (as in RA2).
    private static readonly Glyph[] TabGlyphs = [Glyph.House, Glyph.Shield, Glyph.Person, Glyph.Tank];

    // The rows of the tab scrolled past with the mouse wheel: a tab with the items of a faction taken too can be longer than the grid.
    private int _scroll;
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
        _tabIdle = (StyleBoxFlat)_tabStyle.Duplicate();
        _tabIdle.BgColor = new Color("#2A2E35");
        _tabBlink = (StyleBoxFlat)_tabStyle.Duplicate();

        var menu = GetNode<Button>("Column/MenuRow/MenuButton");
        var stats = GetNode<Button>("Column/MenuRow/StatsButton");
        _repair = GetNode<Button>("Column/SellRepairRow/RepairButton");
        _sell = GetNode<Button>("Column/SellRepairRow/SellButton");

        // Pictures in place of the words (the owner, 2026-10-30); the words go to the tooltips.
        _repair.TooltipText = Tr("UI_SIDEBAR_REPAIR_HINT");
        _sell.TooltipText = Tr("UI_SIDEBAR_SELL_HINT");
        ButtonGlyph.Put(menu, Glyph.Menu, Tr("UI_BUTTON_MENU"));
        ButtonGlyph.Put(stats, Glyph.Stats, Tr("UI_BUTTON_STATS"));
        ButtonGlyph.Put(_repair, Glyph.Wrench, Tr("UI_BUTTON_REPAIR"));
        ButtonGlyph.Put(_sell, Glyph.Dollar, Tr("UI_BUTTON_SELL"));
        _repair.AddThemeStyleboxOverride("pressed", _tabStyle);
        _sell.AddThemeStyleboxOverride("pressed", _tabStyle);

        // The tabs follow BuildGroup in order: buildings, defense, infantry, vehicles.
        for (var i = 0; i < _tabs.Length; i++)
        {
            var group = (BuildGroup)i;
            ButtonGlyph.Put(_tabs[i], TabGlyphs[i], Tr(BuildGroups.LocalizationKey(group)));
            _tabs[i].AddThemeStyleboxOverride("pressed", _tabStyle);
            _tabs[i].AddThemeStyleboxOverride("hover_pressed", _tabStyle);
            _tabs[i].AddThemeStyleboxOverride("normal", _tabIdle);
            _tabs[i].AddThemeStyleboxOverride("hover", _tabIdle);
            // A click on a tab with a building ready takes it up for placing, as the tab's key does.
            _tabs[i].Pressed += () => ArmReady(group);
            _tabs[i].Toggled += pressed =>
            {
                if (pressed)
                {
                    (_group, _scroll) = (group, 0);
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
            icon.Scrolled += rows => _scroll += rows;
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
        _tabBlink.BgColor = accent.Lightened(0.35f);
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
            ArmReady((BuildGroup)tab);
        };
        _host = host;
        _input = input;
    }

    private IReadOnlyList<Cameo> _cameos = [];
    private (GameWorld? World, int Tick, BuildGroup Group, bool Names) _cameosOf = (null, -1, default, true);

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

        // The squares change with a tick of the simulation, a tab or the names: not every frame (a list built anew 60 times a second
        // was over a third of all the garbage of the game, the performance log, 2026-10-28).
        var names = AppSettings.Instance?.Values.Ra2Names ?? true;
        if (!ReferenceEquals(world, _cameosOf.World) || world.CurrentTick != _cameosOf.Tick || _group != _cameosOf.Group || names != _cameosOf.Names)
        {
            _cameos = SidebarModel.Cameos(world, player, _group, NameOf, Tr("UI_SIDEBAR_PARADROP"));
            _cameosOf = (world, world.CurrentTick, _group, names);
        }

        var cameos = _cameos;
        _scroll = SidebarModel.ScrollRows(cameos.Count, _scroll);
        var first = _scroll * SidebarLayout.Columns;
        // The superweapon armed for a click on the map: its square is lit.
        var armed = _input.Tool == PointerTool.Superweapon ? _input.Superweapon.Building : EntityId.None;
        for (var i = 0; i < _icons.Count; i++)
        {
            var cameo = first + i < cameos.Count && SidebarModel.TabOpen(world, player, _group) ? cameos[first + i] : null;
            _icons[i].Cameo = cameo is { Superweapon: { } superweapon } && superweapon == armed ? cameo with { Armed = true } : cameo;
        }

        _sell.SetPressedNoSignal(_input.Tool == PointerTool.Sell);
        _repair.SetPressedNoSignal(_input.Tool == PointerTool.Repair);
        _sell.Disabled = _host.IsPlayback;
        _repair.Disabled = _host.IsPlayback;
    }

    /// <summary>
    /// The name on screen of a unit or building: RA2's while the setting says so (DisplayNames). Kept once made: a name through the
    /// translation server is a new string every time, and the tab asks for every one on every tick.
    /// </summary>
    private string NameOf(string id, string name)
    {
        var ra2 = AppSettings.Instance?.Values.Ra2Names ?? true;
        if (ra2 != _namesRa2)
        {
            _names.Clear();
            _namesRa2 = ra2;
        }

        if (!_names.TryGetValue(id, out var shown))
        {
            shown = DisplayNames.Of(id, name, ra2, key => Tr(key));
            _names[id] = shown;
        }

        return shown;
    }

    private readonly Dictionary<string, string> _names = new(System.StringComparer.Ordinal);
    private bool _namesRa2 = true;

    /// <summary>With a building of the tab ready, its key or a click on it takes it up for placing, as a click on its square does.</summary>
    private void ArmReady(BuildGroup group)
    {
        if (_host is { IsPlayback: false } match && _input is { } placing
            && SidebarModel.ReadyToPlace(SidebarModel.Cameos(match.Simulation.World, SimulationHost.LocalPlayer, group, NameOf)) is { } ready)
        {
            placing.ArmPlacement(ready);
        }
    }

    /// <summary>
    /// Greys out the tabs without a factory. When the shown tab closes, or none was open and one opens, the first open tab is shown. A tab
    /// not shown with a building ready to place blinks.
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

        // A building ready to place on a tab not shown: the tab blinks until it is shown (the owner, 2026-10-30).
        var on = Time.GetTicksMsec() / BlinkMs % 2 == 0;
        for (var i = 0; i < _tabs.Length; i++)
        {
            var lit = on && i != (int)_group && SidebarModel.HasReadyToPlace(world, player, (BuildGroup)i);
            if (lit != _tabLit[i])
            {
                _tabLit[i] = lit;
                _tabs[i].AddThemeStyleboxOverride("normal", lit ? _tabBlink : _tabIdle);
                _tabs[i].AddThemeStyleboxOverride("hover", lit ? _tabBlink : _tabIdle);
            }
        }
    }

    private void OnIconClicked(Cameo cameo, bool right, bool shift)
    {
        if (_host is null || _input is null || _host.IsPlayback)
        {
            return;
        }

        // A superweapon: a click when it is ready arms it for the map, another click (or the right button) puts it away.
        if (cameo.Superweapon is { } building)
        {
            if (cameo.Armed || (!right && cameo.State == CameoState.Ready))
            {
                _input.ToggleSuperweapon(building);
            }

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
