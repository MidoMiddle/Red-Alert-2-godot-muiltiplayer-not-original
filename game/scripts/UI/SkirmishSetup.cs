using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;
using Rts.Client.App;
using Rts.Client.Maps;
using Rts.Client.Matches;
using Rts.Simulation.Matches;

namespace Rts.Client.UI;

/// <summary>
/// Skirmish setup, layout only (docs/menu/README.md, section 3.3; docs/implementation/M6_menus_skirmish.md, 4.3).
/// Players and options are on the left, the map and the buttons on the right, as in the classic RA2 setup.
/// The map list works since M7 step 10: every map of <see cref="MapCatalog"/>, with its preview (cached, or drawn in the
/// background) and a line of information; its last item, «Random map…» (step 15), shows the generator's settings and a
/// preview of the map they make. The players and Start work since M8 step 13 (with M6 step 7): a row for every start of the map — the
/// person, then bots or closed seats with their level, personality, colour, team and start, a hint on hover — the starting credits and
/// the short game; <see cref="SkirmishConfig"/> keeps the logic and the last config, in <see cref="ConfigPath"/>. Start opens the match
/// as Quick Skirmish does. The speed, the starting units, superweapons and crates stay locked with "Coming soon" on hover.
/// </summary>
public partial class SkirmishSetup : Control
{
    private const float ActionWidth = 360f;
    private const float ActionHeight = 56f;
    private const float FieldWidth = 128f;
    private const float OptionWidth = 240f;
    private const float FieldHeight = 48f;
    private const int PlayerColumns = 8;
    private const int OptionColumns = 4;
    private const int PlayerSeparation = 10;
    private const int OptionSeparation = 24;
    private const int RowSeparation = 12;
    private const int SwatchSize = 16;
    private const string MatchScene = "res://scenes/Main.tscn";

    /// <summary>The last config of the screen, for the next time it opens.</summary>
    public const string ConfigPath = "user://skirmish.cfg";

    /// <summary>
    /// The random map a match is started on: out of <see cref="MapCatalog.UserDirectory"/>, so it does not join the list of maps; the
    /// replay keeps the map itself.
    /// </summary>
    public const string RandomMapPath = "user://skirmish/random.rtsmap";

    // The factions of the faction column, in its order: the ids of the data and «random».
    private static readonly string[] FactionIds = ["union", "alliance", "cult", SkirmishConfig.RandomFaction];

    // The names of the colours of PlayerPalette, in its order.
    private static readonly string[] ColorKeys =
    [
        "UI_COLOR_BLUE", "UI_COLOR_RED", "UI_COLOR_GREEN", "UI_COLOR_YELLOW",
        "UI_COLOR_ORANGE", "UI_COLOR_PURPLE", "UI_COLOR_TEAL", "UI_COLOR_PINK",
    ];

    private List<MapEntry> _maps = [];
    private MapPreviewView _preview = null!;
    private Label _previewLabel = null!;
    private Label _mapInfo = null!;
    private MapEntry? _shown;
    private VBoxContainer _random = null!;
    private MapEntry? _generated;
    private GridContainer _players = null!;
    private readonly List<PlayerRow> _rows = [];
    private OptionButton _credits = null!;
    private CheckBox _shortGame = null!;
    private CheckBox _fog = null!;
    private Button _start = null!;
    private SkirmishConfig _config = SkirmishConfig.For(0);

    // «random», then the personalities of the data, «Balanced» first.
    private List<string> _personalities = [];

