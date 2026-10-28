using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Rts.Client.App;

/// <summary>
/// The graphics test (the owner, 2026-10-28: «the game picks the settings for 60 FPS»): over the general battle of <c>--perf-battle</c>
/// it tries the levels of <see cref="GraphicsLadder"/> that <see cref="GraphicsSearch"/> asks for — each first drawn
/// <see cref="WarmFrames"/> frames, then the median of <see cref="SampleFrames"/> taken as its time — keeps the finest within
/// <see cref="GraphicsLadder.TargetMs"/> and goes back to the screen it came from (<see cref="LastResult"/> says what it chose). The time
/// is that of the graphics card for the frame, as Godot measures it: a frame limit or VSync does not stretch it, and a slow processor does
/// not count against the graphics; where the card does not report it (Metal on a Mac) the time of the whole frame stands in, and the
/// lightest level is measured too, as the share of the processor (<see cref="GraphicsSearch.Floor"/>). While it measures, VSync
/// and the frame limit are off. Esc stops it and puts the graphics back. Opened from the Graphics tab, and on the first start of the game
/// (<see cref="AppSettings.FirstStart"/>).
/// </summary>
public partial class GraphicsBenchmark : Node
{
    /// <summary>The argument of the router that opens the match scene as the graphics test.</summary>
    public const string Key = "graphics_test";

    /// <summary>Frames drawn at a new level before it is measured: its shaders compile, its shadow maps fill.</summary>
    public const int WarmFrames = 20;

    /// <summary>Frames measured at a level.</summary>
    public const int SampleFrames = 30;

    /// <summary>A frame of the whole game slower than this, with the graphics within the target, is held up by the processor.</summary>
    private const double FrameMs = 1000.0 / 60;

    private const string MainMenuScene = "res://scenes/ui/MainMenu.tscn";

    private readonly Func<bool> _start;
    private readonly GraphicsSearch _search = new();
    private readonly List<double> _gpu = new();
    private readonly List<double> _frames = new();
    private readonly Dictionary<int, double> _frameMs = new();
    private GraphicsLevel _before;
    private Rid _viewport;
    private bool _started;
    private bool _flooring;
    private double _held;
    private bool _done;
    private int _warm;
    private int _measured;
    private Label _status = null!;

    /// <param name="start">Sets the scene going; true once it has (the battle charges on its first tick).</param>
    public GraphicsBenchmark(Func<bool> start)
    {
        _start = start;
    }

    /// <summary>Whether the screen being entered is the graphics test.</summary>
    public static bool Requested => SceneRouter.Instance?.Args.ContainsKey(Key) == true;

    /// <summary>What the last test chose, for the Graphics tab to show; null before the first.</summary>
    public static string? LastResult { get; private set; }

