using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;
using Rts.Client.App;
using Rts.Client.Hud;
using Rts.Client.Maps;
using Rts.Client.Matches;
using Rts.Data.Maps;
using Rts.Simulation.Matches;

namespace Rts.Client.UI;

/// <summary>
/// The skirmish screen as in RA2 (the owner's wish, 2026-10-24; before: docs/menu/README.md, section 3.3; M6, 4.3). Two pages share it:
/// <list type="bullet">
/// <item><b>Skirmish Game</b>: the players — who (the person, a bot of a level with its personality beside, or closed), the flag and the
/// Side (a country, a random country of a faction, or random: <see cref="SkirmishConfig.SideOf"/>), the colour, the start and the team
/// A–D — then the options of RA2 in their order, those without rules yet locked with "Coming soon"; on the right the preview of the map
/// chosen, the game type and the map, Start Game, Customize Battle and Back.</item>
/// <item><b>Customize Battle</b>: the game types (only Battle plays yet) and the maps of <see cref="MapCatalog"/>, sorted by name or by
/// players, filtered by players and by a search; on the right the preview of the map under the cursor, Use Map, Random Map… (the
/// generator's settings instead of the list), Map Editor and Cancel.</item>
/// </list>
/// <see cref="SkirmishConfig"/> keeps the logic and the last config, the map with it, in <see cref="ConfigPath"/>. Start opens the match
/// as Quick Skirmish does.
/// </summary>
public partial class SkirmishSetup : Control
{
    private const float ActionWidth = 360f;
    private const float ActionHeight = 56f;
    private const float FieldWidth = 150f;
    private const float WhoWidth = 220f;
    private const float PersonalityWidth = 120f;
    private const float FieldHeight = 44f;
    private const float SliderWidth = 200f;
    private const int PlayerColumns = 7;
    private const int RowSeparation = 10;
    private const int SwatchSize = 16;
    private const string MatchScene = "res://scenes/Main.tscn";
    private const string EditorScene = "res://scenes/editor/MapEditor.tscn";

    /// <summary>The last config of the screen, for the next time it opens.</summary>
    public const string ConfigPath = "user://skirmish.cfg";

    /// <summary>
    /// The random map a match is started on: out of <see cref="MapCatalog.UserDirectory"/>, so it does not join the list of maps; the
    /// replay keeps the map itself.
    /// </summary>
    public const string RandomMapPath = "user://skirmish/random.rtsmap";

    // The factions the screen knows, in the order of the Side list: the ids of the data.
    private static readonly string[] FactionIds = ["alliance", "union", "cult"];

    // The game types of RA2, in its order, by their names; only Battle (the first) plays yet.
    private static readonly string[] GameTypes =
    [
        "UI_GAME_TYPE_BATTLE", "UI_GAME_TYPE_FREE_FOR_ALL", "UI_GAME_TYPE_UNHOLY_ALLIANCE", "UI_GAME_TYPE_MEGAWEALTH", "UI_GAME_TYPE_LAND_RUSH",
        "UI_GAME_TYPE_MEAT_GRINDER", "UI_GAME_TYPE_NAVAL_WAR", "UI_GAME_TYPE_TEAM_ALLIANCE",
    ];

    // The names of the colours of PlayerPalette, in its order.
    private static readonly string[] ColorKeys =
    [
        "UI_COLOR_BLUE", "UI_COLOR_RED", "UI_COLOR_GREEN", "UI_COLOR_YELLOW",
        "UI_COLOR_ORANGE", "UI_COLOR_PURPLE", "UI_COLOR_TEAL", "UI_COLOR_PINK",
    ];

    // The filters of the map list by players: all, two, up to four, more.
    private static readonly (int Min, int Max)[] PlayerFilters = [(0, 99), (2, 2), (3, 4), (5, 99)];

    private List<MapEntry> _maps = [];
    private MapPreviewView _preview = null!;
    private Label _previewLabel = null!;
    private Label _header = null!;
    private Label _mapInfo = null!;
    private VBoxContainer _actions = null!;
    private Control _gamePage = null!;
    private Control _customizePage = null!;
    private GridContainer _players = null!;
    private readonly List<PlayerRow> _rows = [];
    private Button _start = null!;
    private SkirmishConfig _config = SkirmishConfig.For(0);

    // The options of the Game page.
    private CheckBox _shortGame = null!;
    private CheckBox _repacks = null!;
    private CheckBox _superweapons = null!;
    private CheckBox _bridges = null!;
    private CheckBox _fog = null!;
    private HSlider _speed = null!;
    private Label _speedValue = null!;
    private HSlider _credits = null!;
    private Label _creditsValue = null!;