    public override void _Ready()
    {
        ScreenshotCapture.AttachIfRequested(this);

        GetNode<Label>("Column/Scroll/Stack/Header/HeaderLabel").Text = Tr("UI_SKIRMISH_HEADER");
        GetNode<Label>("Column/Scroll/Stack/MapLabel").Text = Tr("UI_SKIRMISH_MAP");
        _previewLabel = GetNode<Label>("Column/Scroll/Stack/Preview/PreviewLabel");
        _mapInfo = GetNode<Label>("Column/Scroll/Stack/MapInfo");
        // A long description wraps instead of widening the column.
        _mapInfo.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _mapInfo.CustomMinimumSize = new Vector2(ActionWidth, 0);
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
        _config = LoadConfig() ?? _config;

        var content = GetNode<VBoxContainer>("Screen/Scroll/Content");
        content.AddChild(Caption(Tr("UI_SKIRMISH_PLAYERS")));
        content.AddChild(BuildPlayers());
        content.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
        content.AddChild(Caption(Tr("UI_SKIRMISH_OPTIONS")));
        content.AddChild(BuildOptions());

        var actions = GetNode<VBoxContainer>("Column/Scroll/Stack/Actions");
        _start = new Button
        {
            Name = "StartGame",
            Text = Tr("UI_SKIRMISH_START_GAME"),
            CustomMinimumSize = new Vector2(ActionWidth, ActionHeight),
        };
        _start.Pressed += StartGame;
        actions.AddChild(_start);
        BuildMapList();

        var back = new Button
        {
            Name = "Back",
            Text = Tr("UI_BUTTON_BACK"),
            CustomMinimumSize = new Vector2(ActionWidth, ActionHeight),
        };
        back.Pressed += OnBackPressed;
        actions.AddChild(back);
        if (_start.Disabled)
        {
            back.GrabFocus();
        }
        else
        {
            _start.GrabFocus();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel"))
        {
            OnBackPressed();
        }
    }

    /// <summary>The maps of the catalog in a dropdown; the first one is shown. Broken map files go to the log.</summary>
    private void BuildMapList()
    {
        var box = GetNode<VBoxContainer>("Column/Scroll/Stack/MapBox");
        var (maps, errors) = MapCatalog.Load(MapPreviews.ReadMapFiles());
        foreach (var error in errors)
        {
            GD.PushWarning($"Map left out: {error}");
        }

        _maps = maps;
        if (_maps.Count == 0)
        {
            box.AddChild(DisabledOption(ActionWidth, 0, Tr("UI_MAP_NONE")));
            _previewLabel.Text = Tr("UI_MAP_PREVIEW");
            _mapInfo.Text = Tr("UI_MAP_INFO_NONE");
            Refresh();
            return;
        }

        var list = new OptionButton { Name = "MapList", CustomMinimumSize = new Vector2(ActionWidth, FieldHeight) };
        foreach (var entry in _maps)
        {
            list.AddItem(entry.Map.Name);
        }

        // The last item makes a random map (M7 step 15): its settings appear below the list.
        list.AddItem(Tr("UI_MAP_RANDOM"));
        list.ItemSelected += index => Pick((int)index);
        box.AddChild(list);
        _random = new VBoxContainer { Name = "RandomMap", Visible = false };
        var options = new MapGenOptions { Name = "RandomMapOptions", Columns = 1, CustomMinimumSize = new Vector2(ActionWidth, 0) };
        _random.AddChild(options);
        var generate = new Button { Text = Tr("UI_EDITOR_GENERATE"), CustomMinimumSize = new Vector2(ActionWidth, FieldHeight) };
        generate.Pressed += () => GenerateRandom(options.Settings);
        _random.AddChild(generate);
        box.AddChild(_random);
        Pick(0);
    }

    private void Pick(int index)
    {
        _random.Visible = index == _maps.Count;
        if (index < _maps.Count)
        {
            ShowMap(_maps[index]);
            return;
        }

        _shown = _generated;
        if (_generated is not null)
        {
            ShowMap(_generated);
            return;
        }

        _preview.Show(null, null);
        _previewLabel.Text = Tr("UI_MAP_PREVIEW");
        _mapInfo.Text = Tr("UI_MAP_RANDOM_HINT");
        Refresh();
    }

    /// <summary>A random map from the settings, checked and shown with its preview; Start plays it.</summary>
    private void GenerateRandom(Rts.MapGen.MapGenSettings settings)
    {
        try
        {
            var (map, _) = Rts.MapGen.MapGenerator.Generate(settings);
            _generated = new MapEntry("random", map, Rts.Data.Maps.MapHasher.Hash(map));
            ShowMap(_generated);
        }
        catch (Rts.MapGen.MapGenException error)
        {
            _mapInfo.Text = string.Format(Tr("UI_EDITOR_GENERATE_FAILED"), error.Message);
        }
    }

    /// <summary>Shows the preview of a map: from the cache at once, or a placeholder until the background drawing is done.</summary>
    private async void ShowMap(MapEntry entry)
    {
        _shown = entry;
        var map = entry.Map;
        SetSlots(map);
        _mapInfo.Text = string.Format(Tr("UI_MAP_INFO"), map.Name, map.Width, map.Height, map.Players)
            + (map.Description.Length > 0 ? "\n" + map.Description : "");
        var texture = MapPreviews.TryLoad(entry);
        if (texture is null)
        {
            _preview.Show(null, null);
            _previewLabel.Text = Tr("UI_MAP_PREVIEW_RENDERING");
            texture = await MapPreviews.RenderAsync(entry);
            if (!IsInsideTree() || !ReferenceEquals(_shown, entry))
            {
                // The player picked another map meanwhile; the picture is in the cache for later.
                return;
            }
        }

        _previewLabel.Text = "";
        _preview.Show(texture, map);
    }

    /// <summary>
    /// The players table: the captions, then one row for every start of the map shown (<see cref="SkirmishConfig"/>). The first row is the
    /// person, who has no level or personality; the others are a bot or closed.
    /// </summary>
    private GridContainer BuildPlayers()
    {
        _players = new GridContainer { Name = "Players", Columns = PlayerColumns };
        _players.AddThemeConstantOverride("h_separation", PlayerSeparation);
        _players.AddThemeConstantOverride("v_separation", RowSeparation);

        foreach (var key in new[]
        {
            "UI_SKIRMISH_WHO", "UI_SKIRMISH_DIFFICULTY", "UI_SKIRMISH_PERSONALITY", "UI_SKIRMISH_FACTION", "UI_SKIRMISH_COUNTRY",
            "UI_SKIRMISH_COLOR", "UI_SKIRMISH_TEAM", "UI_SKIRMISH_START_POSITION",
        })
        {
            _players.AddChild(Caption(Tr(key)));
        }

        return _players;
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
                who = Caption(Tr("UI_SKIRMISH_YOU"));
            }
            else
            {
                var kind = Option($"Who{row}", Tr("UI_SLOT_BOT"), Tr("UI_SLOT_CLOSED"));
                kind.ItemSelected += item => Change(row, slot => slot with { Kind = item == 0 ? SlotKind.Bot : SlotKind.Closed });
                who = kind;
            }

            var level = Option($"Level{row}");
            foreach (var difficulty in SkirmishConfig.Levels)
            {
                level.AddItem(SkirmishNames.Level(this, difficulty));
                level.SetItemTooltip(level.ItemCount - 1, SkirmishNames.LevelHint(this, difficulty));
            }

            level.ItemSelected += item => Change(row, slot => slot with { Difficulty = SkirmishConfig.Levels[(int)item] });

            var personality = Option($"Personality{row}");
            foreach (var id in _personalities)
            {
                personality.AddItem(SkirmishNames.Personality(this, id));
                personality.SetItemTooltip(personality.ItemCount - 1, SkirmishNames.PersonalityHint(this, id));
            }

            personality.ItemSelected += item => Change(row, slot => slot with { Personality = _personalities[(int)item] });

            var color = Option($"Color{row}");
            for (var i = 0; i < PlayerPalette.Colors.Length; i++)
            {
                color.AddIconItem(Swatch(i), Tr(ColorKeys[i]));
            }

            color.ItemSelected += item => Apply(_config.WithColor(row, (int)item));

            var team = Option($"Team{row}", Tr("UI_TEAM_NONE"), "1", "2", "3", "4");
            team.ItemSelected += item => Change(row, slot => slot with { Team = (int)item });

            var start = Option($"Start{row}", Tr("UI_RANDOM"));
            for (var i = 1; i <= _config.Slots.Length; i++)
            {
                start.AddItem(i.ToString(CultureInfo.InvariantCulture));
            }

            start.ItemSelected += item => Apply(_config.WithStart(row, item == 0 ? SkirmishConfig.RandomStart : (int)item - 1));

            var faction = FactionOption(row);
            faction.ItemSelected += item => Change(row, slot => slot with { Faction = FactionIds[(int)item], Country = SkirmishConfig.RandomCountry });

            // The country (loose ends, stage 5): its items follow the faction of the row (Refresh).
            var country = Option($"Country{row}");
            var countryRow = row;
            country.ItemSelected += item => Change(countryRow, slot => slot with { Country = CountryChoices(slot)[(int)item] });

            var placeholder = row == 0;
            var controls = new Control[]
            {
                who, placeholder ? new Control() : level, placeholder ? new Control() : personality, faction, country, color, team, start,
            };
            foreach (var control in controls)
            {
                _players.AddChild(control);
            }

            _rows.Add(new PlayerRow(controls, who as OptionButton, level, personality, faction, country, color, team, start));
        }

