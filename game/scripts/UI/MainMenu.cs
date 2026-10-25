using System;
using System.Linq;
using Godot;
using Rts.Client.App;

namespace Rts.Client.UI;

/// <summary>
/// Main menu, laid out after the classic RA2 menu (docs/menu/README.md, section 3.2): a large screen with the
/// title on the left, a column with the header and the entries on the right. All entries are shown in the order of
/// section 2.3. An entry whose screen does not exist yet is greyed out and marked "coming soon": Settings comes with
/// Replays with step 15, Online with M10; Multiplayer opens the network menu of M9 (host or join by address). Credits has no step yet.
/// Skirmish opens its setup screen (step 7, no match start yet), Settings the settings screen (step 5), and Map Editor the
/// map editor (M7 step 11).
/// Test Sandbox is a temporary entry: it opens the debug scene <c>Main</c> with everything that is ready (M0 simulation,
/// M1 camera and lighting) until a real match can be started from the skirmish screen. In it, Esc or the Menu button
/// opens the pause menu, which returns to this menu.
/// </summary>
public partial class MainMenu : Control
{
    private const float EntryWidth = 360f;
    private const float EntryHeight = 56f;
    private const int EntrySeparation = 12;
    private const float ExitSpacerHeight = 32f;
    private const string SkirmishSetupScene = "res://scenes/ui/SkirmishSetup.tscn";
    private const string SettingsScene = "res://scenes/ui/Settings.tscn";
    private const string SandboxScene = "res://scenes/Main.tscn";
    private const string QuickSkirmishScene = "res://scenes/debug/QuickSkirmish.tscn";
    private const string MapEditorScene = "res://scenes/editor/MapEditor.tscn";

    private VBoxContainer _entries = null!;
    private ConfirmationDialog _exitDialog = null!;

    public override void _Ready()
    {
        ScreenshotCapture.AttachIfRequested(this);

        GetNode<Label>("Screen/Title").Text = Tr("UI_GAME_TITLE");
        GetNode<Label>("Column/Header/HeaderLabel").Text = Tr("UI_MAIN_MENU");
        GetNode<Label>("Column/Version").Text = string.Format(Tr("UI_VERSION"), GameInfo.Version);

        _entries = GetNode<VBoxContainer>("Column/Scroll/Entries");
        _entries.AddThemeConstantOverride("separation", EntrySeparation);

        // Entries without an action are not ready yet. Each step that adds a screen passes its action here.
        AddEntry("UI_MAIN_SANDBOX", OpenSandbox);
        AddEntry("UI_MAIN_QUICK_SKIRMISH", OpenQuickSkirmish);
        AddEntry("UI_MAIN_SKIRMISH", OpenSkirmishSetup);
        AddEntry("UI_MAIN_MULTIPLAYER", OpenNetwork);
        AddEntry("UI_MAIN_ONLINE", null);
        AddEntry("UI_MAIN_MAP_EDITOR", OpenMapEditor);
        AddEntry("UI_MAIN_REPLAYS", null);
        AddEntry("UI_MAIN_SETTINGS", OpenSettings);
        AddEntry("UI_MAIN_CREDITS", null);
        _entries.AddChild(new Control { Name = "ExitSpacer", CustomMinimumSize = new Vector2(0f, ExitSpacerHeight) });
        AddEntry("UI_MAIN_EXIT", ShowExitDialog);

        _exitDialog = new ConfirmationDialog
        {
            Title = Tr("UI_EXIT_TITLE"),
            DialogText = Tr("UI_EXIT_CONFIRM_TEXT"),
        };
        _exitDialog.Confirmed += OnExitConfirmed;
        AddChild(_exitDialog);

        FocusFirstReadyEntry();

        // The launch options of a network game go straight to its menu (M9 step 9).
        var args = OS.GetCmdlineUserArgs();
        if (args.Contains("--net-host") || args.Contains("--net-join"))
        {
            // After the router's fade into this screen: it opens nothing while it switches.
            GetTree().CreateTimer(0.5).Timeout += OpenNetwork;
        }
    }

    /// <summary>Adds an entry. Without an action it is disabled and says "coming soon".</summary>
    private void AddEntry(string key, Action? onPressed)
    {
        var ready = onPressed is not null;
        var button = new Button
        {
            Name = key,
            Text = ready ? Tr(key) : string.Format(Tr("UI_MENU_ITEM_SOON"), Tr(key)),
            Disabled = !ready,
            CustomMinimumSize = new Vector2(EntryWidth, EntryHeight),
        };
        if (onPressed is not null)
        {
            button.Pressed += onPressed;
        }

        _entries.AddChild(button);
    }

    private void FocusFirstReadyEntry()
    {
        var first = _entries.GetChildren().OfType<Button>().FirstOrDefault(button => !button.Disabled);
        first?.GrabFocus();
    }

    private void OpenSkirmishSetup()
    {
        SceneRouter.Instance?.GoTo(SkirmishSetupScene);
    }

    private void OpenMapEditor()
    {
        SceneRouter.Instance?.GoTo(MapEditorScene);
    }

    private void OpenSandbox()
    {
        SceneRouter.Instance?.GoTo(SandboxScene);
    }

    // The debug quick skirmish of M5 step 2: one against a «Medium» bot until the skirmish screen starts matches.
    private void OpenQuickSkirmish()
    {
        SceneRouter.Instance?.GoTo(QuickSkirmishScene);
    }

    // The network game of M9: host or join by address.
    private void OpenNetwork()
    {
        SceneRouter.Instance?.GoTo(Net.NetMenu.Scene);
    }

    private void OpenSettings()
    {
        SceneRouter.Instance?.GoTo(SettingsScene);
    }

    private void ShowExitDialog()
    {
        _exitDialog.PopupCentered();
    }

    private void OnExitConfirmed()
    {
        GetTree().Quit();
    }
}
