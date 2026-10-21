using System;
using System.Linq;
using Godot;
using Rts.Client.App;

namespace Rts.Client.UI;

/// <summary>
/// Main menu (docs/menu/README.md, sections 2.3 and 3.2). All entries are shown in the order of section 2.3.
/// An entry whose screen does not exist yet is greyed out and marked "coming soon": Settings comes with step 5,
/// Skirmish with step 7, Replays with step 15, Map Editor with M7, Multiplayer with M9, Online with M10.
/// Credits has no step yet.
/// </summary>
public partial class MainMenu : Control
{
    private const float EntryWidth = 420f;
    private const float EntryHeight = 56f;
    private const int EntrySeparation = 12;

    private VBoxContainer _entries = null!;
    private ConfirmationDialog _exitDialog = null!;

    public override void _Ready()
    {
        ScreenshotCapture.AttachIfRequested(this);

        GetNode<Label>("Title").Text = Tr("UI_GAME_TITLE");
        GetNode<Label>("Version").Text = string.Format(Tr("UI_VERSION"), GameInfo.Version);

        _entries = GetNode<VBoxContainer>("Entries");
        _entries.AddThemeConstantOverride("separation", EntrySeparation);

        // Entries without an action are not ready yet. Each step that adds a screen passes its action here.
        AddEntry("UI_MAIN_SKIRMISH", null);
        AddEntry("UI_MAIN_MULTIPLAYER", null);
        AddEntry("UI_MAIN_ONLINE", null);
        AddEntry("UI_MAIN_MAP_EDITOR", null);
        AddEntry("UI_MAIN_REPLAYS", null);
        AddEntry("UI_MAIN_SETTINGS", null);
        AddEntry("UI_MAIN_CREDITS", null);
        AddEntry("UI_MAIN_EXIT", ShowExitDialog);

        _exitDialog = new ConfirmationDialog
        {
            Title = Tr("UI_EXIT_TITLE"),
            DialogText = Tr("UI_EXIT_CONFIRM_TEXT"),
        };
        _exitDialog.Confirmed += OnExitConfirmed;
        AddChild(_exitDialog);

        FocusFirstReadyEntry();
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

    private void ShowExitDialog()
    {
        _exitDialog.PopupCentered();
    }

    private void OnExitConfirmed()
    {
        GetTree().Quit();
    }
}
