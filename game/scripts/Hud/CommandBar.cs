using System;
using System.Collections.Generic;
using Godot;
using Rts.Client.App;
using Rts.Client.Controls;

namespace Rts.Client.Hud;

/// <summary>
/// The command bar at the bottom of the field (docs/08_ui_ux.md, section 5; the RA2 bar): the buttons for the selected units, in the
/// order of the RA2 bar. A press asks the player to run the command, the same one its key runs. Buttons that need a selection are
/// greyed without one; the ones that wait for a milestone stay greyed, with "Coming soon". The hints name the keys from the settings.
/// </summary>
public partial class CommandBar : PanelContainer
{
    [Signal]
    public delegate void RequestedEventHandler(int button);

    private readonly List<CommandIcon> _icons = new();
    private PlayerInput? _player;

    public override void _Ready()
    {
        var row = GetNode<HBoxContainer>("Row");
        foreach (var kind in Enum.GetValues<CommandBarButton>())
        {
            var icon = new CommandIcon { Kind = kind, TooltipText = HintFor(kind) };
            icon.Activated += () => EmitSignal(SignalName.Requested, (int)kind);
            _icons.Add(icon);
            row.AddChild(icon);
        }
    }

    /// <summary>Connects the bar to the player: the selection greys the orders, and the waypoints button shows its mode.</summary>
    public void Bind(PlayerInput player) => _player = player;

    public override void _Process(double delta)
    {
        var hasSelection = _player is { Selection.IsEmpty: false };
        foreach (var icon in _icons)
        {
            icon.Enabled = CommandBarLayout.Enabled(icon.Kind, hasSelection);
            icon.Active = icon.Kind == CommandBarButton.Waypoints && _player is { WaypointArmed: true };
        }
    }

    /// <summary>The tooltip of a button: its name, its key from the settings, and "Coming soon" when it waits for a milestone.</summary>
    private string HintFor(CommandBarButton kind)
    {
        var keys = AppSettings.Instance?.Keys ?? new KeyBindings();
        var (titleKey, actionId) = kind switch
        {
            CommandBarButton.TypeOnScreen => ("UI_CMD_TYPE_ON_SCREEN", "type_select"),
            CommandBarButton.TypeOnMap => ("UI_CMD_TYPE_ON_MAP", "type_select"),
            CommandBarButton.SelectCombat => ("UI_CMD_SELECT_COMBAT", string.Empty),
            CommandBarButton.Scatter => ("UI_CMD_SCATTER", "scatter"),
            CommandBarButton.Guard => ("UI_CMD_GUARD", "guard"),
            CommandBarButton.Stop => ("UI_CMD_STOP", "stop"),
            CommandBarButton.Waypoints => ("UI_CMD_WAYPOINTS", "waypoint_mode"),
            _ => ("UI_CMD_BUG", string.Empty),
        };

        var hint = Tr(titleKey);
        if (actionId.Length > 0 && keys.KeyOf(actionId).Length > 0)
        {
            hint += $" ({keys.KeyOf(actionId)})";
        }

        return CommandBarLayout.Enabled(kind, hasSelection: true) ? hint : hint + "\n" + Tr("UI_COMING_SOON");
    }
}
