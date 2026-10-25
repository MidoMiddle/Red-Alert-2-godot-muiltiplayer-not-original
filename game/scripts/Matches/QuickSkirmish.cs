using System.Collections.Generic;
using System.Linq;
using Godot;
using Rts.Client.App;
using Rts.Client.Maps;

namespace Rts.Client.Matches;

/// <summary>
/// The debug «Quick skirmish» (docs/implementation/M5_first_bot.md, step 2, decision of the owner): a map of the catalog with two starts
/// or more, played one against one with a «Medium» bot (<see cref="MatchSetups.OneOnOne"/>); on a map of four starts the «Mode» button
/// switches to two against two, the person with a bot ally (<see cref="MatchSetups.TwoOnTwo"/>, M6, A6). Temporary: the skirmish screen
/// of M6 sets matches up in full.
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
    private Label _who = null!;
    private Button _mode = null!;
    private Button _start = null!;
    private bool _twoOnTwo;

    public override void _Ready()
    {
        ScreenshotCapture.AttachIfRequested(this);
        var column = GetNode<VBoxContainer>("Column");
        column.AddChild(new Label { Name = "Title", Text = Tr("UI_QUICK_SKIRMISH_TITLE") });
        _who = new Label { Name = "Who" };
        column.AddChild(_who);

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
        _mode = new Button { Name = "Mode", CustomMinimumSize = new Vector2(Width, ButtonHeight) };
        _mode.Pressed += () => SetMode(!_twoOnTwo);
        column.AddChild(_mode);

        _start = new Button { Name = "Start", Text = Tr("UI_QUICK_SKIRMISH_START"), CustomMinimumSize = new Vector2(Width, ButtonHeight) };
        _start.Pressed += Start;
        column.AddChild(_start);
        var back = new Button { Name = "Back", Text = Tr("UI_BUTTON_BACK"), CustomMinimumSize = new Vector2(Width, ButtonHeight) };
        back.Pressed += () => SceneRouter.Instance?.Back();
        column.AddChild(back);

        _start.Disabled = _maps.Count == 0;
        SetMode(false);
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

    /// <summary>One against one, or two against two where the map shown has the starts for it.</summary>
    private void SetMode(bool twoOnTwo)
    {
        var possible = _shown is not null && MatchSetups.IsPlayableTwoOnTwo(_shown.Map);
        _twoOnTwo = twoOnTwo && possible;
        _mode.Disabled = !possible;
        _mode.Text = Tr(_twoOnTwo ? "UI_QUICK_SKIRMISH_MODE_2V2" : "UI_QUICK_SKIRMISH_MODE_1V1");
        _who.Text = Tr(_twoOnTwo ? "UI_QUICK_SKIRMISH_WHO_2V2" : "UI_QUICK_SKIRMISH_WHO");
    }

    private async void ShowMap(MapEntry entry)
    {
        _shown = entry;
        SetMode(_twoOnTwo);
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

        var faction = DataFiles.Current.Rules.DefaultFaction.Id;
        var setup = _twoOnTwo
            ? MatchSetups.TwoOnTwo(_shown.Map, faction, MatchSetups.NewSeed(), DataFiles.Current.Rules.Database.Countries)
            : MatchSetups.OneOnOne(_shown.Map, faction, MatchSetups.NewSeed(), DataFiles.Current.Rules.Database.Countries);
        SceneRouter.Instance?.GoTo(MatchScene, new Dictionary<string, object>
        {
            [SimulationHost.MapArgument] = _shown.File,
            [SimulationHost.MatchArgument] = setup,
        });
    }
}
