using System.Collections.Generic;
using Godot;
using Rts.Client.App;

namespace Rts.Client.UI;

/// <summary>
/// Placeholder for a screen that is not built yet. It shows its level, a "next" button that opens
/// the next level, and a "back" button. Esc goes back too. Used to check the screen flow.
/// </summary>
public partial class ScreenStub : Control
{
    private const string ScenePath = "res://scenes/ui/ScreenStub.tscn";
    private const string LevelKey = "level";

    private int _level = 1;

    public override void _Ready()
    {
        ScreenshotCapture.AttachIfRequested(this);

        var args = SceneRouter.Instance?.Args;
        if (args is not null && args.TryGetValue(LevelKey, out var level) && level is int known)
        {
            _level = known;
        }

        GetNode<Label>("Title").Text = $"{Tr("UI_STUB_TITLE")} {_level}";

        var next = GetNode<Button>("Next");
        next.Text = Tr("UI_BUTTON_NEXT");
        next.Pressed += OnNextPressed;
        next.GrabFocus();

        var back = GetNode<Button>("Back");
        back.Text = Tr("UI_BUTTON_BACK");
        back.Pressed += OnBackPressed;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel"))
        {
            OnBackPressed();
        }
    }

    private void OnNextPressed()
    {
        SceneRouter.Instance?.GoTo(ScenePath, new Dictionary<string, object> { [LevelKey] = _level + 1 });
    }

    private void OnBackPressed()
    {
        SceneRouter.Instance?.Back();
    }
}
