using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Godot;
using Rts.Client.App;
using Rts.Client.Maps;
using Rts.Client.Matches;
using Rts.Simulation.Matches;
using Rts.Simulation.Replays;

namespace Rts.Client.UI;

/// <summary>
/// The list of replays (docs/menu/README.md, 3.8; docs/implementation/M6_menus_skirmish.md, 4.16, step 15): every <c>.rtsreplay</c> under
/// user://replays — those saved from the results, by F5, of network matches and of desyncs — newest first, a row each: the date, the
/// map, the players, the length, the rules. One this build cannot play (another format or rules version) is grey with «old» and is not
/// watched. Watch (or a double click) opens it in the match scene through the loading screen; leaving it comes back here. There is no
/// delete: the files are in the folder the screen names.
/// </summary>
public partial class ReplaysScreen : Control
{
    public const string Scene = "res://scenes/ui/Replays.tscn";
    private const string Folder = "user://replays";

    private readonly List<Row> _rows = new();
    private Tree _table = null!;
    private Button _watch = null!;

    private sealed record Row(string Path, ReplayHeader Header, string? Map);

    public override void _Ready()
    {
        ScreenshotCapture.AttachIfRequested(this);
        var background = new ColorRect { Color = new Color(0.09f, 0.1f, 0.12f) };
        background.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var column = new VBoxContainer();
        column.SetAnchorsPreset(LayoutPreset.FullRect);
        column.OffsetLeft = 96;
        column.OffsetTop = 72;
        column.OffsetRight = -96;
        column.OffsetBottom = -72;
        column.AddThemeConstantOverride("separation", 16);
        AddChild(column);

        var title = new Label { Text = Tr("UI_REPLAYS_TITLE") };
        title.AddThemeFontSizeOverride("font_size", 34);
        column.AddChild(title);

        _table = new Tree
        {
            Columns = 5,
            ColumnTitlesVisible = true,
            HideRoot = true,
            SelectMode = Tree.SelectModeEnum.Row,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        string[] headers = ["UI_REPLAYS_DATE", "UI_REPLAYS_MAP", "UI_REPLAYS_PLAYERS", "UI_REPLAYS_LENGTH", "UI_REPLAYS_VERSION"];
        for (var i = 0; i < headers.Length; i++)
        {
            _table.SetColumnTitle(i, Tr(headers[i]));
            _table.SetColumnExpand(i, i == 2);
            _table.SetColumnCustomMinimumWidth(i, i switch { 0 => 230, 1 => 240, 2 => 360, _ => 150 });
        }

        _table.ItemSelected += () => _watch.Disabled = Selected() is not { Header.Playable: true };
        _table.ItemActivated += Watch;
        column.AddChild(_table);

        var hint = new Label
        {
            Text = string.Format(CultureInfo.InvariantCulture, Tr("UI_REPLAYS_FOLDER"), ProjectSettings.GlobalizePath(Folder)),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        hint.AddThemeColorOverride("font_color", new Color(0.62f, 0.64f, 0.68f));
        column.AddChild(hint);

        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", 24);
        _watch = new Button { Text = Tr("UI_REPLAYS_WATCH"), CustomMinimumSize = new Vector2(240, 48), Disabled = true };
        _watch.Pressed += Watch;
        buttons.AddChild(_watch);
        var back = new Button { Text = Tr("UI_BUTTON_BACK"), CustomMinimumSize = new Vector2(240, 48) };
        back.Pressed += () => SceneRouter.Instance?.Back();
        buttons.AddChild(back);
        column.AddChild(buttons);

        Fill();

        // --watch FILE|latest: watches it at once, after the router's fade into this screen.
        var args = OS.GetCmdlineUserArgs();
        if (Array.IndexOf(args, "--watch") is var at and >= 0 && at + 1 < args.Length)
        {
            var wanted = args[at + 1];
            var index = wanted == "latest"
                ? _rows.FindIndex(row => row.Header.Playable)
                : _rows.FindIndex(row => row.Path == ProjectSettings.GlobalizePath(wanted) || row.Path == Path.GetFullPath(wanted));
            if (index >= 0)
            {
                GetTree().CreateTimer(0.5).Timeout += () => Watch(_rows[index]);
            }
        }
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel"))
        {
            SceneRouter.Instance?.Back();
            GetViewport().SetInputAsHandled();
        }
    }

    // Every replay under the folder, newest first; one that cannot be read at all is left out.
    private void Fill()
    {
        var maps = MapCatalog.Load(MapPreviews.ReadMapFiles()).Maps;
        var root = _table.CreateItem();
        var files = Files(ProjectSettings.GlobalizePath(Folder)).OrderByDescending(File.GetLastWriteTime).ToList();
        foreach (var file in files)
        {
            ReplayHeader header;
            Replay? replay = null;
            try
            {
                var bytes = File.ReadAllBytes(file);
                header = ReplayFormat.ReadHeader(bytes);
                if (header.FormatVersion == ReplayFormat.FormatVersion)
                {
                    replay = ReplayFormat.Read(bytes);
                }
            }
            catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                GD.Print($"[replays] {file}: {error.Message}");
                continue;
            }

            var setup = replay?.Settings.Match;
            var entry = setup is null ? null : maps.FirstOrDefault(map => map.Map.Id == setup.MapId);
            var item = _table.CreateItem(root);
            item.SetText(0, File.GetLastWriteTime(file).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
            // A file of another format is not read past its header: its map and players are unknown.
            item.SetText(1, replay is null ? "—" : entry?.Map.Name ?? setup?.MapId ?? Tr("UI_REPLAYS_SANDBOX"));
            item.SetText(2, replay is null ? "—" : ReplayListing.Players(setup, replay.Network, _ => Tr("UI_RESULTS_YOU"),
                bot => string.Format(Tr("UI_RESULTS_BOT"), SkirmishNames.Level(this, bot.Difficulty), SkirmishNames.Personality(this, bot.Personality))));
            item.SetText(3, ReplayListing.Length(header.TickCount, header.TicksPerSecond));
            item.SetText(4, ReplayListing.Version(header, Tr("UI_REPLAYS_OLD")));
            item.SetMetadata(0, _rows.Count);
            if (!header.Playable)
            {
                for (var i = 0; i < _table.Columns; i++)
                {
                    item.SetCustomColor(i, new Color(0.5f, 0.52f, 0.55f));
                }
            }

            _rows.Add(new Row(file, header, entry?.File));
        }

        if (_rows.Count == 0)
        {
            _table.CreateItem(root).SetText(2, Tr("UI_REPLAYS_NONE"));
        }
    }

    private Row? Selected() =>
        _table.GetSelected() is { } item && item.GetMetadata(0).VariantType == Variant.Type.Int ? _rows[item.GetMetadata(0).AsInt32()] : null;

    // Into the match scene through the loading screen: the map of the replay when the catalog has it, for its look; any other map
    // gives way to the land of the replay.
    private void Watch()
    {
        if (Selected() is { Header.Playable: true } row)
        {
            Watch(row);
        }
    }

    private void Watch(Row row)
    {
        GD.Print($"[replays] watch {row.Path}");
        SceneRouter.Instance?.GoTo(SceneRouter.MatchScene, new Dictionary<string, object>
        {
            [SimulationHost.MapArgument] = row.Map ?? SimulationHost.SandboxMapPath,
            [SimulationHost.ReplayArgument] = row.Path,
        });
    }

    private static IEnumerable<string> Files(string folder) =>
        Directory.Exists(folder) ? Directory.EnumerateFiles(folder, "*" + ReplayFormat.FileExtension, SearchOption.AllDirectories) : [];
}
