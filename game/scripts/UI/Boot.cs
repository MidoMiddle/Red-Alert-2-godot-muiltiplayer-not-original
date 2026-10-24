using System.Linq;
using Godot;
using Rts.Client.App;

namespace Rts.Client.UI;

/// <summary>
/// Entry scene. Shows a short splash, then opens the main menu. Debug arguments skip the splash:
/// <c>--smoke</c>, <c>--check-camera</c>, <c>--check-terrain</c> and <c>--screenshot</c> go straight to <c>Main</c>, so the M0 checks keep
/// working; <c>--sandbox</c> goes through the loading screen, as every match does (docs/implementation/M6_menus_skirmish.md, 4.14).
/// <c>--ui-menu</c>, <c>--ui-skirmish</c>, <c>--ui-settings</c>, <c>--ui-stub</c> and <c>--ui-showcase</c> open UI screens for visual checks;
/// <c>--ui-loading</c> opens the loading screen and holds it at its end.
/// </summary>
public partial class Boot : Control
{
    private const double SplashSeconds = 1.5;
    private const string MainMenuScene = "res://scenes/ui/MainMenu.tscn";
    private const string SkirmishScene = "res://scenes/ui/SkirmishSetup.tscn";
    private const string EditorScene = "res://scenes/editor/MapEditor.tscn";
    private const string SettingsScene = "res://scenes/ui/Settings.tscn";
    private const string StubScene = "res://scenes/ui/ScreenStub.tscn";
    private const string ShowcaseScene = "res://scenes/ui/ThemeShowcase.tscn";
    private const string MainScene = "res://scenes/Main.tscn";
    private const string UnitGalleryScene = "res://scenes/ui/UnitGallery.tscn";

    private double _elapsed;
    private bool _finished;

    public override void _Ready()
    {
        Localization.Load();
        // A building with a model stands as high as the table of models says, for the bars over it.
        Terrain.BuildingShape.ModelHeight = id => Models.ModelLibrary.Find("building/" + id) is { Height: > 0f } entry ? entry.Height : null;

        var args = OS.GetCmdlineUserArgs();
        if (args.Contains("--ui-showcase"))
        {
            OpenDebugScene(ShowcaseScene);
        }
        else if (args.Contains("--ui-menu"))
        {
            OpenDebugScene(MainMenuScene);
        }
        else if (args.Contains("--ui-units"))
        {
            OpenDebugScene(UnitGalleryScene);
        }
        else if (args.Contains("--ui-skirmish"))
        {
            OpenDebugScene(SkirmishScene);
        }
        else if (args.Contains("--ui-editor"))
        {
            OpenDebugScene(EditorScene);
        }
        else if (args.Contains("--ui-settings"))
        {
            OpenDebugScene(SettingsScene);
        }
        else if (args.Contains("--ui-stub"))
        {
            OpenDebugScene(StubScene);
        }
        else if (args.Contains("--ui-loading"))
        {
            OpenDebugScene(SceneRouter.LoadingScene);
        }
        else if (args.Contains("--smoke") || args.Contains("--screenshot") || args.Contains("--check-camera") || args.Contains("--check-terrain"))
        {
            OpenDebugScene(MainScene);
        }
        else if (args.Contains("--sandbox"))
        {
            OpenDebugScene(SceneRouter.LoadingScene);
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
