using Godot;
using Rts.Client.App;

namespace Rts.Client.UI;

/// <summary>
/// Every component of the theme on one screen, for visual checks. Opened from <see cref="Boot"/>
/// with <c>--ui-showcase</c>; add <c>--screenshot &lt;path&gt;</c> to save a frame and quit.
/// </summary>
public partial class ThemeShowcase : Control
{
    public override void _Ready()
    {
        var background = new ColorRect { Color = new Color(0.09f, 0.1f, 0.12f, 1f) };
        background.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var column = new VBoxContainer();
        column.SetAnchorsPreset(LayoutPreset.FullRect);
        column.OffsetLeft = 64f;
        column.OffsetTop = 48f;
        column.OffsetRight = -64f;
        column.OffsetBottom = -48f;
        column.AddThemeConstantOverride("separation", 16);
        AddChild(column);

        column.AddChild(NewLabel("UI_SHOWCASE_TITLE"));
        column.AddChild(ButtonRow());
        column.AddChild(PanelWithText());
        column.AddChild(ControlRow());
        column.AddChild(Tabs());

        ScreenshotCapture.AttachIfRequested(this);
    }

    private Label NewLabel(string key)
    {
        return new Label { Text = Tr(key) };
    }

    private PanelContainer PanelWithText()
    {
        var panel = new PanelContainer();
        panel.AddChild(NewLabel("UI_SHOWCASE_PANEL_TEXT"));
        return panel;
    }

    private HBoxContainer ButtonRow()
    {
        var row = new HBoxContainer();
        row.AddChild(new Button { Text = Tr("UI_SHOWCASE_BUTTON") });
        row.AddChild(new Button { Text = Tr("UI_SHOWCASE_BUTTON_SELECTED"), ToggleMode = true, ButtonPressed = true });
        row.AddChild(new Button { Text = Tr("UI_SHOWCASE_BUTTON_LOCKED"), Disabled = true });
        return row;
    }

    private HBoxContainer ControlRow()
    {
        var row = new HBoxContainer();
        row.AddChild(new CheckBox { Text = Tr("UI_SHOWCASE_CHECKBOX"), ButtonPressed = true });

        var option = new OptionButton();
        option.AddItem(Tr("UI_SHOWCASE_OPTION_FIRST"));
        option.AddItem(Tr("UI_SHOWCASE_OPTION_SECOND"));
        row.AddChild(option);

        row.AddChild(new HSlider { CustomMinimumSize = new Vector2(320f, 0f), Value = 40 });
        return row;
    }

    private TabContainer Tabs()
    {
        var tabs = new TabContainer { CustomMinimumSize = new Vector2(0f, 240f) };

        var first = NewLabel("UI_SHOWCASE_TAB_TEXT");
        first.Name = "FirstTab";
        tabs.AddChild(first);
        tabs.SetTabTitle(0, Tr("UI_SHOWCASE_TAB_FIRST"));

        var scroll = new ScrollContainer { Name = "SecondTab" };
        var text = NewLabel("UI_SHOWCASE_SCROLL_TEXT");
        text.AutowrapMode = TextServer.AutowrapMode.Word;
        text.CustomMinimumSize = new Vector2(1000f, 0f);
        scroll.AddChild(text);
        tabs.AddChild(scroll);
        tabs.SetTabTitle(1, Tr("UI_SHOWCASE_TAB_SECOND"));

        return tabs;
    }
}
