using System.Collections.Generic;
using System.Linq;
using Godot;
using Rts.Client.App;
using Rts.Client.Maps;

namespace Rts.Client.Matches;

/// <summary>
/// The debug «Quick skirmish» (docs/implementation/M5_first_bot.md, step 2, decision of the owner): a map of the catalog with two starts
/// or more, played one against one with a «Medium» bot (<see cref="MatchSetups.OneOnOne"/>). Temporary: the skirmish screen of M6 sets
/// matches up in full.
/// </summary>
public partial class QuickSkirmish : Control
{
    private const string MatchScene = "res://scenes/Main.tscn";
    private const int Width = 560;
    private const int ButtonHeight = 48;

    private List<MapEntry> _maps = [];
    private MapEntry? _shown;
    private MapPreviewView _preview = null!;
    private Label _info = null!;
    private Button _start = null!;

    public override void _Ready()
    {
        ScreenshotCapture.AttachIfRequested(this);
        var column = GetNode<VBoxContainer>("Column");
        column.AddChild(new Label { Name = "Title", Text = Tr("UI_QUICK_SKIRMISH_TITLE") });
        column.AddChild(new Label { Name = "Who", Text = Tr("UI_QUICK_SKIRMISH_WHO") });

        var (maps, errors) = MapCatalog.Load(MapPreviews.ReadMapFiles());
        foreach (var error in errors)
        {
            GD.PushWarning($"Map left out: {error}");
        }

        // The maps for two first: one against one is what this screen plays.
        _maps = maps.Where(entry => MatchSetups.IsPlayable(entry.Map))
            .OrderBy(entry => entry.Map.Players)
            .ThenBy(entry => entry.Map.Name, System.StringComparer.Ordinal)
            .ToList();
        var list = new OptionButton { Name = "MapList", CustomMinimumSize = new Vector2(Width, ButtonHeight) };
        foreach (var entry in _maps)
        {
            list.AddItem(entry.Map.Name);
        }

        list.ItemSelected += index => ShowMap(_maps[(int)index]);
        column.AddChild(list);
        _preview = new MapPreviewView { Name = "Preview", CustomMinimumSize = new Vector2(Width, 400) };
        column.AddChild(_preview);
        _info = new Label { Name = "Info", AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(Width, 0) };
        column.AddChild(_info);

        _start = new Button { Name = "Start", Text = Tr("UI_QUICK_SKIRMISH_START"), CustomMinimumSize = new Vector2(Width, ButtonHeight) };
        _start.Pressed += Start;
        column.AddChild(_start);
        var back = new Button { Name = "Back", Text = Tr("UI_BUTTON_BACK"), CustomMinimumSize = new Vector2(Width, ButtonHeight) };
        back.Pressed += () => SceneRouter.Instance?.Back();
        column.AddChild(back);

        _start.Disabled = _maps.Count == 0;
        if (_maps.Count == 0)
        {
            _info.Text = Tr("UI_QUICK_SKIRMISH_NO_MAPS");
            back.GrabFocus();
            return;
        }

        ShowMap(_maps[0]);
        _start.GrabFocus();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel"))
        {
            SceneRouter.Instance?.Back();
        }
    }

    private async void ShowMap(MapEntry entry)
    {
        _shown = entry;
        var map = entry.Map;
        _info.Text = string.Format(Tr("UI_MAP_INFO"), map.Name, map.Width, map.Height, map.Players);
        var texture = MapPreviews.TryLoad(entry);
        if (texture is null)
        {
            _preview.Show(null, null);
            texture = await MapPreviews.RenderAsync(entry);
            if (!IsInsideTree() || !ReferenceEquals(_shown, entry))
            {
                return;
            }
        }

        _preview.Show(texture, map);
    }

    private void Start()
    {
        if (_shown is null)
        {
            return;
        }

        var setup = MatchSetups.OneOnOne(_shown.Map, DataFiles.Current.Rules.DefaultFaction.Id, MatchSetups.NewSeed());
        SceneRouter.Instance?.GoTo(MatchScene, new Dictionary<string, object>
        {
            [SimulationHost.MapArgument] = _shown.File,
            [SimulationHost.MatchArgument] = setup,
        });
    }
}
