using System;
using System.Globalization;
using System.Linq;
using Godot;
using Rts.Client.App;
using Rts.Client.Controls;

namespace Rts.Client.UI;

/// <summary>
/// Settings with four tabs (docs/menu/README.md, sections 2.5 and 3.7; docs/implementation/M6_menus_skirmish.md, 4.5).
/// The window options, the interface scale and the master volume work now. Every other option is greyed out with
/// "Coming soon" until its step comes, and changes nothing yet. A new resolution is shown at once and kept only after
/// the player confirms it within 15 seconds (<see cref="ResolutionChange"/>).
/// Debug argument: <c>--tab &lt;0-3&gt;</c> opens a tab, for visual checks.
/// </summary>
public partial class Settings : Control
{
    private const int GraphicsTab = 0;
    private const int AudioTab = 1;
    private const int ControlsTab = 2;
    private const int GameTab = 3;

    private const float LabelWidth = 320f;
    private const float FieldWidth = 360f;
    private const float FieldHeight = 48f;
    private const float ValueWidth = 80f;
    private const int RowSeparation = 24;
    private const int PageSeparation = 14;
    private const float ActionWidth = 360f;
    private const string KeyBindingsScene = "res://scenes/ui/KeyBindings.tscn";

    /// <summary>The theme's disabled text colour (#7D838C).</summary>
    private static readonly Color LockedText = new(0.49f, 0.514f, 0.549f);
    private static readonly Color LockedTint = new(1f, 1f, 1f, 0.5f);

    /// <summary>The tab shown last, so that coming back from the key bindings returns to it.</summary>
    private static int _lastTab;

    private readonly ResolutionChange _change = new();
    private TabContainer _tabs = null!;
    private Button _default = null!;
    private ConfirmationDialog _keepDialog = null!;
    private int _shownSeconds;

    private OptionButton _resolution = null!;
    private OptionButton _windowMode = null!;
    private CheckBox _vsync = null!;
    private OptionButton _fps = null!;
    private HSlider _interfaceScale = null!;
    private Label _interfaceScaleValue = null!;
    private HSlider _masterVolume = null!;
    private Label _masterVolumeValue = null!;
    private OptionButton _scheme = null!;
    private HSlider _scrollSpeed = null!;
    private Label _scrollSpeedValue = null!;
    private CheckBox _edgeScroll = null!;
    private CheckBox _cameraDrag = null!;
    private CheckBox _playtestSurvey = null!;
    private OptionButton _healthBars = null!;

    private static AppSettings Store =>
        AppSettings.Instance ?? throw new InvalidOperationException("AppSettings autoload is missing");

    public override void _Ready()
    {
        ScreenshotCapture.AttachIfRequested(this);

        GetNode<Label>("Screen/Content/Header/HeaderLabel").Text = Tr("UI_SETTINGS_HEADER");
        _tabs = GetNode<TabContainer>("Screen/Content/Tabs");
        AddGraphicsPage();
        AddAudioPage();
        AddControlsPage();
        AddGamePage();
        _tabs.SetTabTitle(GraphicsTab, Tr("UI_SETTINGS_TAB_GRAPHICS"));
        _tabs.SetTabTitle(AudioTab, Tr("UI_SETTINGS_TAB_AUDIO"));
        _tabs.SetTabTitle(ControlsTab, Tr("UI_SETTINGS_TAB_CONTROLS"));
        _tabs.SetTabTitle(GameTab, Tr("UI_SETTINGS_TAB_GAME"));

        _default = GetNode<Button>("Screen/Content/Footer/Default");
        _default.Text = Tr("UI_BUTTON_DEFAULT");
        _default.Pressed += OnDefaultPressed;
        var back = GetNode<Button>("Screen/Content/Footer/Back");
        back.Text = Tr("UI_BUTTON_BACK");
        back.Pressed += OnBackPressed;

        _keepDialog = new ConfirmationDialog
        {
            Title = Tr("UI_SETTINGS_KEEP_TITLE"),
            OkButtonText = Tr("UI_SETTINGS_KEEP"),
            CancelButtonText = Tr("UI_SETTINGS_REVERT"),
        };
        _keepDialog.Confirmed += OnKeepConfirmed;
        _keepDialog.Canceled += OnKeepCanceled;
        AddChild(_keepDialog);

        _tabs.CurrentTab = _lastTab;
        RefreshControls();
        OpenTabFromArguments();
        _tabs.TabChanged += _ =>
        {
            _lastTab = _tabs.CurrentTab;
            UpdateDefaultButton();
        };
        UpdateDefaultButton();
        back.GrabFocus();
    }

