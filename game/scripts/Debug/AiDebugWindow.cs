using Godot;
using Rts.AI.Debug;

namespace Rts.Client.Debug;

/// <summary>
/// The debug window of the bots (docs/implementation/M8_ai_difficulty.md, 4.9, step 12): Shift with the key of the overlay (F12) opens it
/// by the sidebar, a tab for every section of the <see cref="AiDebugSnapshot"/> — Summary, Economy, Army, Enemies, Squads, Team,
/// Decisions. <see cref="AiDebugOverlay"/> shows it and fills it; it only reads.
/// </summary>
public partial class AiDebugWindow : PanelContainer
{
    private const int Width = 760;

    private Label _title = null!;
    private TabContainer _tabs = null!;

    public override void _Ready()
    {
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsPreset(LayoutPreset.RightWide);
        GrowHorizontal = GrowDirection.Begin;
        OffsetLeft = -Width - Hud.SidebarLayout.Width - 12;
        OffsetRight = -Hud.SidebarLayout.Width - 12;
        OffsetTop = 12;
        OffsetBottom = -12;
        AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0.05f, 0.07f, 0.06f, 0.88f), ContentMarginLeft = 10, ContentMarginRight = 10, ContentMarginTop = 8, ContentMarginBottom = 8 });

        var column = new VBoxContainer();
        AddChild(column);
        _title = new Label();
        _title.AddThemeFontSizeOverride("font_size", 18);
        _title.AddThemeColorOverride("font_color", new Color(0.75f, 1f, 0.85f));
        column.AddChild(_title);
        _tabs = new TabContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        // Seven tabs side by side, none hidden behind the arrows.
        _tabs.AddThemeFontSizeOverride("font_size", 14);
        column.AddChild(_tabs);
    }

    /// <summary>Fills the tabs from <paramref name="snapshot"/>; an empty window without a bot.</summary>
    public void Show(AiDebugSnapshot? snapshot, int index, int count)
    {
        _title.Text = snapshot is null ? "No bot in this match" : $"Bot {index + 1} of {count} · Tab — the next one · Shift+F12 — close";
        var sections = snapshot?.Sections() ?? [];
        for (var i = 0; i < sections.Length; i++)
        {
            var (title, text) = sections[i];
            if (i >= _tabs.GetTabCount())
            {
                var scroll = new ScrollContainer { Name = title, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
                var label = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = SizeFlags.ExpandFill };
                label.AddThemeFontSizeOverride("font_size", 15);
                scroll.AddChild(label);
                _tabs.AddChild(scroll);
            }

            _tabs.GetChild<ScrollContainer>(i).GetChild<Label>(0).Text = text;
        }
    }
}
