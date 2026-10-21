using System.Collections.Generic;
using Godot;

namespace Rts.Client.App;

/// <summary>
/// Switches screens with a short fade and keeps a stack of previous screens for "back".
/// Registered as the <c>SceneRouter</c> autoload (docs/implementation/M6_menus_skirmish.md, 4.1).
/// </summary>
public partial class SceneRouter : Node
{
    /// <summary>Whole fade: half to black, then half back.</summary>
    private const float FadeSeconds = 0.3f;

    /// <summary>Above the debug overlay of <c>Main</c>, which uses the default layer.</summary>
    private const int OverlayLayer = 100;

    private readonly Stack<string> _history = new();
    private ColorRect _fade = null!;
    private bool _switching;

    /// <summary>The running router. Set when the autoload is ready.</summary>
    public static SceneRouter? Instance { get; private set; }

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
        Args = args ?? new Dictionary<string, object>();
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
