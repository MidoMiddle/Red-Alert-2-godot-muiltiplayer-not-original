using Godot;
using Rts.Client.App;

namespace Rts.Client.UI;

/// <summary>
/// Pause menu of the game, opened by the Menu button in the sidebar or by Esc (docs/menu/README.md, 3.4;
/// docs/implementation/M6_menus_skirmish.md, 4.15). It stops the game tree, so the simulation, the camera and the player input
/// stop with it. Quit to Main Menu asks first and leaves the match unsaved. Resume and Esc close the menu. In a match the player still
/// in it may surrender (M5 step 10, decision 9.4): asked first, it gives the <c>SurrenderCommand</c> and closes the menu.
/// </summary>
public partial class PauseMenu : CanvasLayer
{
    private const string MainMenuScene = "res://scenes/ui/MainMenu.tscn";

    private Control _overlay = null!;
    private Button _resume = null!;
    private Button _surrender = null!;
    private ConfirmationDialog _leaveDialog = null!;
    private ConfirmationDialog _surrenderDialog = null!;
    private SimulationHost? _host;
    private bool _open;

    public override void _Ready()
    {
        // The menu has to keep working while the game tree is paused.
        ProcessMode = ProcessModeEnum.Always;

        _overlay = GetNode<Control>("Overlay");
        _resume = GetNode<Button>("Overlay/Panel/Column/Resume");
        var quit = GetNode<Button>("Overlay/Panel/Column/QuitToMenu");
        _surrender = GetNode<Button>("Overlay/Panel/Column/Surrender");

        GetNode<Label>("Overlay/Panel/Column/Title").Text = Tr("UI_PAUSE_TITLE");
        _resume.Text = Tr("UI_PAUSE_RESUME");
        quit.Text = Tr("UI_PAUSE_QUIT");
        _surrender.Text = Tr("UI_PAUSE_SURRENDER");

        _resume.Pressed += Close;
        quit.Pressed += () => _leaveDialog.PopupCentered();

        _leaveDialog = new ConfirmationDialog
        {
            Title = Tr("UI_PAUSE_QUIT"),
            DialogText = Tr("UI_PAUSE_LEAVE_TEXT"),
        };
        _leaveDialog.Confirmed += LeaveToMainMenu;
        AddChild(_leaveDialog);

        _surrenderDialog = new ConfirmationDialog
        {
            Title = Tr("UI_PAUSE_SURRENDER"),
            DialogText = Tr("UI_PAUSE_SURRENDER_TEXT"),
        };
        _surrenderDialog.Confirmed += Surrender;
        AddChild(_surrenderDialog);
        _surrender.Pressed += () => _surrenderDialog.PopupCentered();
    }

    /// <summary>The match the menu gives up: without one there is nothing to surrender.</summary>
    public void Bind(SimulationHost host) => _host = host;

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        // While a dialog is up, Escape belongs to it.
        if (_open && !_leaveDialog.Visible && !_surrenderDialog.Visible && inputEvent.IsActionPressed("ui_cancel"))
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>Shows the pause panel and stops the game.</summary>
    public void Open()
    {
        if (_open)
        {
            return;
        }

        _open = true;
        _surrender.Visible = _host?.CanSurrender == true;
        _overlay.Show();
        GetTree().Paused = true;
        _resume.GrabFocus();
    }

    private void Close()
    {
        _open = false;
        _overlay.Hide();
        GetTree().Paused = false;
    }

    private void Surrender()
    {
        _host?.Surrender();
        Close();
    }

    private void LeaveToMainMenu()
    {
        _open = false;
        LeaveMatch(GetTree());
    }

    /// <summary>Leaves the match unsaved: to the main menu, or back to the map editor that started it. The results screen leaves so too.</summary>
    public static void LeaveMatch(SceneTree tree)
    {
        // Unpause first: the router's fade runs on a tween, and a paused tree would hold it.
        tree.Paused = false;

        // A match started by «Play» in the map editor goes back to the editor (M7 step 12).
        if (SceneRouter.Instance?.Args.ContainsKey(Rts.Client.Editor.MapEditorScene.FromEditorArgument) == true)
        {
            SceneRouter.Instance.Back();
            return;
        }

        SceneRouter.Instance?.Replace(MainMenuScene);
    }
}