    private static AppSettings Store =>
        AppSettings.Instance ?? throw new InvalidOperationException("AppSettings autoload is missing");

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _before = GraphicsLevel.Of(Store.Values);
        _viewport = GetViewport().GetViewportRid();
        RenderingServer.ViewportSetMeasureRenderTime(_viewport, true);
        Store.SetMeasuring(true);
        AddChild(Overlay());
        Begin(_search.Next!.Value);
    }

    public override void _Process(double delta)
    {
        if (_done)
        {
            return;
        }

        if (!_started)
        {
            _started = _start();
            return;
        }

        if (++_warm <= WarmFrames)
        {
            return;
        }

        _gpu.Add(RenderingServer.ViewportGetMeasuredRenderTimeGpu(_viewport));
        _frames.Add(delta * 1000.0);
        if (_gpu.Count < SampleFrames)
        {
            return;
        }

        var level = _flooring ? GraphicsLadder.Levels.Count - 1 : _search.Next!.Value;
        var gpu = Median(_gpu);
        var frame = Median(_frames);
        _frameMs[level] = frame;
        GD.Print($"[graphics test] level {level} {Describe(GraphicsLadder.Levels[level])}: graphics card {gpu:0.0} ms, frame {frame:0.0} ms");
        if (_flooring)
        {
            // The lightest level: what the processor takes. Now the first level counts against it.
            _flooring = false;
            _search.Floor(frame);
            GD.Print($"[graphics test] no time from the graphics card: a level may take {_search.TargetMs:0.0} ms of the whole frame");
            _search.Report(_held);
        }
        else if (gpu > 0)
        {
            _search.Report(gpu);
        }
        else if (_measured == 1)
        {
            // The card reports no time (Metal on a Mac): the whole frame stands in, the processor's share in it. The lightest level is
            // measured first, as the floor (GraphicsSearch.Floor).
            (_held, _flooring) = (frame, true);
            Begin(GraphicsLadder.Levels.Count - 1);
            return;
        }
        else
        {
            _search.Report(frame);
        }

        if (_search.Next is { } next)
        {
            Begin(next);
        }
        else
        {
            Finish();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!_done && @event.IsActionPressed("ui_cancel"))
        {
            GetViewport().SetInputAsHandled();
            // On the first start the defaults stay, and are kept: the test does not come back unasked.
            Store.SetGraphics(_before, save: Store.FirstStart);
            LastResult = Tr("UI_GRAPHICS_TEST_CANCELED");
            End();
        }
    }

    /// <summary>Tries <paramref name="level"/>: its graphics, not saved, and the count from the start.</summary>
    private void Begin(int level)
    {
        Store.SetGraphics(GraphicsLadder.Levels[level], save: false);
        (_warm, _measured) = (0, _measured + 1);
        _gpu.Clear();
        _frames.Clear();
        _status.Text = string.Format(Tr("UI_GRAPHICS_TEST_STEP"), _measured);
        GD.Print($"[graphics test] trying level {level}");
    }

    private void Finish()
    {
        var chosen = GraphicsLadder.Levels[_search.Result];
        Store.SetGraphics(chosen, save: true);
        var text = string.Format(Tr("UI_GRAPHICS_TEST_RESULT"), Describe(chosen));
        if (!_search.Fits)
        {
            text += " " + Tr("UI_GRAPHICS_TEST_SLOW");
        }
        else if (_frameMs.TryGetValue(_search.Result, out var frame) && frame > FrameMs)
        {
            text += " " + Tr("UI_GRAPHICS_TEST_PROCESSOR");
        }

        LastResult = text;
        GD.Print($"[graphics test] chose level {_search.Result}: {Describe(chosen)}");
        End();
    }

    private void End()
    {
        _done = true;
        RenderingServer.ViewportSetMeasureRenderTime(_viewport, false);
        Store.SetMeasuring(false);
        if (SceneRouter.Instance is not { } router)
        {
            return;
        }

        if (router.CanGoBack)
        {
            router.Back();
        }
        else
        {
            router.Replace(MainMenuScene);
        }
    }

    /// <summary>The level in words: «3D 85 %, MSAA 2×, shadows Low, SSAO off».</summary>
    private string Describe(GraphicsLevel level) => string.Format(
        Tr("UI_GRAPHICS_TEST_LEVEL"),
        level.RenderScale,
        Tr(level.AntiAliasing switch
        {
            AntiAliasing.Fxaa => "UI_AA_FXAA",
            AntiAliasing.Msaa2 => "UI_AA_MSAA2",
            AntiAliasing.Msaa4 => "UI_AA_MSAA4",
            _ => "UI_AA_OFF",
        }),
        Tr(level.Shadows switch
        {
            ShadowQuality.Low => "UI_QUALITY_LOW",
            ShadowQuality.Medium => "UI_QUALITY_MEDIUM",
            ShadowQuality.High => "UI_QUALITY_HIGH",
            _ => "UI_QUALITY_OFF",
        }),
        Tr(level.Ssao ? "UI_ON" : "UI_OFF"));

    private static double Median(List<double> values)
    {
        var sorted = values.OrderBy(value => value).ToList();
        return sorted[sorted.Count / 2];
    }

    /// <summary>What the test is doing, over the battle: a dark panel in the middle of the screen.</summary>
    private CanvasLayer Overlay()
    {
        var layer = new CanvasLayer { Layer = 50 };
        var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 8);
        box.AddChild(new Label { Text = Tr("UI_GRAPHICS_TEST_TITLE"), HorizontalAlignment = HorizontalAlignment.Center });
        _status = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        box.AddChild(_status);
        box.AddChild(new Label { Text = Tr("UI_GRAPHICS_TEST_CANCEL"), HorizontalAlignment = HorizontalAlignment.Center });
        var margin = new MarginContainer();
        foreach (var side in new[] { "left", "right", "top", "bottom" })
        {
            margin.AddThemeConstantOverride("margin_" + side, 24);
        }

        margin.AddChild(box);
        panel.AddChild(margin);
        panel.GrowHorizontal = Control.GrowDirection.Both;
        panel.GrowVertical = Control.GrowDirection.Both;
        layer.AddChild(panel);
        return layer;
    }
}
