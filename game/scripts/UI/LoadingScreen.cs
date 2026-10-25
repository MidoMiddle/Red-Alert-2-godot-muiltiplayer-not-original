using System;
using System.Threading.Tasks;
using Godot;
using Rts.Client.App;
using Rts.Client.Maps;

namespace Rts.Client.UI;

/// <summary>
/// The loading screen between a menu and a match (docs/implementation/M6_menus_skirmish.md, 4.14). It runs the stages of
/// <see cref="LoadingPlan"/>, one frame each, so the bar moves: the data and the map are read and parsed on a background thread;
/// the match scene is then added to the tree, which builds its simulation and its ground; the first tick spawns the starting units,
/// and their models are drawn on the next frames; the shaders are warmed up (<see cref="ShaderWarmup"/>); and the match starts,
/// becoming the current scene. <see cref="SceneRouter"/> opens it before every match scene. With <c>--ui-loading</c> it stops at
/// the end, for a screenshot. It is a canvas layer above the match's HUD (layer 1) and below the router's fade (layer 100).
/// </summary>
public partial class LoadingScreen : CanvasLayer
{
    private const string HoldArgument = "--ui-loading";
    private const string TitleKey = "UI_LOADING_TITLE";
    private const string PercentKey = "UI_LOADING_PERCENT";

    /// <summary>Accent of the faction, darkened to a flat background (the sides: Union until M11).</summary>
    private const float BackgroundDarkening = 0.5f;

    private static readonly Random Hints = new();

    private ColorRect _background = null!;
    private Label _stage = null!;
    private Label _percent = null!;
    private ProgressBar _bar = null!;
    private Label _hint = null!;

    public override void _Ready()
    {
        Localization.Load();
        ScreenshotCapture.AttachIfRequested(this);

        _background = GetNode<ColorRect>("Background");
        _stage = GetNode<Label>("Stage");
        _percent = GetNode<Label>("Percent");
        _bar = GetNode<ProgressBar>("Bar");
        _hint = GetNode<Label>("Hint");

        GetNode<Label>("Title").Text = Tr(TitleKey);
        _background.Color = new Color(FactionPalette.AccentHex(Faction.Union)).Darkened(BackgroundDarkening);
        _hint.Text = Tr(LoadingPlan.HintKeys[Hints.Next(LoadingPlan.HintKeys.Count)]);
        _percent.Text = string.Format(Tr(PercentKey), 0);

        _ = RunAsync();
    }

    /// <summary>The stages in order. A failure stops the game with the reason in the console, as a data error does.</summary>
    private async Task RunAsync()
    {
        try
        {
            var next = NextScene();
            var hold = OS.GetCmdlineUserArgs().Contains(HoldArgument);

            // 1. The rules, read and parsed on a background thread. A mistake in them has been printed already.
            await Begin(0);
            if (!await Task.Run(DataFiles.TryLoad))
            {
                GetTree().Quit(1);
                return;
            }

            await End(0);

            // 2. The map: the file is read here, its text parsed on a background thread, and the host takes the result.
            await Begin(1);
            var path = SimulationHost.MapPath;
            var text = Godot.FileAccess.GetFileAsString(path);
            if (string.IsNullOrEmpty(text))
            {
                throw new InvalidOperationException($"Cannot read {path}: {Godot.FileAccess.GetOpenError()}.");
            }

            var rules = DataFiles.Current.Rules;
            var (map, definition) = await Task.Run(() => PreparedMap.Parse(path, text, rules));
            PreparedMap.Store(path, map, definition);
            await End(1);

            // 3. The ground: the match scene is built (the host starts its simulation and the terrain is drawn). Nothing ticks yet.
            await Begin(2);
            var match = GD.Load<PackedScene>(next).Instantiate<Node3D>();
            GetTree().Root.AddChild(match);
            var host = match.GetNode<SimulationHost>("SimulationHost");
            host.Paused = true;
            await End(2);

            // 4. The models: the first tick brings the starting units into the world; their views are made on the next frames.
            await Begin(3);
            host.StepOnce();
            await NextFrame();
            await NextFrame();
            await End(3);

            // 5. The shaders: every material of the match, drawn once where nobody sees it.
            await Begin(4);
            await ShaderWarmup.RunAsync(this, match);
            await End(4);

            // 6. The starting state: the camera centres on the start at the end of the frame (SimulationHost.LookAtStart).
            await Begin(5);
            await NextFrame();
            await End(5);
            if (hold)
            {
                return;
            }

            host.Paused = false;
            GetTree().CurrentScene = match;
            GD.Print("[loading] the match starts");
            // A network match: loaded — tick 0 comes for everyone once the last one is too (M9 step 9). The tick of step 4 is the same
            // first tick without commands on every computer, computed before anyone's lockstep starts.
            Net.NetGame.Current?.ReportLoaded(100);
            QueueFree();
        }
        catch (Exception error)
        {
            GD.PushError($"[loading] {error}");
            GetTree().Quit(1);
        }
    }

    /// <summary>The scene the router asked for: a match scene, <see cref="SceneRouter.MatchScene"/> when it did not say.</summary>
    private static string NextScene()
    {
        var args = SceneRouter.Instance?.Args;
        return args is not null && args.TryGetValue(SceneRouter.NextKey, out var next) && next is string scene
            ? scene
            : SceneRouter.MatchScene;
    }

    /// <summary>Shows the label of a stage, one frame before its work, so the screen shows what is loading.</summary>
    private async Task Begin(int stage)
    {
        _stage.Text = Tr(LoadingPlan.Stages[stage].LabelKey);
        await NextFrame();
    }

    /// <summary>Moves the bar to the end of a stage and lets a frame show it.</summary>
    private async Task End(int stage)
    {
        var percent = LoadingPlan.Stages[stage].Percent;
        _bar.Value = percent;
        // Everyone of a network match sees how far each has loaded; 100 only once all is done.
        Net.NetGame.Current?.ReportLoaded(Math.Min(99, (int)percent));
        _percent.Text = string.Format(Tr(PercentKey), percent);
        await NextFrame();
    }

    private async Task NextFrame()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
}