    // The map chosen for the match, and the one the Customize page shows.
    private MapEntry? _chosen;
    private MapEntry? _browsed;
    private MapEntry? _generated;

    // The Customize page: the list of maps as it is shown, and the controls that shape it.
    private ItemList _mapList = null!;
    private readonly List<MapEntry> _listed = [];
    private OptionButton _filter = null!;
    private OptionButton _originFilter = null!;
    private LineEdit _search = null!;
    private bool _byPlayers;
    private Control _listBox = null!;
    private Control _randomBox = null!;

    // «random», then the personalities of the data, «Balanced» first.
    private List<string> _personalities = [];

    // The sides of the Side column, in its order (SkirmishConfig.SideOf).
    private List<string> _sides = [];

    public override void _Ready()
    {
        ScreenshotCapture.AttachIfRequested(this);

        _header = GetNode<Label>("Column/Scroll/Stack/Header/HeaderLabel");
        GetNode<Label>("Column/Scroll/Stack/MapLabel").Visible = false;
        GetNode<Control>("Column/Scroll/Stack/MapBox").Visible = false;
        _previewLabel = GetNode<Label>("Column/Scroll/Stack/Preview/PreviewLabel");
        _mapInfo = GetNode<Label>("Column/Scroll/Stack/MapInfo");
        // A long description wraps instead of widening the column.
        _mapInfo.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _mapInfo.CustomMinimumSize = new Vector2(ActionWidth, 0);
        _mapInfo.HorizontalAlignment = HorizontalAlignment.Center;
        _actions = GetNode<VBoxContainer>("Column/Scroll/Stack/Actions");
        _preview = new MapPreviewView { Name = "PreviewImage", MouseFilter = MouseFilterEnum.Ignore };
        _preview.SetAnchorsPreset(LayoutPreset.FullRect);
        GetNode<Panel>("Column/Scroll/Stack/Preview").AddChild(_preview);
        _personalities =
        [
            MatchPlayer.RandomPersonality,
            .. DataFiles.Current.Rules.Database.Personalities
                .Select(personality => personality.Id)
                .OrderBy(id => id == MatchPlayer.DefaultPersonality ? 0 : 1),
        ];
        _sides = Sides();
        _config = LoadConfig() ?? _config;

        var content = GetNode<VBoxContainer>("Screen/Scroll/Content");
        _gamePage = BuildGamePage();
        _customizePage = BuildCustomizePage();
        content.AddChild(_gamePage);
        content.AddChild(_customizePage);
        LoadMaps();
        ShowGamePage();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel"))
        {
            if (_customizePage.Visible)
            {
                ShowGamePage();
            }
            else
            {
                OnBackPressed();
            }
        }
    }

    // ---- The pages and the buttons of the right column.

    private void ShowGamePage()
    {
        _gamePage.Visible = true;
        _customizePage.Visible = false;
        _header.Text = Tr("UI_SKIRMISH_GAME");
        ClearActions();
        _start = AddAction("StartGame", "UI_SKIRMISH_START_GAME", StartGame);
        AddAction("CustomizeBattle", "UI_CUSTOMIZE_BATTLE", ShowCustomizePage);
        _actions.AddChild(new Control { CustomMinimumSize = new Vector2(0, ActionHeight) });
        var back = AddAction("Back", "UI_BUTTON_BACK", OnBackPressed);
        if (_chosen is not null)
        {
            ShowPreview(_chosen);
        }
        else
        {
            _preview.Show(null, null);
            _previewLabel.Text = Tr("UI_MAP_PREVIEW");
            _mapInfo.Text = Tr("UI_MAP_INFO_NONE");
        }

        Refresh();
        (_start.Disabled ? back : _start).GrabFocus();
    }

    private void ShowCustomizePage()
    {
        _gamePage.Visible = false;
        _customizePage.Visible = true;
        _header.Text = Tr("UI_CUSTOMIZE_BATTLE");
        ClearActions();
        AddAction("UseMap", "UI_USE_MAP", UseMap);
        AddAction("RandomMap", "UI_RANDOM_MAP_BUTTON", ShowRandomMap);
        AddAction("MapEditor", "UI_MAP_EDITOR", () => SceneRouter.Instance?.GoTo(EditorScene));
        _actions.AddChild(new Control { CustomMinimumSize = new Vector2(0, ActionHeight) });
        AddAction("Cancel", "UI_BUTTON_CANCEL", ShowGamePage);
        _listBox.Visible = true;
        _randomBox.Visible = false;
        _browsed = _chosen;
        FillMapList();
        if (_browsed is not null)
        {
            ShowPreview(_browsed);
        }
    }

