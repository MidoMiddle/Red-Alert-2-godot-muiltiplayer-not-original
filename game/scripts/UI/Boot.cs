using System.Linq;
using Godot;
using Rts.Client.App;

namespace Rts.Client.UI;

/// <summary>
/// Entry scene. Shows a short splash, then opens the main menu. Debug arguments skip the splash:
/// <c>--sandbox</c>, <c>--smoke</c> and <c>--screenshot</c> go to <c>Main</c>, so the M0 checks and the manual
/// checklist keep working; <c>--ui-menu</c>, <c>--ui-skirmish</c>, <c>--ui-settings</c>, <c>--ui-stub</c> and
/// <c>--ui-showcase</c> open UI screens for visual checks.
/// </summary>
public partial class Boot : Control
{
    private const double SplashSeconds = 1.5;
    private const string MainMenuScene = "res://scenes/ui/MainMenu.tscn";
    private const string SkirmishScene = "res://scenes/ui/SkirmishSetup.tscn";
    private const string SettingsScene = "res://scenes/ui/Settings.tscn";
    private const string StubScene = "res://scenes/ui/ScreenStub.tscn";
    private const string ShowcaseScene = "res://scenes/ui/ThemeShowcase.tscn";
    private const string MainScene = "res://scenes/Main.tscn";

    private double _elapsed;
    private bool _finished;

    public override void _Ready()
    {
        Localization.Load();

        var args = OS.GetCmdlineUserArgs();
        if (args.Contains("--ui-showcase"))
        {
            OpenDebugScene(ShowcaseScene);
        }
        else if (args.Contains("--ui-menu"))
        {
            OpenDebugScene(MainMenuScene);
        }
        else if (args.Contains("--ui-skirmish"))
        {
            OpenDebugScene(SkirmishScene);
        }
        else if (args.Contains("--ui-settings"))
        {
            OpenDebugScene(SettingsScene);
        }
        else if (args.Contains("--ui-stub"))
        {
            OpenDebugScene(StubScene);
        }
        else if (args.Contains("--sandbox") || args.Contains("--smoke") || args.Contains("--screenshot"))
        {
            OpenDebugScene(MainScene);
        }
    }

    public override void _Process(double delta)
    {
        if (_finished)
        {
            return;
        }

        _elapsed += delta;
        if (_elapsed >= SplashSeconds)
        {
            Finish();
        }
    }

    /// <summary>A click skips the splash. The root is a Control, so clicks arrive here, not in <c>_UnhandledInput</c>.</summary>
    public override void _GuiInput(InputEvent @event)
    {
        if (!_finished && @event is InputEventMouseButton { Pressed: true })
        {
            Finish();
        }
    }

    /// <summary>A key skips the splash.</summary>
    public override void _UnhandledInput(InputEvent @event)
    {
        if (!_finished && @event is InputEventKey { Pressed: true })
        {
            Finish();
        }
    }

    private void OpenDebugScene(string scenePath)
    {
        _finished = true;
        GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, scenePath);
    }

    private void Finish()
    {
        _finished = true;
        if (SceneRouter.Instance is null)
        {
            GD.PushError("[boot] SceneRouter autoload is missing");
            return;
        }

        SceneRouter.Instance.Replace(MainMenuScene);
    }
}
