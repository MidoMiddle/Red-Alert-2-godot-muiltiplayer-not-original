using System;
using Godot;
using Rts.Client.App;

namespace Rts.Client.UI;

/// <summary>
/// Main menu (docs/menu/README.md, sections 2.3 and 3.2). An entry is shown only when its screen exists:
/// at this step only Exit is ready. Settings comes with step 5, Skirmish with step 7, Replays with step 15.
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
        AddEntry("Exit", "UI_MAIN_EXIT", ShowExitDialog);

        _exitDialog = new ConfirmationDialog
        {
            Title = Tr("UI_EXIT_TITLE"),
            DialogText = Tr("UI_EXIT_CONFIRM_TEXT"),
        };
        _exitDialog.Confirmed += OnExitConfirmed;
        AddChild(_exitDialog);

        _entries.GetChild<Button>(0).GrabFocus();
    }

    private void AddEntry(string name, string key, Action onPressed)
    {
        var button = new Button
        {
            Name = name,
            Text = Tr(key),
            CustomMinimumSize = new Vector2(EntryWidth, EntryHeight),
        };
        button.Pressed += onPressed;
        _entries.AddChild(button);
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