    private void ClearActions()
    {
        foreach (var child in _actions.GetChildren())
        {
            _actions.RemoveChild(child);
            child.QueueFree();
        }
    }

    private Button AddAction(string name, string key, Action pressed)
    {
        var button = new Button { Name = name, Text = Tr(key), CustomMinimumSize = new Vector2(ActionWidth, ActionHeight) };
        button.Pressed += pressed;
        _actions.AddChild(button);
        return button;
    }

    // ---- The Game page.

    private Control BuildGamePage()
    {
        var page = new VBoxContainer { Name = "GamePage", SizeFlagsVertical = SizeFlags.ExpandFill };
        page.AddThemeConstantOverride("separation", 24);
        _players = new GridContainer { Name = "Players", Columns = PlayerColumns };
        _players.AddThemeConstantOverride("h_separation", 8);
        _players.AddThemeConstantOverride("v_separation", RowSeparation);
        foreach (var key in new[] { "UI_SKIRMISH_PLAYERS", "", "", "UI_SKIRMISH_SIDE", "UI_SKIRMISH_COLOR", "UI_SKIRMISH_START_POSITION", "UI_SKIRMISH_TEAM" })
        {
            _players.AddChild(Caption(key.Length == 0 ? "" : Tr(key), header: true));
        }

        page.AddChild(_players);
        page.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
        page.AddChild(BuildOptions());
        return page;
    }

