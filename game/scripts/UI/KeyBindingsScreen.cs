using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Rts.Client.App;
using Rts.Client.Controls;

namespace Rts.Client.UI;

/// <summary>
/// Key bindings (docs/implementation/M6_menus_skirmish.md, 4.6): one row per game action with its key. Press a key button, then the
/// next key becomes the key of that action. A key that another action has is swapped, and the player is told so. Escape stops the
/// wait for a key; with no wait, Escape goes back.
/// </summary>
public partial class KeyBindingsScreen : Control
{
    private const float LabelWidth = 360f;
    private const float KeyWidth = 200f;
    private const float RowHeight = 48f;
    private const int RowSeparation = 24;

    private readonly Dictionary<string, Button> _buttons = new();
    private VBoxContainer _rows = null!;
    private Label _warning = null!;

    /// <summary>The action whose key is being changed, or null when the screen waits for nothing.</summary>
    private string? _waitingFor;

    private static AppSettings Store =>
        AppSettings.Instance ?? throw new InvalidOperationException("AppSettings autoload is missing");

    public override void _Ready()
    {
        ScreenshotCapture.AttachIfRequested(this);

        GetNode<Label>("Screen/Content/Header/HeaderLabel").Text = Tr("UI_KEYS_HEADER");
        _rows = GetNode<VBoxContainer>("Screen/Content/Scroll/Rows");
        _warning = GetNode<Label>("Screen/Content/Warning");
        foreach (var action in KeyBindings.Actions)
        {
            _rows.AddChild(BuildRow(action));
        }

        var defaults = GetNode<Button>("Screen/Content/Footer/Default");
        defaults.Text = Tr("UI_BUTTON_DEFAULT");
        defaults.Pressed += OnDefaultPressed;
        var back = GetNode<Button>("Screen/Content/Footer/Back");
        back.Text = Tr("UI_BUTTON_BACK");
        back.Pressed += OnBackPressed;

        RefreshButtons();
        back.GrabFocus();
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        var waiting = _waitingFor;
        if (waiting is null)
        {
            if (inputEvent.IsActionPressed("ui_cancel"))
            {
                OnBackPressed();
            }

            return;
        }

        if (inputEvent is not InputEventKey { Pressed: true, Echo: false } key)
        {
            return;
        }

        GetViewport().SetInputAsHandled();
        if (key.Keycode == Key.Escape)
        {
            StopWaiting();
            return;
        }

        var name = OS.GetKeycodeString(key.Keycode);
        var result = Store.SetKey(waiting, name);
        if (result.Status == RebindStatus.Refused)
        {
            _warning.Text = Tr("UI_KEYS_REFUSED");
            return;
        }

        if (result.Status == RebindStatus.Swapped)
        {
            var other = KeyBindings.Actions.First(action => action.Id == result.OtherAction);
            _warning.Text = string.Format(Tr("UI_KEYS_SWAPPED"), name, Tr(other.TitleKey), result.OtherKey);
        }

        StopWaiting();
    }

    private HBoxContainer BuildRow(KeyAction action)
    {
        var label = new Label
        {
            Text = Tr(action.TitleKey),
            CustomMinimumSize = new Vector2(LabelWidth, RowHeight),
            VerticalAlignment = VerticalAlignment.Center,
        };

        var id = action.Id;
        var button = new Button
        {
            Name = id,
            CustomMinimumSize = new Vector2(KeyWidth, RowHeight),
        };
        button.Pressed += () => StartWaiting(id);
        _buttons[id] = button;

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", RowSeparation);
        row.AddChild(label);
        row.AddChild(button);
        return row;
    }

    /// <summary>Shows each action's key. The action being changed shows the prompt instead.</summary>
    private void RefreshButtons()
    {
        foreach (var action in KeyBindings.Actions)
        {
            _buttons[action.Id].Text = action.Id == _waitingFor ? Tr("UI_KEY_WAITING") : Store.Keys.KeyOf(action.Id);
        }
    }

    private void StartWaiting(string actionId)
    {
        _waitingFor = actionId;
        _warning.Text = string.Empty;
        RefreshButtons();
    }

    private void StopWaiting()
    {
        _waitingFor = null;
        RefreshButtons();
    }

    private void OnDefaultPressed()
    {
        Store.ResetKeys();
        _warning.Text = string.Empty;
        RefreshButtons();
    }

    private void OnBackPressed()
    {
        SceneRouter.Instance?.Back();
    }
}
