using System.Collections.Generic;
using Godot;
using Rts.Client.App;
using Rts.Client.Hud;
using Rts.Simulation.Matches;
using Rts.Simulation.World;

namespace Rts.Client.UI;

/// <summary>
/// The match screen of RA2 (docs/menu/README.md, 3.4; docs/implementation/M6_menus_skirmish.md, 4.15; the owner's screenshots,
/// 2026-10-25): the sidebar stays where it is with other contents — the emblem of the player's faction at its top, the buttons of
/// the screen in its middle, Resume Mission at its bottom — and the field turns into the emblem of the faction. Esc or the Menu button
/// opens the menu: Settings (the screen of settings over the match, M6 step 15), Fullscreen, Restart (asked first: the same match with
/// a new seed — a match on one computer only), Surrender (asked first, for a player still in the match: <c>SurrenderCommand</c>, M5
/// step 10), Abort Mission (asked first: the match is left unsaved). Tab or the Stats button opens the statistics over the emblem: every player with
/// the flag of his country, whether he is your ally, his kills; the map and the options of the match. Either stops the game tree —
/// the simulation, the camera and the player input stop with it; Resume Mission, Esc, or Tab on the statistics close it.
/// </summary>
public partial class PauseMenu : CanvasLayer
{
    private const string MainMenuScene = "res://scenes/ui/MainMenu.tscn";

    private enum Screen
    {
        Menu,
        Stats,
    }

    private Control? _overlay;
    private FactionEmblem _field = null!;
    private FactionEmblem _logo = null!;
    private Control _menuButtons = null!;
    private Button _surrender = null!;
    private Button _fullscreen = null!;
    private Button _resume = null!;
    private VBoxContainer _stats = null!;
    private GridContainer _table = null!;
    private Label _info = null!;
    private ConfirmationDialog _leaveDialog = null!;
    private ConfirmationDialog _surrenderDialog = null!;
    private ConfirmationDialog _restartDialog = null!;
    private Button _restart = null!;
    private Settings? _settings;
    private SimulationHost? _host;
    private bool _open;
    private Screen _screen;

    /// <summary>The theme of the match (the side's colours): the screen and its dialogs take it.</summary>
    public Theme? Theme
    {
        get => _theme;
        set
        {
            _theme = value;
            ApplyTheme();
        }
    }

    private Theme? _theme;

    // Given before or after the screen is built: whichever comes second applies it.
    private void ApplyTheme()
    {
        if (_overlay is null)
        {
            return;
        }

        _overlay.Theme = _theme;
        _leaveDialog.Theme = _theme;
        _surrenderDialog.Theme = _theme;
        _restartDialog.Theme = _theme;
    }

    /// <summary>Whether the screen is up, the menu or the statistics.</summary>
    public bool IsOpen => _open;

    public override void _Ready()
    {
        // The screen has to keep working while the game tree is paused.
        ProcessMode = ProcessModeEnum.Always;
        foreach (var child in GetChildren())
        {
            // The scene's old panel: the screen is built here.
            child.QueueFree();
        }

        _overlay = new Control { Name = "Overlay", Visible = false, MouseFilter = Control.MouseFilterEnum.Stop };
        _overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_overlay);

