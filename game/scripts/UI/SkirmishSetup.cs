using System.Collections.Generic;
using Godot;
using Rts.Client.App;
using Rts.Client.Maps;

namespace Rts.Client.UI;

/// <summary>
/// Skirmish setup, layout only (docs/menu/README.md, section 3.3; docs/implementation/M6_menus_skirmish.md, 4.3).
/// Players and options are on the left, the map and the buttons on the right, as in the classic RA2 setup.
/// The map list works since M7 step 10: every map of <see cref="MapCatalog"/>, with its preview (cached, or drawn in the
/// background) and a line of information; its last item, «Random map…» (step 15), shows the generator's settings and a
/// preview of the map they make. The rest waits for the bot and the match setup of M5: only Back is active, every
/// other control is disabled and says "Coming soon" on hover.
/// </summary>
public partial class SkirmishSetup : Control
{
    private const float ActionWidth = 360f;
    private const float ActionHeight = 56f;
    private const float FieldWidth = 156f;
    private const float OptionWidth = 240f;
    private const float FieldHeight = 48f;
    private const int PlayerColumns = 7;
    private const int OptionColumns = 4;
    private const int PlayerSeparation = 16;
    private const int OptionSeparation = 24;
    private const int RowSeparation = 12;

    private List<MapEntry> _maps = [];
    private MapPreviewView _preview = null!;
    private Label _previewLabel = null!;
    private Label _mapInfo = null!;
    private MapEntry? _shown;
    private VBoxContainer _random = null!;

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
        BuildMapList();

        var content = GetNode<VBoxContainer>("Screen/Scroll/Content");
        content.AddChild(Caption(Tr("UI_SKIRMISH_PLAYERS")));
        content.AddChild(BuildPlayers());
        content.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
        content.AddChild(Caption(Tr("UI_SKIRMISH_OPTIONS")));
        content.AddChild(BuildOptions());

        var actions = GetNode<VBoxContainer>("Column/Scroll/Stack/Actions");
        actions.AddChild(new Button
        {
            Text = string.Format(Tr("UI_MENU_ITEM_SOON"), Tr("UI_SKIRMISH_START_GAME")),
            Disabled = true,
            CustomMinimumSize = new Vector2(ActionWidth, ActionHeight),
        });

        var back = new Button
        {
            Name = "Back",
            Text = Tr("UI_BUTTON_BACK"),
            CustomMinimumSize = new Vector2(ActionWidth, ActionHeight),
        };
        back.Pressed += OnBackPressed;
        actions.AddChild(back);
        back.GrabFocus();
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

        _shown = null;
        _preview.Show(null, null);
        _previewLabel.Text = Tr("UI_MAP_PREVIEW");
        _mapInfo.Text = Tr("UI_MAP_RANDOM_HINT");
    }

    /// <summary>A random map from the settings, checked and shown with its preview; the match itself starts with M5.</summary>
    private void GenerateRandom(Rts.MapGen.MapGenSettings settings)
    {
        try
        {
            var (map, _) = Rts.MapGen.MapGenerator.Generate(settings);
            ShowMap(new MapEntry("random", map, Rts.Data.Maps.MapHasher.Hash(map)));
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
    /// Players table. The first row is the local player, who has no difficulty or personality. The second row is
    /// the opponent slot: a bot at Medium, the only level until M8, or a closed slot. Later there is one row per start.
    /// </summary>
    private GridContainer BuildPlayers()
    {
        var grid = new GridContainer { Columns = PlayerColumns };
        grid.AddThemeConstantOverride("h_separation", PlayerSeparation);
        grid.AddThemeConstantOverride("v_separation", RowSeparation);

        foreach (var key in new[]
        {
            "UI_SKIRMISH_WHO", "UI_SKIRMISH_DIFFICULTY", "UI_SKIRMISH_PERSONALITY", "UI_SKIRMISH_FACTION",
            "UI_SKIRMISH_COLOR", "UI_SKIRMISH_TEAM", "UI_SKIRMISH_START_POSITION",
        })
        {
            grid.AddChild(Caption(Tr(key)));
        }

        grid.AddChild(Caption(Tr("UI_SKIRMISH_YOU")));
        grid.AddChild(new Control());
        grid.AddChild(new Control());
        grid.AddChild(FactionOption());
        grid.AddChild(DisabledOption(FieldWidth, 0, Tr("UI_COLOR_RED")));
        grid.AddChild(TeamOption(0));
        grid.AddChild(DisabledOption(FieldWidth, 0, "1"));

        grid.AddChild(DisabledOption(FieldWidth, 0, Tr("UI_SLOT_BOT"), Tr("UI_SLOT_CLOSED")));
        grid.AddChild(DisabledOption(
            FieldWidth, 1, Tr("UI_DIFFICULTY_EASY"), Tr("UI_DIFFICULTY_MEDIUM"), Tr("UI_DIFFICULTY_HARD"), Tr("UI_DIFFICULTY_BRUTAL")));
        grid.AddChild(DisabledOption(FieldWidth, 0, Tr("UI_RANDOM")));
        grid.AddChild(FactionOption());
        grid.AddChild(DisabledOption(FieldWidth, 0, Tr("UI_COLOR_BLUE")));
        grid.AddChild(TeamOption(2));
        grid.AddChild(DisabledOption(FieldWidth, 0, Tr("UI_RANDOM")));

        return grid;
    }

    /// <summary>
    /// Game options as in the design mock. Superweapons and crates come with M11, so they are shown and locked.
    /// </summary>
    private GridContainer BuildOptions()
    {
        var grid = new GridContainer { Columns = OptionColumns };
        grid.AddThemeConstantOverride("h_separation", OptionSeparation);
        grid.AddThemeConstantOverride("v_separation", RowSeparation);

        grid.AddChild(Caption(Tr("UI_OPTION_CREDITS")));
        grid.AddChild(DisabledOption(OptionWidth, 1, Tr("UI_CREDITS_5000"), Tr("UI_CREDITS_10000"), Tr("UI_CREDITS_20000")));
        grid.AddChild(Caption(Tr("UI_OPTION_GAME_SPEED")));
        grid.AddChild(DisabledSlider());

        grid.AddChild(Caption(Tr("UI_OPTION_UNITS")));
        grid.AddChild(DisabledOption(OptionWidth, 0, Tr("UI_UNITS_MCV"), Tr("UI_UNITS_MCV_INFANTRY"), Tr("UI_UNITS_MCV_ARMY")));
        grid.AddChild(Caption(Tr("UI_OPTION_SHORT_GAME")));
        grid.AddChild(DisabledCheckBox(false));

        grid.AddChild(Caption(Tr("UI_OPTION_SUPERWEAPONS")));
        grid.AddChild(DisabledCheckBox(true));
        grid.AddChild(Caption(Tr("UI_OPTION_CRATES")));
        grid.AddChild(DisabledCheckBox(false));

        return grid;
    }

    /// <summary>Union is the only faction until M11. Alliance and Cult are listed, Random means Union.</summary>
    private OptionButton FactionOption()
    {
        return DisabledOption(
            FieldWidth, 0, Tr("UI_FACTION_UNION"), Tr("UI_FACTION_ALLIANCE"), Tr("UI_FACTION_CULT"), Tr("UI_RANDOM"));
    }

    /// <summary>Team: none, then 1 to 4.</summary>
    private OptionButton TeamOption(int selected)
    {
        return DisabledOption(FieldWidth, selected, Tr("UI_TEAM_NONE"), "1", "2", "3", "4");
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
}
