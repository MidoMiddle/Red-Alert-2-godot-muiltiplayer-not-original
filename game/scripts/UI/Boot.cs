using System.Linq;
using Godot;
using Rts.Client.App;

namespace Rts.Client.UI;

/// <summary>
/// Entry scene. Shows a short splash, then opens the first screen.
/// Debug arguments skip the splash: <c>--smoke</c> and <c>--screenshot</c> go to <c>Main</c>
/// so the M0 checks keep working, and <c>--ui-showcase</c> goes to the component showcase.
/// </summary>
public partial class Boot : Control
{
    private const double SplashSeconds = 1.5;
    private const string MainScene = "res://scenes/Main.tscn";
    private const string FirstScreen = "res://scenes/ui/ScreenStub.tscn";
    private const string ShowcaseScene = "res://scenes/ui/ThemeShowcase.tscn";

    private double _elapsed;
    private bool _finished;

    public override void _Ready()
    {
        var args = OS.GetCmdlineUserArgs();
        if (args.Contains("--ui-showcase"))
        {
            OpenDebugScene(ShowcaseScene);
        }
        else if (args.Contains("--smoke") || args.Contains("--screenshot"))
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

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!_finished && @event is (InputEventKey { Pressed: true } or InputEventMouseButton { Pressed: true }))
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

        SceneRouter.Instance.Replace(FirstScreen);
    }
}