    /// <summary>The rows of the table for <see cref="_config"/>, made again when the map changes the number of starts.</summary>
    private void BuildRows()
    {
        foreach (var row in _rows)
        {
            foreach (var control in row.Controls)
            {
                _players.RemoveChild(control);
                control.QueueFree();
            }
        }

        _rows.Clear();
        for (var index = 0; index < _config.Slots.Length; index++)
        {
            var row = index;
            Control who;
            if (row == 0)
            {
                var box = new PanelContainer { CustomMinimumSize = new Vector2(WhoWidth, FieldHeight) };
                box.AddChild(new Label { Text = Tr("UI_SKIRMISH_PLAYER_ONE"), VerticalAlignment = VerticalAlignment.Center });
                who = box;
            }
            else
            {
                // Closed, or a bot of a level: one dropdown, as in RA2.
                var kind = Option($"Who{row}", WhoWidth, Tr("UI_SLOT_CLOSED"));
                foreach (var level in SkirmishConfig.Levels)
                {
                    kind.AddItem(string.Format(Tr("UI_SKIRMISH_ENEMY"), SkirmishNames.Level(this, level)));
                    kind.SetItemTooltip(kind.ItemCount - 1, SkirmishNames.LevelHint(this, level));
                }

                kind.ItemSelected += item => Change(row, slot => item == 0
                    ? slot with { Kind = SlotKind.Closed }
                    : slot with { Kind = SlotKind.Bot, Difficulty = SkirmishConfig.Levels[(int)item - 1] });
                who = kind;
            }

            var personality = Option($"Personality{row}", PersonalityWidth);
            foreach (var id in _personalities)
            {
                personality.AddItem(SkirmishNames.Personality(this, id));
                personality.SetItemTooltip(personality.ItemCount - 1, SkirmishNames.PersonalityHint(this, id));
            }

            personality.ItemSelected += item => Change(row, slot => slot with { Personality = _personalities[(int)item] });

            var flag = new TextureRect
            {
                CustomMinimumSize = new Vector2(Flags.Width, FieldHeight),
                StretchMode = TextureRect.StretchModeEnum.KeepCentered,
            };
            var unknown = new Label { Text = Tr("UI_RANDOM_SHORT"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            unknown.SetAnchorsPreset(LayoutPreset.FullRect);
            flag.AddChild(unknown);

            var side = Option($"Side{row}", FieldWidth + 60f);
            foreach (var id in _sides)
            {
                side.AddItem(SideName(id));
            }

            side.ItemSelected += item => Change(row, slot => SkirmishConfig.WithSide(slot, _sides[(int)item], DataFiles.Current.Rules.Database.Countries));

            var color = Option($"Color{row}", FieldWidth);
            for (var i = 0; i < PlayerPalette.Colors.Length; i++)
            {
                color.AddIconItem(Swatch(i), Tr(ColorKeys[i]));
            }

            color.ItemSelected += item => Apply(_config.WithColor(row, (int)item));

            var start = Option($"Start{row}", FieldWidth * 0.6f, Tr("UI_RANDOM_SHORT"));
            for (var i = 1; i <= _config.Slots.Length; i++)
            {
                start.AddItem(i.ToString(CultureInfo.InvariantCulture));
            }

            start.ItemSelected += item => Apply(_config.WithStart(row, item == 0 ? SkirmishConfig.RandomStart : (int)item - 1));

            var team = Option($"Team{row}", FieldWidth * 0.6f, Tr("UI_SKIRMISH_COUNTRY_DASH"), Tr("UI_TEAM_A"), Tr("UI_TEAM_B"), Tr("UI_TEAM_C"), Tr("UI_TEAM_D"));
            team.ItemSelected += item => Change(row, slot => slot with { Team = (int)item });

            var controls = new[] { who, row == 0 ? new Control() : personality, flag, side, color, start, team };
            foreach (var control in controls)
            {
                _players.AddChild(control);
            }

            _rows.Add(new PlayerRow(controls, who as OptionButton, personality, flag, unknown, side, color, start, team));
        }

        Refresh();
    }

    /// <summary>
    /// The options of RA2 in its order: on the left Short Game, MCV Repacks, Crates, Superweapons, Destroyable Bridges; in the middle
    /// Multi Engineer, Instant Capture, No Dog Engineer Kills, Delayed Oils and our fog of war; on the right the game speed, the credits,
    /// the starting units and Build Off Ally ConYards. What has no rules yet is locked with "Coming soon".
    /// </summary>
    private Control BuildOptions()
    {
        var grid = new HBoxContainer { Name = "Options" };
        grid.AddThemeConstantOverride("separation", 48);

        var left = Column(grid);
        _shortGame = Check(left, "UI_OPTION_SHORT_GAME", on => Apply(_config with { ShortGame = on }));
        _repacks = Check(left, "UI_OPTION_MCV_REPACKS", on => Apply(_config with { McvRepacks = on }));
        Locked(left, "UI_OPTION_CRATES", false);
        _superweapons = Check(left, "UI_OPTION_SUPERWEAPONS", on => Apply(_config with { Superweapons = on }));
        _bridges = Check(left, "UI_OPTION_DESTROYABLE_BRIDGES", on => Apply(_config with { DestroyableBridges = on }));

        var middle = Column(grid);
        Locked(middle, "UI_OPTION_MULTI_ENGINEER", false);
        Locked(middle, "UI_OPTION_INSTANT_CAPTURE", true);
        Locked(middle, "UI_OPTION_NO_DOG_ENGINEER_KILLS", false);
        Locked(middle, "UI_OPTION_DELAYED_OILS", false);
        _fog = Check(middle, "UI_OPTION_FOG_OF_WAR", on => Apply(_config with { FogOfWar = on }));
        _fog.TooltipText = Tr("UI_OPTION_FOG_OF_WAR_HINT");

        var right = new GridContainer { Columns = 3 };
        right.AddThemeConstantOverride("h_separation", 12);
        right.AddThemeConstantOverride("v_separation", RowSeparation);
        grid.AddChild(right);
        (_speed, _speedValue) = Slider(right, "UI_OPTION_GAME_SPEED", 1, SkirmishConfig.MaxGameSpeed, value => Apply(_config with { GameSpeed = value }));
        (_credits, _creditsValue) = Slider(right, "UI_OPTION_CREDITS", 0, SkirmishConfig.CreditChoices.Length - 1,
            value => Apply(_config with { Credits = SkirmishConfig.CreditChoices[value] }));
        var (units, unitsValue) = Slider(right, "UI_OPTION_UNIT_COUNT", 0, 10, _ => { });
        units.Editable = false;
        units.Modulate = new Color(1f, 1f, 1f, 0.5f);
        units.TooltipText = Tr("UI_COMING_SOON");
        unitsValue.Text = 0.ToString(CultureInfo.InvariantCulture);
        right.AddChild(new Control());
        Locked(right, "UI_OPTION_ALLY_CONYARDS", false);
        return grid;
    }

    private static VBoxContainer Column(Control parent)
    {
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", RowSeparation);
        parent.AddChild(column);
        return column;
    }

    private CheckBox Check(Control parent, string key, Action<bool> toggled)
    {
        var box = new CheckBox { Text = Tr(key), Name = key };
        box.Toggled += on => toggled(on);
        parent.AddChild(box);
        return box;
    }

    private void Locked(Control parent, string key, bool pressed)
    {
        parent.AddChild(new CheckBox { Text = Tr(key), ButtonPressed = pressed, Disabled = true, TooltipText = Tr("UI_COMING_SOON") });
    }

    private (HSlider Slider, Label Value) Slider(GridContainer parent, string key, int min, int max, Action<int> changed)
    {
        parent.AddChild(Caption(Tr(key)));
        var slider = new HSlider
        {
            MinValue = min,
            MaxValue = max,
            Step = 1,
            CustomMinimumSize = new Vector2(SliderWidth, FieldHeight),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        slider.ValueChanged += value => changed((int)value);
        parent.AddChild(slider);
        var label = new Label { CustomMinimumSize = new Vector2(80f, 0), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        parent.AddChild(label);
        return (slider, label);
    }

    /// <summary>Row <paramref name="row"/> changed by <paramref name="change"/>.</summary>
    private void Change(int row, Func<SkirmishSlot, SkirmishSlot> change) => Apply(_config.With(row, change(_config.Slots[row])));

    /// <summary>Takes <paramref name="config"/>, shows it and keeps it for the next time.</summary>
    private void Apply(SkirmishConfig config)
    {
        _config = config;
        Refresh();
        SaveConfig();
    }

    /// <summary>Every control shows <see cref="_config"/>; a closed row has no personality; Start waits for an enemy and a map.</summary>
    private void Refresh()
    {
        var faction = DataFiles.Current.Rules.DefaultFaction.Id;
        for (var i = 0; i < _rows.Count; i++)
        {
            var slot = _config.Slots[i];
            var row = _rows[i];
            var bot = slot.Kind == SlotKind.Bot;
            if (row.Who is not null)
            {
                row.Who.Selected = bot ? SkirmishConfig.Levels.IndexOf(slot.Difficulty) + 1 : 0;
            }

            row.Personality.Selected = _personalities.IndexOf(slot.Personality);
            row.Personality.Disabled = !bot;
            row.Personality.TooltipText = SkirmishNames.PersonalityHint(this, slot.Personality);
            var side = SkirmishConfig.SideOf(slot, faction);
            row.Side.Selected = Math.Max(0, _sides.IndexOf(side));
            row.Side.TooltipText = bot ? Tr("UI_SKIRMISH_SIDE_BOT_HINT") : Tr("UI_SKIRMISH_COUNTRY_HINT");
            var flag = Flags.Of(side);
            row.Flag.Texture = flag;
            row.Unknown.Visible = flag is null;
            row.Color.Selected = slot.Color;
            row.Team.Selected = slot.Team;
            row.Start.Selected = slot.Start == SkirmishConfig.RandomStart ? 0 : slot.Start + 1;
            foreach (var control in new Control[] { row.Side, row.Color, row.Team, row.Start, row.Flag })
            {
                control.Modulate = slot.Kind == SlotKind.Closed ? new Color(1f, 1f, 1f, 0.4f) : Colors.White;
            }
        }

        _shortGame.SetPressedNoSignal(_config.ShortGame);
        _repacks.SetPressedNoSignal(_config.McvRepacks);
        _superweapons.SetPressedNoSignal(_config.Superweapons);
        _bridges.SetPressedNoSignal(_config.DestroyableBridges);
        _fog.SetPressedNoSignal(_config.FogOfWar);
        _speed.SetValueNoSignal(_config.GameSpeed);
        _speedValue.Text = _config.GameSpeed.ToString(CultureInfo.InvariantCulture);
        var credits = Math.Max(0, SkirmishConfig.CreditChoices.IndexOf(_config.Credits));
        _credits.SetValueNoSignal(credits);
        _creditsValue.Text = SkirmishConfig.CreditChoices[credits].ToString(CultureInfo.InvariantCulture);
        if (_gamePage.Visible && IsInstanceValid(_start))
        {
            _start.Disabled = _chosen is null || !_config.CanStart;
            _start.TooltipText = _chosen is null ? Tr("UI_SKIRMISH_START_NEEDS_MAP") : _config.CanStart ? "" : Tr("UI_SKIRMISH_START_NEEDS_ENEMY");
        }
    }

    /// <summary>
    /// The sides of the Side column (RA2): Random, a random country of every faction with countries, the countries of the Alliance and
    /// of the Union, then the factions without countries (the Cult, locked until the data has it).
    /// </summary>
    private static List<string> Sides()
    {
        var database = DataFiles.Current.Rules.Database;
        var sides = new List<string> { SkirmishConfig.RandomSide };
        foreach (var faction in FactionIds.Where(id => database.Countries.Any(country => country.Faction == id)))
        {
            sides.Add(SkirmishConfig.RandomSidePrefix + faction);
        }

        foreach (var faction in FactionIds)
        {
            sides.AddRange(database.Countries.Where(country => country.Faction == faction).Select(country => country.Id));
        }

        sides.AddRange(FactionIds.Where(id => database.TryGetFaction(id, out _) && !database.Countries.Any(country => country.Faction == id)));
        return sides;
    }

    /// <summary>The name of a side: «Random», «Random Allies», a country (RA2's name for now), a faction.</summary>
    private string SideName(string side)
    {
        if (side == SkirmishConfig.RandomSide)
        {
            return Tr("UI_RANDOM_SIDE");
        }

        if (side.StartsWith(SkirmishConfig.RandomSidePrefix, StringComparison.Ordinal))
        {
            return side[SkirmishConfig.RandomSidePrefix.Length..] switch
            {
                "alliance" => Tr("UI_RANDOM_SIDE_ALLIANCE"),
                "union" => Tr("UI_RANDOM_SIDE_UNION"),
                _ => Tr("UI_RANDOM_SIDE_CULT"),
            };
        }

        var database = DataFiles.Current.Rules.Database;
        if (database.TryGetCountry(side, out var country))
        {
            return DisplayNames.Of(side, country.Name, AppSettings.Instance?.Values.Ra2Names ?? true, key => Tr(key));
        }

        return database.TryGetFaction(side, out var faction) ? faction.Name : side;
    }

    /// <summary>The rows for the starts of <paramref name="map"/>, keeping what can be kept of the rows before; personalities the data lost are random.</summary>
    private void SetSlots(Rts.Data.Maps.MapDefinition map)
    {
        var config = SkirmishConfig.For(map.Starts.Length, _config) with { MapId = map.Id };
        for (var row = 0; row < config.Slots.Length; row++)
        {
            if (!_personalities.Contains(config.Slots[row].Personality))
            {
                config = config.With(row, config.Slots[row] with { Personality = MatchPlayer.RandomPersonality });
            }
        }

        var rebuild = config.Slots.Length != _rows.Count;
        _config = config;
        if (rebuild)
        {
            BuildRows();
        }
        else
        {
            Refresh();
        }

        SaveConfig();
    }

    // ---- The Customize page.

    private Control BuildCustomizePage()
    {
        var page = new VBoxContainer { Name = "CustomizePage", SizeFlagsVertical = SizeFlags.ExpandFill };
        page.AddThemeConstantOverride("separation", 24);
        page.AddChild(new Label { Text = Tr("UI_SELECT_ENGAGEMENT"), HorizontalAlignment = HorizontalAlignment.Center });
        var row = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 48);
        page.AddChild(row);

        var types = Column(row);
        types.AddChild(Caption(Tr("UI_GAME_TYPE"), header: true));
        var typeList = new ItemList { Name = "GameTypes", CustomMinimumSize = new Vector2(320f, 560f) };
        foreach (var type in GameTypes)
        {
            var item = typeList.AddItem(Tr(type));
            if (item > 0)
            {
                // Each mode has rules of its own: they come later.
                typeList.SetItemDisabled(item, true);
                typeList.SetItemTooltip(item, Tr("UI_COMING_SOON"));
            }
        }

        typeList.Select(0);
        types.AddChild(typeList);

        _listBox = Column(row);
        _listBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 12);
        header.AddChild(Caption(Tr("UI_GAME_MAP"), header: true));
        header.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        var sort = new Button { Text = Tr("UI_MAP_SORT"), TooltipText = Tr("UI_MAP_SORT_HINT"), CustomMinimumSize = new Vector2(FieldHeight, FieldHeight) };
        sort.Pressed += () =>
        {
            _byPlayers = !_byPlayers;
            FillMapList();
        };
        header.AddChild(sort);
        _filter = Option("MapFilter", FieldWidth, Tr("UI_MAP_FILTER_ALL"), Tr("UI_MAP_FILTER_TWO"), Tr("UI_MAP_FILTER_FOUR"), Tr("UI_MAP_FILTER_MORE"));
        _filter.ItemSelected += _ => FillMapList();
        header.AddChild(_filter);
        // Where the maps come from: those of RA2, the hand-made, the generated…, so it is plain which are kept.
        _originFilter = Option("MapOriginFilter", FieldWidth, [Tr("UI_MAP_ORIGIN_ALL"), .. MapOrigin.Ids.Select(id => Tr(MapCatalog.OriginKey(id)))]);
        _originFilter.ItemSelected += _ => FillMapList();
        header.AddChild(_originFilter);
        _listBox.AddChild(header);
        _mapList = new ItemList { Name = "MapList", CustomMinimumSize = new Vector2(520f, 480f), SizeFlagsVertical = SizeFlags.ExpandFill };
        _mapList.ItemSelected += index => Browse(_listed[(int)index]);
        _mapList.ItemActivated += index =>
        {
            Browse(_listed[(int)index]);
            UseMap();
        };
        _listBox.AddChild(_mapList);
        var searchRow = new HBoxContainer();
        searchRow.AddThemeConstantOverride("separation", 12);
        searchRow.AddChild(Caption(Tr("UI_SEARCH")));
        _search = new LineEdit { SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, FieldHeight) };
        _search.TextChanged += _ => FillMapList();
        searchRow.AddChild(_search);
        _listBox.AddChild(searchRow);

        // Random Map…: the generator's settings instead of the list.
        _randomBox = Column(row);
        _randomBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _randomBox.Visible = false;
        _randomBox.AddChild(Caption(Tr("UI_MAP_RANDOM"), header: true));
        var options = new MapGenOptions { Name = "RandomMapOptions", Columns = 2 };
        _randomBox.AddChild(options);
        var generate = new Button { Text = Tr("UI_EDITOR_GENERATE"), CustomMinimumSize = new Vector2(0, FieldHeight) };
        generate.Pressed += () => GenerateRandom(options.Settings);
        _randomBox.AddChild(generate);
        var toList = new Button { Text = Tr("UI_MAP_BACK_TO_LIST"), CustomMinimumSize = new Vector2(0, FieldHeight) };
        toList.Pressed += () =>
        {
            _randomBox.Visible = false;
            _listBox.Visible = true;
        };
        _randomBox.AddChild(toList);
        return page;
    }

    /// <summary>The maps of the catalog; the one of the last config is chosen, else the first. Broken map files go to the log.</summary>
    private void LoadMaps()
    {
        var (maps, errors) = MapCatalog.Load(MapPreviews.ReadMapFiles());
        foreach (var error in errors)
        {
            GD.PushWarning($"Map left out: {error}");
        }

        _maps = maps;
        if (_maps.Count > 0)
        {
            Choose(_maps.FirstOrDefault(entry => entry.Map.Id == _config.MapId) ?? _maps[0]);
        }
        else
        {
            BuildRows();
        }
    }

    /// <summary>The list of maps by the sort, the filter and the search; the browsed map stays selected.</summary>
    private void FillMapList()
    {
        var (min, max) = PlayerFilters[Math.Max(0, _filter.Selected)];
        var search = _search.Text.Trim();
        var origin = _originFilter.Selected > 0 ? MapOrigin.Ids[_originFilter.Selected - 1] : null;
        var shown = MapCatalog.OfOrigin(_maps, origin)
            .Where(entry => entry.Map.Players >= min && entry.Map.Players <= max)
            .Where(entry => search.Length == 0 || entry.Map.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
        shown = _byPlayers ? shown.OrderBy(entry => entry.Map.Players).ThenBy(entry => entry.Map.Name, StringComparer.OrdinalIgnoreCase)
            : shown.OrderBy(entry => entry.Map.Name, StringComparer.OrdinalIgnoreCase);
        _listed.Clear();
        _listed.AddRange(shown);
        _mapList.Clear();
        foreach (var entry in _listed)
        {
            var index = _mapList.AddItem(string.Format(CultureInfo.InvariantCulture, Tr("UI_MAP_LIST_ITEM_ORIGIN"), entry.Map.Name, entry.Map.Players,
                Tr(MapCatalog.OriginKey(entry.Map.Origin))));
            if (ReferenceEquals(entry, _browsed))
            {
                _mapList.Select(index);
                _mapList.EnsureCurrentIsVisible();
            }
        }
    }

    private void Browse(MapEntry entry)
    {
        _browsed = entry;
        ShowPreview(entry);
    }

    /// <summary>Use Map: the map browsed becomes the map of the match; back to the Game page.</summary>
    private void UseMap()
    {
        if (_browsed is { } entry)
        {
            Choose(entry);
        }

        ShowGamePage();
    }

    private void Choose(MapEntry entry)
    {
        _chosen = entry;
        SetSlots(entry.Map);
        ShowPreview(entry);
    }

    private void ShowRandomMap()
    {
        _listBox.Visible = false;
        _randomBox.Visible = true;
    }

    /// <summary>A random map from the settings, checked and shown with its preview; Use Map takes it.</summary>
    private void GenerateRandom(Rts.MapGen.MapGenSettings settings)
    {
        try
        {
            var (map, _) = Rts.MapGen.MapGenerator.Generate(settings);
            _generated = new MapEntry("random", map, Rts.Data.Maps.MapHasher.Hash(map));
            Browse(_generated);
        }
        catch (Rts.MapGen.MapGenException error)
        {
            _mapInfo.Text = string.Format(Tr("UI_EDITOR_GENERATE_FAILED"), error.Message);
        }
    }

    /// <summary>
    /// The preview of a map in the right column, from the cache at once or a placeholder until the background drawing is done; under it
    /// the game type and the map, as RA2 writes them.
    /// </summary>
    private async void ShowPreview(MapEntry entry)
    {
        var map = entry.Map;
        _mapInfo.Text = Tr("UI_GAME_TYPE_BATTLE") + "\n\n" + string.Format(CultureInfo.InvariantCulture, Tr("UI_MAP_LIST_ITEM"), map.Name, map.Players);
        var texture = MapPreviews.TryLoad(entry);
        if (texture is null)
        {
            _preview.Show(null, null);
            _previewLabel.Text = Tr("UI_MAP_PREVIEW_RENDERING");
            texture = await MapPreviews.RenderAsync(entry);
            if (!IsInsideTree() || !ReferenceEquals(_customizePage.Visible ? _browsed : _chosen, entry))
            {
                // Another map was picked meanwhile; the picture is in the cache for later.
                return;
            }
        }

        _previewLabel.Text = "";
        _preview.Show(texture, map);
    }

    // ---- Starting.

    /// <summary>
    /// Starts the match of the rows on the map chosen, as Quick Skirmish does: the scene of the match with the map file and the setup. A
    /// random map is written to <see cref="RandomMapPath"/> first — the match reads its map from a file.
    /// </summary>
    private void StartGame()
    {
        if (_chosen is not { } entry || !_config.CanStart)
        {
            return;
        }

        var file = entry.File;
        if (ReferenceEquals(entry, _generated))
        {
            DirAccess.MakeDirRecursiveAbsolute(RandomMapPath[..RandomMapPath.LastIndexOf('/')]);
            using var written = FileAccess.Open(RandomMapPath, FileAccess.ModeFlags.Write);
            if (written is null)
            {
                _mapInfo.Text = string.Format(Tr("UI_EDITOR_GENERATE_FAILED"), FileAccess.GetOpenError().ToString());
                return;
            }

            written.StoreString(Rts.Data.Maps.RtsMapWriter.Write(entry.Map));
            file = RandomMapPath;
        }

        SaveConfig();
        var setup = _config.ToSetup(entry.Map, DataFiles.Current.Rules.DefaultFaction.Id, MatchSetups.NewSeed(), Playable(), DataFiles.Current.Rules.Database.Countries);
        SceneRouter.Instance?.GoTo(MatchScene, new Dictionary<string, object>
        {
            [SimulationHost.MapArgument] = file,
            [SimulationHost.MatchArgument] = setup,
        });
    }

    private void SaveConfig()
    {
        using var file = FileAccess.Open(ConfigPath, FileAccess.ModeFlags.Write);
        file?.StoreString(_config.Save());
    }

    private static SkirmishConfig? LoadConfig() =>
        FileAccess.FileExists(ConfigPath) ? SkirmishConfig.Load(FileAccess.GetFileAsString(ConfigPath)) : null;

    /// <summary>The factions the screen offers that the data has: Random draws among them.</summary>
    private static List<string> Playable()
    {
        var database = DataFiles.Current.Rules.Database;
        return [.. FactionIds.Where(id => database.TryGetFaction(id, out _))];
    }

    /// <summary>A dropdown of <paramref name="items"/>, named for the input scripts.</summary>
    private static OptionButton Option(string name, float width, params string[] items)
    {
        var option = new OptionButton { Name = name, CustomMinimumSize = new Vector2(width, FieldHeight), ClipText = true };
        foreach (var item in items)
        {
            option.AddItem(item);
        }

        return option;
    }

    /// <summary>A square of colour <paramref name="index"/> of <see cref="PlayerPalette"/>, for the colour dropdown.</summary>
    private static ImageTexture Swatch(int index)
    {
        var (r, g, b) = PlayerPalette.Colors[index];
        var image = Image.CreateEmpty(SwatchSize, SwatchSize, false, Image.Format.Rgb8);
        image.Fill(new Color(r, g, b));
        return ImageTexture.CreateFromImage(image);
    }

    /// <summary>A label that sits in the middle of its row, next to a control; a header in the gold of RA2.</summary>
    private static Label Caption(string text, bool header = false)
    {
        var label = new Label
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        if (header)
        {
            label.AddThemeColorOverride("font_color", new Color(0.957f, 0.784f, 0.263f));
            label.AddThemeFontSizeOverride("font_size", 24);
        }

        return label;
    }

    private void OnBackPressed()
    {
        SceneRouter.Instance?.Back();
    }

    /// <summary>The controls of a row of the players table, in the order of the columns, and those that show its slot.</summary>
    private sealed record PlayerRow(
        Control[] Controls, OptionButton? Who, OptionButton Personality, TextureRect Flag, Label Unknown, OptionButton Side, OptionButton Color,
        OptionButton Start, OptionButton Team);
}