    public override void _Process(double delta)
    {
        if (!_change.IsPending)
        {
            return;
        }

        if (_change.Tick(delta) is WindowSize previous)
        {
            Store.PreviewResolution(previous);
            _keepDialog.Hide();
            GD.Print($"[settings] resolution reverted to {previous.Width}x{previous.Height}");
            RefreshControls();
        }
        else if (_change.SecondsLeft != _shownSeconds)
        {
            UpdateKeepText();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel") && !_keepDialog.Visible)
        {
            OnBackPressed();
        }
    }

    private void AddGraphicsPage()
    {
        var rows = AddPage("Graphics");

        _resolution = NewDropdown(0, SettingsValues.ResolutionChoices
            .Select(size => string.Format(Tr("UI_RESOLUTION_FORMAT"), size.Width, size.Height))
            .ToArray());
        _resolution.ItemSelected += index => RequestResolution(SettingsValues.ResolutionChoices[(int)index]);
        AddRow(rows, "UI_SETTINGS_RESOLUTION", _resolution);

        _windowMode = NewDropdown(0, Tr("UI_WINDOW_MODE_WINDOWED"), Tr("UI_WINDOW_MODE_FULLSCREEN"), Tr("UI_WINDOW_MODE_BORDERLESS"));
        _windowMode.ItemSelected += index =>
        {
            Store.SetWindowMode((WindowMode)index);
            RefreshControls();
        };
        AddRow(rows, "UI_SETTINGS_WINDOW_MODE", _windowMode);

        _vsync = NewCheckBox(false);
        _vsync.Toggled += enabled => Store.SetVSync(enabled);
        AddRow(rows, "UI_SETTINGS_VSYNC", _vsync);

        _fps = NewDropdown(0, SettingsValues.FpsChoices.Select(FpsText).ToArray());
        _fps.ItemSelected += index => Store.SetFpsLimit(SettingsValues.FpsChoices[(int)index]);
        AddRow(rows, "UI_SETTINGS_FRAME_LIMIT", _fps);

        AddRow(rows, "UI_SETTINGS_SHADOWS", NewDropdown(2, Tr("UI_QUALITY_LOW"), Tr("UI_QUALITY_MEDIUM"), Tr("UI_QUALITY_HIGH")), locked: true);
        AddRow(rows, "UI_SETTINGS_SSAO", NewCheckBox(true), locked: true);
        AddRow(rows, "UI_SETTINGS_EFFECTS", NewDropdown(2, Tr("UI_QUALITY_LOW"), Tr("UI_QUALITY_MEDIUM"), Tr("UI_QUALITY_HIGH")), locked: true);

        _interfaceScale = NewSlider(SettingsValues.MinInterfaceScale, SettingsValues.MaxInterfaceScale,
            SettingsValues.InterfaceScaleStep, SettingsValues.Defaults.InterfaceScale);
        _interfaceScaleValue = NewValueLabel(SettingsValues.Defaults.InterfaceScale);
        _interfaceScale.ValueChanged += value =>
        {
            Store.SetInterfaceScale((int)value);
            _interfaceScaleValue.Text = PercentText((int)value);
        };
        AddRow(rows, "UI_SETTINGS_INTERFACE_SCALE", WithValue(_interfaceScale, _interfaceScaleValue));
    }

    private void AddAudioPage()
    {
        var rows = AddPage("Audio");

        _masterVolume = NewSlider(SettingsValues.MinMasterVolume, SettingsValues.MaxMasterVolume, 1,
            SettingsValues.Defaults.MasterVolume);
        _masterVolumeValue = NewValueLabel(SettingsValues.Defaults.MasterVolume);
        _masterVolume.ValueChanged += value =>
        {
            Store.SetMasterVolume((int)value);
            _masterVolumeValue.Text = PercentText((int)value);
        };
        AddRow(rows, "UI_SETTINGS_VOLUME_MASTER", WithValue(_masterVolume, _masterVolumeValue));

        AddRow(rows, "UI_SETTINGS_VOLUME_MUSIC", LockedVolume(), locked: true);
        AddRow(rows, "UI_SETTINGS_VOLUME_SFX", LockedVolume(), locked: true);
        AddRow(rows, "UI_SETTINGS_VOLUME_VOICE", LockedVolume(), locked: true);
        AddRow(rows, "UI_SETTINGS_VOLUME_UI", LockedVolume(), locked: true);
        AddRow(rows, "UI_SETTINGS_MUSIC_ON", NewCheckBox(true), locked: true);
    }

    private void AddControlsPage()
    {
        var rows = AddPage("Controls");

        _scheme = NewDropdown(0, Tr("UI_SCHEME_CLASSIC"), Tr("UI_SCHEME_MODERN"));
        _scheme.ItemSelected += index => Store.SetScheme((ControlScheme)index);
        AddRow(rows, "UI_SETTINGS_SCHEME", _scheme);

        _scrollSpeed = NewSlider(SettingsValues.MinScrollSpeed, SettingsValues.MaxScrollSpeed, 1, SettingsValues.Defaults.ScrollSpeed);
        _scrollSpeedValue = NewCountLabel(SettingsValues.Defaults.ScrollSpeed);
        _scrollSpeed.ValueChanged += value =>
        {
            Store.SetScrollSpeed((int)value);
            _scrollSpeedValue.Text = CountText((int)value);
        };
        AddRow(rows, "UI_SETTINGS_SCROLL_SPEED", WithValue(_scrollSpeed, _scrollSpeedValue));

        _edgeScroll = NewCheckBox(true);
        _edgeScroll.Toggled += enabled => Store.SetEdgeScroll(enabled);
        AddRow(rows, "UI_SETTINGS_EDGE_SCROLL", _edgeScroll);

        _cameraDrag = NewCheckBox(true);
        _cameraDrag.Toggled += enabled => Store.SetCameraDrag(enabled);
        AddRow(rows, "UI_SETTINGS_CAMERA_DRAG", _cameraDrag);

        var keys = new Button
        {
            Name = "KeyBindings",
            Text = Tr("UI_BUTTON_EDIT"),
            CustomMinimumSize = new Vector2(ActionWidth, FieldHeight),
        };
        keys.Pressed += () => SceneRouter.Instance?.GoTo(KeyBindingsScene);
        AddRow(rows, "UI_SETTINGS_KEY_BINDINGS", keys);
    }

    private void AddGamePage()
    {
        var rows = AddPage("Game");

        // M4 step 13: the bars work now, in the order of HealthBarMode.
        _healthBars = NewDropdown((int)HealthBarMode.Damaged, Tr("UI_HEALTH_BARS_ALWAYS"), Tr("UI_HEALTH_BARS_DAMAGED"), Tr("UI_HEALTH_BARS_SELECTED"));
        _healthBars.ItemSelected += index => Store.SetHealthBars((HealthBarMode)(int)index);
        AddRow(rows, "UI_SETTINGS_HEALTH_BARS", _healthBars);
        AddRow(rows, "UI_SETTINGS_CONFIRM_SELL", NewCheckBox(true), locked: true);
        AddRow(rows, "UI_SETTINGS_HINTS", NewCheckBox(true), locked: true);

        // M8 step 14: the survey of the tests by people on the results screen.
        _playtestSurvey = NewCheckBox(false);
        _playtestSurvey.Name = "PlaytestSurvey";
        _playtestSurvey.TooltipText = Tr("UI_SETTINGS_PLAYTEST_SURVEY_HINT");
        _playtestSurvey.Toggled += enabled => Store.SetPlaytestSurvey(enabled);
        AddRow(rows, "UI_SETTINGS_PLAYTEST_SURVEY", _playtestSurvey);
    }

    /// <summary>Adds a tab page. Long pages scroll, so the tab stays usable at a large interface scale.</summary>
    private VBoxContainer AddPage(string name)
    {
        var rows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        rows.AddThemeConstantOverride("separation", PageSeparation);

        var page = new ScrollContainer { Name = name, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        page.AddChild(rows);
        _tabs.AddChild(page);
        return rows;
    }

    /// <summary>One line of a tab: the label on the left, the control on the right.</summary>
    private void AddRow(VBoxContainer rows, string labelKey, Control field, bool locked = false)
    {
        var label = new Label
        {
            Text = Tr(labelKey),
            CustomMinimumSize = new Vector2(LabelWidth, FieldHeight),
            VerticalAlignment = VerticalAlignment.Center,
        };

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", RowSeparation);
        row.AddChild(label);
        row.AddChild(field);
        rows.AddChild(row);

        if (locked)
        {
            label.AddThemeColorOverride("font_color", LockedText);
            Lock(field);
        }
    }

    /// <summary>Greys out an option that comes with a later step: it cannot be changed, and it says so on hover.</summary>
    private void Lock(Control field)
    {
        if (field is HBoxContainer box)
        {
            foreach (var child in box.GetChildren().OfType<Control>())
            {
                Lock(child);
            }

            return;
        }

        field.TooltipText = Tr("UI_COMING_SOON");
        switch (field)
        {
            case Slider slider:
                slider.Editable = false;
                slider.Modulate = LockedTint;
                break;
            case BaseButton button:
                button.Disabled = true;
                break;
            case Label label:
                label.AddThemeColorOverride("font_color", LockedText);
                break;
        }
    }

    private HBoxContainer LockedVolume()
    {
        return WithValue(NewSlider(SettingsValues.MinMasterVolume, SettingsValues.MaxMasterVolume, 1,
            SettingsValues.MaxMasterVolume), NewValueLabel(SettingsValues.Defaults.MasterVolume));
    }

    /// <summary>Shows a window size the player asked for and asks them to keep it. The old size comes back otherwise.</summary>
    private void RequestResolution(WindowSize next)
    {
        var previous = Store.Values.Resolution;
        if (next == previous || !_change.Begin(previous, next))
        {
            RefreshControls();
            return;
        }

        Store.PreviewResolution(next);
        _resolution.Selected = Array.IndexOf(SettingsValues.ResolutionChoices, next);
        Callable.From(ShowKeepDialog).CallDeferred();
    }

    private void ShowKeepDialog()
    {
        UpdateKeepText();
        _keepDialog.PopupCentered();
    }

    private void UpdateKeepText()
    {
        _shownSeconds = _change.SecondsLeft;
        _keepDialog.DialogText = string.Format(Tr("UI_SETTINGS_KEEP_RESOLUTION"), _shownSeconds);
    }

    private void OnKeepConfirmed()
    {
        if (_change.Keep() is WindowSize kept)
        {
            Store.CommitResolution(kept);
        }

        RefreshControls();
    }

    private void OnKeepCanceled()
    {
        if (_change.Revert() is WindowSize previous)
        {
            Store.PreviewResolution(previous);
        }

        RefreshControls();
    }

    /// <summary>DEFAULT resets the active options of the tab. A tab with only greyed options has nothing to reset.</summary>
    private void OnDefaultPressed()
    {
        var defaults = SettingsValues.Defaults;
        switch (_tabs.CurrentTab)
        {
            case GraphicsTab:
                Store.SetWindowMode(defaults.WindowMode);
                Store.SetVSync(defaults.VSync);
                Store.SetFpsLimit(defaults.FpsLimit);
                Store.SetInterfaceScale(defaults.InterfaceScale);
                RequestResolution(defaults.Resolution);
                break;
            case AudioTab:
                Store.SetMasterVolume(defaults.MasterVolume);
                break;
            case ControlsTab:
                Store.SetScheme(defaults.Scheme);
                Store.SetScrollSpeed(defaults.ScrollSpeed);
                Store.SetEdgeScroll(defaults.EdgeScroll);
                Store.SetCameraDrag(defaults.CameraDrag);
                Store.ResetKeys();
                break;
        }

        RefreshControls();
    }

    private void UpdateDefaultButton()
    {
        _default.Disabled = _tabs.CurrentTab is not (GraphicsTab or AudioTab or ControlsTab);
    }

    /// <summary>Shows the stored values in the controls. Their signals are not emitted, so nothing is applied twice.</summary>
    private void RefreshControls()
    {
        var values = Store.Values;
        if (!_change.IsPending)
        {
            _resolution.Selected = Array.IndexOf(SettingsValues.ResolutionChoices, values.Resolution);
        }

        var fullscreen = values.WindowMode == WindowMode.Fullscreen;
        _resolution.Disabled = fullscreen;
        _resolution.TooltipText = fullscreen ? Tr("UI_SETTINGS_RESOLUTION_HINT") : string.Empty;
        _windowMode.Selected = (int)values.WindowMode;
        _vsync.SetPressedNoSignal(values.VSync);
        _fps.Selected = Array.IndexOf(SettingsValues.FpsChoices, values.FpsLimit);
        _interfaceScale.SetValueNoSignal(values.InterfaceScale);
        _interfaceScaleValue.Text = PercentText(values.InterfaceScale);
        _masterVolume.SetValueNoSignal(values.MasterVolume);
        _masterVolumeValue.Text = PercentText(values.MasterVolume);
        _scheme.Selected = (int)values.Scheme;
        _scrollSpeed.SetValueNoSignal(values.ScrollSpeed);
        _scrollSpeedValue.Text = CountText(values.ScrollSpeed);
        _edgeScroll.SetPressedNoSignal(values.EdgeScroll);
        _cameraDrag.SetPressedNoSignal(values.CameraDrag);
        _healthBars.Selected = (int)values.HealthBars;
        _playtestSurvey.SetPressedNoSignal(values.PlaytestSurvey);
    }

    /// <summary>Opens the tab named by <c>--tab</c>, so the tabs can be checked from the command line.</summary>
    private void OpenTabFromArguments()
    {
        var args = OS.GetCmdlineUserArgs();
        var index = Array.IndexOf(args, "--tab");
        if (index >= 0 && index + 1 < args.Length && int.TryParse(args[index + 1], out var tab))
        {
            _tabs.CurrentTab = tab;
        }
    }

    private void OnBackPressed()
    {
        SceneRouter.Instance?.Back();
    }

    private string FpsText(int fps) =>
        fps == SettingsValues.Unlimited ? Tr("UI_FPS_UNLIMITED") : string.Format(Tr("UI_FPS_FORMAT"), fps);

    private string PercentText(int percent) => string.Format(Tr("UI_PERCENT"), percent);

    private OptionButton NewDropdown(int selected, params string[] items)
    {
        var option = new OptionButton { CustomMinimumSize = new Vector2(FieldWidth, FieldHeight) };
        foreach (var item in items)
        {
            option.AddItem(item);
        }

        option.Selected = selected;
        return option;
    }

    private static CheckBox NewCheckBox(bool pressed)
    {
        return new CheckBox { ButtonPressed = pressed };
    }

    private static HSlider NewSlider(double min, double max, double step, double value)
    {
        return new HSlider
        {
            MinValue = min,
            MaxValue = max,
            Step = step,
            Value = value,
            CustomMinimumSize = new Vector2(FieldWidth - ValueWidth - RowSeparation, FieldHeight),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
    }

    private Label NewValueLabel(int percent)
    {
        return new Label
        {
            Text = PercentText(percent),
            CustomMinimumSize = new Vector2(ValueWidth, FieldHeight),
            VerticalAlignment = VerticalAlignment.Center,
        };
    }

    private Label NewCountLabel(int count)
    {
        return new Label
        {
            Text = CountText(count),
            CustomMinimumSize = new Vector2(ValueWidth, FieldHeight),
            VerticalAlignment = VerticalAlignment.Center,
        };
    }

    private static string CountText(int count) => count.ToString(CultureInfo.InvariantCulture);

    private static HBoxContainer WithValue(Control control, Label value)
    {
        var box = new HBoxContainer();
        box.AddThemeConstantOverride("separation", RowSeparation);
        box.AddChild(control);
        box.AddChild(value);
        return box;
    }
}
