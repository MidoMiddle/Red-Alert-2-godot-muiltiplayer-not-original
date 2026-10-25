using System.Collections.Generic;
using Godot;

namespace Rts.Client.App;

/// <summary>
/// Switches screens with a short fade and keeps a stack of previous screens for "back".
/// Registered as the <c>SceneRouter</c> autoload (docs/implementation/M6_menus_skirmish.md, 4.1).
/// </summary>
public partial class SceneRouter : Node
{
    /// <summary>The loading screen. Every match scene opens through it (docs/implementation/M6_menus_skirmish.md, 4.14).</summary>
    public const string LoadingScene = "res://scenes/ui/Loading.tscn";

    /// <summary>The match scene: the sandbox, a skirmish, the quick skirmish. It is always reached through <see cref="LoadingScene"/>.</summary>
    public const string MatchScene = "res://scenes/Main.tscn";

    /// <summary>The argument that names the scene the loading screen adds after it; <see cref="MatchScene"/> is its only value.</summary>
    public const string NextKey = "next";

    /// <summary>Whole fade: half to black, then half back.</summary>
    private const float FadeSeconds = 0.3f;

    /// <summary>Above the debug overlay of <c>Main</c>, which uses the default layer.</summary>
    private const int OverlayLayer = 100;

    private readonly Stack<string> _history = new();
    private ColorRect _fade = null!;
    private bool _switching;

    /// <summary>The running router. Set when the autoload is ready.</summary>
    public static SceneRouter? Instance { get; private set; }

    /// <summary>A screen is being switched to: another switch now is ignored, so a screen that must go somewhere waits for this to end.</summary>
    public bool Switching => _switching;

    /// <summary>Parameters for the screen that is being entered. Empty after <see cref="Back"/>.</summary>
    public Dictionary<string, object> Args { get; private set; } = new();

    public override void _Ready()
    {
        Instance = this;

        var layer = new CanvasLayer { Layer = OverlayLayer };
        AddChild(layer);

        _fade = new ColorRect
        {
            Color = Colors.Black,
            Modulate = new Color(1f, 1f, 1f, 0f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _fade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(_fade);
        InputScript.AttachIfRequested(this);
    }

    /// <summary>Opens a screen and remembers the current one, so <see cref="Back"/> returns to it.</summary>
    public void GoTo(string scenePath, Dictionary<string, object>? args = null)
    {
        var current = GetTree().CurrentScene?.SceneFilePath;
        if (!string.IsNullOrEmpty(current))
        {
            _history.Push(current);
        }

        Switch(scenePath, args);
    }

    /// <summary>Opens a screen and forgets the history. Used for the first screen after boot.</summary>
    public void Replace(string scenePath, Dictionary<string, object>? args = null)
    {
        _history.Clear();
        Switch(scenePath, args);
    }

    /// <summary>Returns to the previous screen. Does nothing if there is none.</summary>
    public void Back()
    {
        if (_history.Count == 0)
        {
            return;
        }

        Switch(_history.Pop(), null);
    }

    private void Switch(string scenePath, Dictionary<string, object>? args)
    {
        if (_switching)
        {
            return;
        }

        _switching = true;
        Args = args is null ? new Dictionary<string, object>() : new Dictionary<string, object>(args);
        if (scenePath == MatchScene)
        {
            Args[NextKey] = MatchScene;
            scenePath = LoadingScene;
        }

        _fade.MouseFilter = Control.MouseFilterEnum.Stop;

        var tween = CreateTween();
        tween.TweenProperty(_fade, "modulate:a", 1f, FadeSeconds / 2);
        tween.TweenCallback(Callable.From(() => ChangeScene(scenePath)));
        tween.TweenProperty(_fade, "modulate:a", 0f, FadeSeconds / 2);
        tween.TweenCallback(Callable.From(() =>
        {
            _fade.MouseFilter = Control.MouseFilterEnum.Ignore;
            _switching = false;
        }));
    }

    private void ChangeScene(string scenePath)
    {
        GD.Print($"[router] open {scenePath}");
        var error = GetTree().ChangeSceneToFile(scenePath);
        if (error != Error.Ok)
        {
            GD.PushError($"[router] cannot open {scenePath}: {error}");
        }
    }
}