        // The field: the emblem of the faction, the statistics over it.
        _field = new FactionEmblem { Name = "Field" };
        _field.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _field.OffsetRight = -SidebarLayout.Width;
        _field.OffsetBottom = -CommandBarLayout.Height;
        _overlay!.AddChild(_field);
        _stats = new VBoxContainer { Name = "Stats", Alignment = BoxContainer.AlignmentMode.Center };
        _stats.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _stats.AddThemeConstantOverride("separation", 48);
        _field.AddChild(_stats);
        var centre = new CenterContainer();
        _stats.AddChild(centre);
        _table = new GridContainer { Columns = 4 };
        // The text of the table and of the line under it stands out over the emblem: larger, with a dark edge.
        var type = new Theme();
        type.SetFontSize("font_size", "Label", 30);
        type.SetConstant("outline_size", "Label", 8);
        type.SetColor("font_outline_color", "Label", Colors.Black);
        _stats.Theme = type;
        _table.AddThemeConstantOverride("h_separation", 56);
        _table.AddThemeConstantOverride("v_separation", 18);
        centre.AddChild(_table);
        var infoBox = new CenterContainer();
        _stats.AddChild(infoBox);
        _info = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(900, 0) };
        _info.AddThemeColorOverride("font_color", new Color(1f, 0.9f, 0.2f));
        infoBox.AddChild(_info);

        // The sidebar: its emblem, the buttons of the menu, Resume Mission.
        var side = new PanelContainer { Name = "Side", ClipContents = true };
        side.SetAnchorsPreset(Control.LayoutPreset.RightWide);
        side.OffsetLeft = -SidebarLayout.Width;
        side.OffsetRight = 0;
        _overlay!.AddChild(side);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 10);
        side.AddChild(column);
        _logo = new FactionEmblem { Name = "Logo", Small = true, CustomMinimumSize = new Vector2(0, SidebarLayout.MinimapSize + 40) };
        column.AddChild(_logo);
        var buttons = new VBoxContainer();
        buttons.AddThemeConstantOverride("separation", 8);
        _menuButtons = buttons;
        column.AddChild(buttons);
        var settings = new Button { Name = "SettingsButton", Text = Tr("UI_MAIN_SETTINGS"), ClipText = true };
        settings.Pressed += OpenSettings;
        buttons.AddChild(settings);
        _fullscreen = new Button { ClipText = true };
        _fullscreen.Pressed += ToggleFullscreen;
        buttons.AddChild(_fullscreen);
        _restart = new Button { Name = "RestartButton", Text = Tr("UI_PAUSE_RESTART"), ClipText = true };
        buttons.AddChild(_restart);
        _surrender = new Button { Name = "SurrenderButton", Text = Tr("UI_PAUSE_SURRENDER"), ClipText = true };
        buttons.AddChild(_surrender);
        var abort = new Button { Text = Tr("UI_MATCH_ABORT"), ClipText = true };
        buttons.AddChild(abort);
        column.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill });
        _resume = new Button { Text = Tr("UI_MATCH_RESUME"), CustomMinimumSize = new Vector2(0, 44), ClipText = true };
        _resume.Pressed += Close;
        column.AddChild(_resume);
        foreach (var button in new[] { settings, _fullscreen, _restart, _surrender, abort, _resume })
        {
            // The sidebar is narrow: the labels of RA2 in a smaller type.
            button.AddThemeFontSizeOverride("font_size", 17);
        }

        _leaveDialog = new ConfirmationDialog
        {
            Title = Tr("UI_MATCH_ABORT"),
            DialogText = Tr("UI_PAUSE_LEAVE_TEXT"),
        };
        _leaveDialog.Confirmed += LeaveToMainMenu;
        AddChild(_leaveDialog);
        abort.Pressed += () => _leaveDialog.PopupCentered();

        _surrenderDialog = new ConfirmationDialog
        {
            Title = Tr("UI_PAUSE_SURRENDER"),
            DialogText = Tr("UI_PAUSE_SURRENDER_TEXT"),
        };
        _surrenderDialog.Confirmed += Surrender;
        AddChild(_surrenderDialog);
        _surrender.Pressed += () => _surrenderDialog.PopupCentered();

        _restartDialog = new ConfirmationDialog
        {
            Title = Tr("UI_PAUSE_RESTART"),
            DialogText = Tr("UI_PAUSE_RESTART_TEXT"),
        };
        _restartDialog.Confirmed += Restart;
        AddChild(_restartDialog);
        _restart.Pressed += () => _restartDialog.PopupCentered();
        ApplyTheme();
    }

    /// <summary>The match the screen shows and gives up: without one there is nothing to surrender.</summary>
    public void Bind(SimulationHost host) => _host = host;

    // Tab before the interface: a focused button would take it to move the focus.
    public override void _Input(InputEvent inputEvent)
    {
        if (!_leaveDialog.Visible && !_surrenderDialog.Visible && !_restartDialog.Visible && _settings is null && inputEvent is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Tab })
        {
            ToggleStats();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        // While a dialog or the settings are up, their keys belong to them.
        if (_leaveDialog.Visible || _surrenderDialog.Visible || _restartDialog.Visible || _settings is not null)
        {
            return;
        }

        if (_open && inputEvent.IsActionPressed("ui_cancel"))
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>Opens the menu (Esc, the Menu button) and stops the game.</summary>
    public void Open() => Present(Screen.Menu);

    /// <summary>The Stats button: the statistics, open or shut.</summary>
    public void ToggleStats()
    {
        if (_open && _screen == Screen.Stats)
        {
            Close();
        }
        else
        {
            Present(Screen.Stats);
        }
    }

    private void Present(Screen screen)
    {
        _screen = screen;
        var faction = MatchFaction();
        _field.Faction = _logo.Faction = faction;
        _field.QueueRedraw();
        _logo.QueueRedraw();
        _menuButtons.Visible = screen == Screen.Menu;
        _stats.Visible = screen == Screen.Stats;
        _field.Dim = screen == Screen.Stats;
        _surrender.Visible = _host?.CanSurrender == true;
        _restart.Visible = _host?.CanRestart == true;
        _fullscreen.Text = Tr(DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Fullscreen ? "UI_MATCH_WINDOWED" : "UI_MATCH_FULLSCREEN");
        if (screen == Screen.Stats)
        {
            FillStats();
        }

        if (!_open)
        {
            _open = true;
            _overlay!.Show();
            GetTree().Paused = true;
        }

        _resume.GrabFocus();
    }

    // The settings over the match, the menu under them; Back or Esc there brings the menu back.
    private void OpenSettings()
    {
        if (_settings is not null)
        {
            return;
        }

        _settings = GD.Load<PackedScene>(Settings.Scene).Instantiate<Settings>();
        _settings.OverMatch = true;
        _settings.ProcessMode = Node.ProcessModeEnum.Always;
        _settings.Closed += CloseSettings;
        _overlay!.AddChild(_settings);
        _settings.SetAnchorsPreset(Control.LayoutPreset.FullRect);
    }

    private void CloseSettings()
    {
        _settings?.QueueFree();
        _settings = null;
        _resume.GrabFocus();
    }

    // The same match with a new seed, on the spot (as Play Again of the results).
    private void Restart()
    {
        Close();
        _host?.PlayAgain();
    }

    private void Close()
    {
        CloseSettings();
        _open = false;
        _overlay!.Hide();
        GetTree().Paused = false;
    }

    private Faction MatchFaction() =>
        _host?.Simulation.World.Match?.PlayerOf(SimulationHost.LocalPlayer) is { } local ? FactionPalette.Of(local.Faction)
        : _host is not null ? FactionPalette.Of(_host.Simulation.World.Player(SimulationHost.LocalPlayer).Faction.Id)
        : Faction.Union;

    /// <summary>The players of the match — flag, name, ally of yours, kills — and the map and the options under them.</summary>
    private void FillStats()
    {
        foreach (var child in _table.GetChildren())
        {
            child.QueueFree();
        }

        if (_host?.Simulation.World is not { } world)
        {
            return;
        }

        var header = new Color(1f, 0.9f, 0.2f);
        foreach (var key in new[] { "", "UI_MATCH_PLAYER", "UI_MATCH_ALLIES", "UI_MATCH_KILLS" })
        {
            var label = new Label { Text = key.Length == 0 ? "" : Tr(key) };
            label.AddThemeColorOverride("font_color", header);
            _table.AddChild(label);
        }

        var match = world.Match;
        var rows = new List<(PlayerId Id, string Country, string Name)>();
        if (match is not null)
        {
            foreach (var player in match.Players)
            {
                rows.Add((player.Id, player.Country, NameOf(match, player)));
            }
        }
        else
        {
            // A session without a match (the sandbox): the players of the world.
            foreach (var state in world.Players)
            {
                rows.Add((state.Id, "", state.Id == SimulationHost.LocalPlayer ? Tr("UI_RESULTS_YOU") : string.Format(Tr("UI_MATCH_PLAYER_N"), state.Id.Value)));
            }
        }

        foreach (var (id, country, playerName) in rows)
        {
            var player = (Id: id, Country: country);
            var colour = Matches.PlayerColors.Of(player.Id);
            _table.AddChild(new TextureRect { Texture = Flags.Of(player.Country), StretchMode = TextureRect.StretchModeEnum.KeepCentered, CustomMinimumSize = new Vector2(Flags.Width, Flags.Height) });
            var name = new Label { Text = playerName };
            name.AddThemeColorOverride("font_color", colour);
            _table.AddChild(name);
            // Allies: green for yours, red for the others (RA2's square); none for you.
            var you = player.Id == SimulationHost.LocalPlayer;
            var square = new ColorRect
            {
                CustomMinimumSize = new Vector2(24, 24),
                SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
                Color = you ? new Color(0, 0, 0, 0) : world.AreAllies(player.Id, SimulationHost.LocalPlayer) ? new Color(0.2f, 0.7f, 0.25f) : new Color(0.45f, 0.08f, 0.08f),
            };
            _table.AddChild(square);
            var stats = world.Stats.Of(player.Id);
            var kills = new Label { Text = (stats.UnitsDestroyed + stats.BuildingsDestroyed).ToString(System.Globalization.CultureInfo.InvariantCulture) };
            kills.AddThemeColorOverride("font_color", colour);
            _table.AddChild(kills);
        }

        if (match is null)
        {
            _info.Text = "";
            return;
        }

        var options = match.Options;
        string Option(string key, bool on) => string.Format(Tr("UI_MATCH_OPTION"), Tr(key), Tr(on ? "UI_MATCH_ON" : "UI_MATCH_OFF"));
        var parts = new List<string>
        {
            string.Format(Tr("UI_MATCH_OPTION"), Tr("UI_GAME_TYPE"), Tr("UI_GAME_TYPE_BATTLE")),
            Option("UI_OPTION_SHORT_GAME", options.ShortGame),
            Option("UI_OPTION_SUPERWEAPONS", options.Superweapons),
            Option("UI_OPTION_DESTROYABLE_BRIDGES", options.DestroyableBridges),
            Option("UI_OPTION_MCV_REPACKS", options.McvRepacks),
            Option("UI_OPTION_FOG_OF_WAR", options.FogOfWar),
            string.Format(Tr("UI_MATCH_OPTION"), Tr("UI_OPTION_CREDITS"), options.StartingCredits.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            string.Format(Tr("UI_MATCH_OPTION"), Tr("UI_OPTION_UNIT_COUNT"), options.StartingUnits.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            string.Format(Tr("UI_MATCH_OPTION"), Tr("UI_OPTION_GAME_SPEED"), options.GameSpeed.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        };
        _info.Text = string.Format(Tr("UI_MATCH_MAP"), MapName(match.MapId)) + "\n" + string.Join(Tr("UI_MATCH_SEPARATOR"), parts);
    }

    private string NameOf(MatchSetup match, MatchPlayer player)
    {
        if (player.Id == SimulationHost.LocalPlayer)
        {
            return Tr("UI_RESULTS_YOU");
        }

        if (player.Kind == PlayerKind.Human)
        {
            return string.Format(Tr("UI_MATCH_PLAYER_N"), player.Slot);
        }

        var personality = Rts.AI.MatchBots.PersonalityOf(match, player, DataFiles.Current.Rules.Database);
        return string.Format(Tr("UI_RESULTS_BOT"), SkirmishNames.Level(this, player.Difficulty), SkirmishNames.Personality(this, personality));
    }

    // The name of the map in its file, or its id when it cannot be read.
    private static string MapName(string id)
    {
        foreach (var folder in new[] { Maps.MapCatalog.Directory, Maps.MapCatalog.UserDirectory })
        {
            var path = $"{folder}/{id}.rtsmap";
            if (Godot.FileAccess.FileExists(path))
            {
                try
                {
                    return Rts.Data.Maps.RtsMapReader.Read(Godot.FileAccess.GetFileAsString(path)).Name;
                }
                catch (Rts.Data.Maps.RtsMapFormatException)
                {
                    break;
                }
            }
        }

        return id;
    }

    private void ToggleFullscreen()
    {
        var fullscreen = DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Fullscreen;
        AppSettings.Instance?.SetWindowMode(fullscreen ? WindowMode.Windowed : WindowMode.Fullscreen);
        _fullscreen.Text = Tr(fullscreen ? "UI_MATCH_FULLSCREEN" : "UI_MATCH_WINDOWED");
    }

    private void Surrender()
    {
        _host?.Surrender();
        Close();
    }

    private void LeaveToMainMenu()
    {
        _open = false;
        LeaveMatch(GetTree());
    }

    /// <summary>Leaves the match unsaved: to the main menu, or back to the map editor that started it. The results screen leaves so too.</summary>
    public static void LeaveMatch(SceneTree tree)
    {
        // Unpause first: the router's fade runs on a tween, and a paused tree would hold it.
        tree.Paused = false;

        // A match started by «Play» in the map editor goes back to the editor (M7 step 12), a replay to the list of replays (M6 step 15).
        if (SceneRouter.Instance?.Args.ContainsKey(Rts.Client.Editor.MapEditorScene.FromEditorArgument) == true
            || SceneRouter.Instance?.Args.ContainsKey(SimulationHost.ReplayArgument) == true)
        {
            SceneRouter.Instance.Back();
            return;
        }

        SceneRouter.Instance?.Replace(MainMenuScene);
    }
}