        Refresh();
    }

    /// <summary>
    /// Game options as in the design mock: the starting credits, the short game (M8 step 13) and the fog of war work; the speed, the starting units,
    /// superweapons and crates are shown and locked until they have rules.
    /// </summary>
    private GridContainer BuildOptions()
    {
        var grid = new GridContainer { Columns = OptionColumns };
        grid.AddThemeConstantOverride("h_separation", OptionSeparation);
        grid.AddThemeConstantOverride("v_separation", RowSeparation);

        grid.AddChild(Caption(Tr("UI_OPTION_CREDITS")));
        _credits = new OptionButton { Name = "Credits", CustomMinimumSize = new Vector2(OptionWidth, FieldHeight) };
        foreach (var key in new[] { "UI_CREDITS_5000", "UI_CREDITS_10000", "UI_CREDITS_20000" })
        {
            _credits.AddItem(Tr(key));
        }

        _credits.ItemSelected += item => Apply(_config with { Credits = SkirmishConfig.CreditChoices[(int)item] });
        grid.AddChild(_credits);
        grid.AddChild(Caption(Tr("UI_OPTION_GAME_SPEED")));
        grid.AddChild(DisabledSlider());

        grid.AddChild(Caption(Tr("UI_OPTION_UNITS")));
        grid.AddChild(DisabledOption(OptionWidth, 0, Tr("UI_UNITS_MCV"), Tr("UI_UNITS_MCV_INFANTRY"), Tr("UI_UNITS_MCV_ARMY")));
        grid.AddChild(Caption(Tr("UI_OPTION_SHORT_GAME")));
        _shortGame = new CheckBox { Name = "ShortGame", SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
        _shortGame.Toggled += on => Apply(_config with { ShortGame = on });
        grid.AddChild(_shortGame);

        grid.AddChild(Caption(Tr("UI_OPTION_SUPERWEAPONS")));
        grid.AddChild(DisabledCheckBox(true));
        grid.AddChild(Caption(Tr("UI_OPTION_CRATES")));
        grid.AddChild(DisabledCheckBox(false));

        // The fog of war, off by default: what the person explored stays in sight.
        grid.AddChild(Caption(Tr("UI_OPTION_FOG_OF_WAR")));
        _fog = new CheckBox { Name = "FogOfWar", SizeFlagsHorizontal = SizeFlags.ShrinkBegin, TooltipText = Tr("UI_OPTION_FOG_OF_WAR_HINT") };
        _fog.Toggled += on => Apply(_config with { FogOfWar = on });
        grid.AddChild(_fog);

        return grid;
    }

    /// <summary>Row <paramref name="row"/> changed by <paramref name="change"/>.</summary>
    private void Change(int row, System.Func<SkirmishSlot, SkirmishSlot> change) => Apply(_config.With(row, change(_config.Slots[row])));

    /// <summary>Takes <paramref name="config"/>, shows it and keeps it for the next time.</summary>
    private void Apply(SkirmishConfig config)
    {
        _config = config;
        Refresh();
        SaveConfig();
    }

    /// <summary>Every control shows <see cref="_config"/>; a closed row has no level or personality; Start waits for an enemy and a map.</summary>
    private void Refresh()
    {
        for (var i = 0; i < _rows.Count; i++)
        {
            var slot = _config.Slots[i];
            var row = _rows[i];
            var bot = slot.Kind == SlotKind.Bot;
            if (row.Who is not null)
            {
                row.Who.Selected = bot ? 0 : 1;
            }

            row.Level.Selected = SkirmishConfig.Levels.IndexOf(slot.Difficulty);
            row.Level.Disabled = !bot;
            row.Level.TooltipText = SkirmishNames.LevelHint(this, slot.Difficulty);
            row.Personality.Selected = _personalities.IndexOf(slot.Personality);
            row.Personality.Disabled = !bot;
            row.Personality.TooltipText = SkirmishNames.PersonalityHint(this, slot.Personality);
            row.Color.Selected = slot.Color;
            row.Team.Selected = slot.Team;
            row.Faction.Selected = Math.Max(0, Array.IndexOf(FactionIds, slot.Faction.Length == 0 ? DataFiles.Current.Rules.DefaultFaction.Id : slot.Faction));
            RefreshCountry(row.Country, slot);
            row.Start.Selected = slot.Start == SkirmishConfig.RandomStart ? 0 : slot.Start + 1;
            foreach (var control in new Control[] { row.Color, row.Team, row.Start })
            {
                control.Modulate = slot.Kind == SlotKind.Closed ? new Color(1f, 1f, 1f, 0.5f) : Colors.White;
            }
        }

        _credits.Selected = Math.Max(0, SkirmishConfig.CreditChoices.IndexOf(_config.Credits));
        _shortGame.SetPressedNoSignal(_config.ShortGame);
        _fog.SetPressedNoSignal(_config.FogOfWar);
        _start.Disabled = _shown is null || !_config.CanStart;
        _start.TooltipText = _shown is null ? Tr("UI_SKIRMISH_START_NEEDS_MAP") : _config.CanStart ? "" : Tr("UI_SKIRMISH_START_NEEDS_ENEMY");
    }

    /// <summary>The rows for the starts of <paramref name="map"/>, keeping what can be kept of the rows before; personalities the data lost are random.</summary>
    private void SetSlots(Rts.Data.Maps.MapDefinition map)
    {
        var config = SkirmishConfig.For(map.Starts.Length, _config);
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
    }

    /// <summary>
    /// Starts the match of the rows on the map shown, as Quick Skirmish does: the scene of the match with the map file and the setup. A
    /// random map is written to <see cref="RandomMapPath"/> first — the match reads its map from a file.
    /// </summary>
    private void StartGame()
    {
        if (_shown is not { } entry || !_config.CanStart)
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

    /// <summary>A dropdown of <paramref name="items"/>, named for the input scripts.</summary>
    private static OptionButton Option(string name, params string[] items)
    {
        var option = new OptionButton { Name = name, CustomMinimumSize = new Vector2(FieldWidth, FieldHeight) };
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

    /// <summary>
    /// The faction of a row: Union, Alliance, Random among the factions of the data; the Cult is listed and locked until M11d, as is any
    /// faction the data does not have.
    /// </summary>
    private OptionButton FactionOption(int row)
    {
        var option = Option($"Faction{row}", Tr("UI_FACTION_UNION"), Tr("UI_FACTION_ALLIANCE"), Tr("UI_FACTION_CULT"), Tr("UI_RANDOM"));
        var database = DataFiles.Current.Rules.Database;
        for (var i = 0; i < FactionIds.Length; i++)
        {
            if (FactionIds[i] != SkirmishConfig.RandomFaction && !database.TryGetFaction(FactionIds[i], out _))
            {
                option.SetItemDisabled(i, true);
                option.SetItemTooltip(i, Tr("UI_COMING_SOON"));
            }
        }

        return option;
    }

    /// <summary>
    /// The countries a row may pick (loose ends, stage 5): Random and those of its faction for the person; none for a bot, which plays
    /// the whole faction, or for a random faction.
    /// </summary>
    private static List<string> CountryChoices(SkirmishSlot slot)
    {
        var faction = slot.Faction.Length == 0 ? DataFiles.Current.Rules.DefaultFaction.Id : slot.Faction;
        var countries = DataFiles.Current.Rules.Database.Countries.Where(country => country.Faction == faction).Select(country => country.Id).ToList();
        return slot.Kind != SlotKind.Human || countries.Count == 0 ? [] : [SkirmishConfig.RandomCountry, .. countries];
    }

    /// <summary>The items of the country of a row and the one it has; a row without a choice shows a dash.</summary>
    private void RefreshCountry(OptionButton option, SkirmishSlot slot)
    {
        var choices = CountryChoices(slot);
        option.Clear();
        if (choices.Count == 0)
        {
            option.AddItem(Tr("UI_SKIRMISH_COUNTRY_DASH"));
            option.Disabled = true;
            option.TooltipText = Tr("UI_SKIRMISH_COUNTRY_NONE");
            return;
        }

        foreach (var id in choices)
        {
            option.AddItem(id == SkirmishConfig.RandomCountry ? Tr("UI_RANDOM")
                : DataFiles.Current.Rules.Database.TryGetCountry(id, out var country) ? country.Name : id);
        }

        option.Disabled = false;
        option.TooltipText = Tr("UI_SKIRMISH_COUNTRY_HINT");
        option.Selected = Math.Max(0, choices.IndexOf(slot.Country));
    }

    /// <summary>The factions the screen offers that the data has: Random draws among them.</summary>
    private static List<string> Playable()
    {
        var database = DataFiles.Current.Rules.Database;
        return [.. FactionIds.Where(id => id != SkirmishConfig.RandomFaction && database.TryGetFaction(id, out _))];
    }

    /// <summary>A dropdown that shows its items and the selected one, but cannot be opened yet.</summary>
    private OptionButton DisabledOption(float width, int selected, params string[] items)
    {
        var option = new OptionButton
        {
            Disabled = true,
            TooltipText = Tr("UI_COMING_SOON"),
            CustomMinimumSize = new Vector2(width, FieldHeight),
        };
        foreach (var item in items)
        {
            option.AddItem(item);
        }

        option.Selected = selected;
        return option;
    }

    /// <summary>Game speed from 1 to 6, starting at 4 (M6, 4.4). Not editable until the match setup exists.</summary>
    private HSlider DisabledSlider()
    {
        return new HSlider
        {
            MinValue = 1,
            MaxValue = 6,
            Step = 1,
            Value = 4,
            Editable = false,
            Modulate = new Color(1f, 1f, 1f, 0.5f),
            CustomMinimumSize = new Vector2(OptionWidth, FieldHeight),
            TooltipText = Tr("UI_COMING_SOON"),
        };
    }

    private CheckBox DisabledCheckBox(bool pressed)
    {
        return new CheckBox
        {
            ButtonPressed = pressed,
            Disabled = true,
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
            TooltipText = Tr("UI_COMING_SOON"),
        };
    }

    /// <summary>A label that sits in the middle of its row, next to a control.</summary>
    private static Label Caption(string text)
    {
        return new Label
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
    }

    private void OnBackPressed()
    {
        SceneRouter.Instance?.Back();
    }

    /// <summary>The controls of a row of the players table, in the order of the columns, and those that show its slot.</summary>
    private sealed record PlayerRow(
        Control[] Controls, OptionButton? Who, OptionButton Level, OptionButton Personality, OptionButton Faction, OptionButton Country,
        OptionButton Color, OptionButton Team, OptionButton Start);
}
