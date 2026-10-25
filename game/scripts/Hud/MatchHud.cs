using Godot;
using Rts.Client.App;
using Rts.Client.UI;

namespace Rts.Client.Hud;

/// <summary>
/// The HUD of the match scene: the sidebar, the statistics panel and the command bar, wired to the rest of the scene. The Menu button
/// opens the pause menu, the Stats button shows the statistics, the minimap moves the camera, the command bar runs its commands on the
/// selection, and the camera keeps the strips under the sidebar and the bar out of the field. The sidebar shows the economy of the
/// local player (M3 step 12); the statistics are placeholders until M11.
/// </summary>
public partial class MatchHud : CanvasLayer
{
    /// <summary>The faction of the player: its colour is the accent of the sidebar.</summary>
    [Export]
    public Faction PlayerFaction { get; set; } = Faction.Union;

    // Before the children's _Ready, which set their texts: a match scene started on its own (the editor, Main.tscn from the command
    // line) did not go through Boot, which loads them.
    public override void _EnterTree() => Localization.Load();

    public override void _Ready()
    {
        var sidebar = GetNode<Sidebar>("Sidebar");
        var bar = GetNode<CommandBar>("CommandBar");
        var camera = GetNode<RtsCamera>("../CameraRig");
        var player = GetNode<PlayerInput>("../PlayerInput");
        var host = GetNode<SimulationHost>("../SimulationHost");

        camera.ReservedRight = SidebarLayout.Width;
        camera.ReservedBottom = CommandBarLayout.Height;
        sidebar.Minimap.Bind(host, camera);
        var pause = GetNode<PauseMenu>("../PauseMenu");
        pause.Bind(host);
        GetNode<Rts.Client.UI.MatchResults>("../MatchResults").Bind(host);
        // The match screen of RA2 (PauseMenu): the menu by the Menu button and Esc, the statistics by the Stats button and Tab.
        sidebar.MenuRequested += pause.Open;
        sidebar.StatsRequested += pause.ToggleStats;

        // The mouse stays in the window while the player plays; the menu, the statistics and the results let it go.
        var results = GetNode<Rts.Client.UI.MatchResults>("../MatchResults");
        AddChild(new Rts.Client.Controls.MouseConfinement { Name = "MouseConfinement", Released = () => pause.IsOpen || results.IsOpen });
        // The faction the local player plays in the match: its colour is the accent (the Union outside a match).
        if (host.Simulation.World.Match?.PlayerOf(SimulationHost.LocalPlayer) is { } local)
        {
            PlayerFaction = FactionPalette.Of(local.Faction);
        }
        else
        {
            // A session without a match setup (the sandbox): the faction of the player in the world.
            PlayerFaction = FactionPalette.Of(host.Simulation.World.Player(SimulationHost.LocalPlayer).Faction.Id);
        }

        // The screens of the match in the colours of the side taken: blue for the Alliance, red for the Union (RA2).
        var theme = FactionTheme.Of(PlayerFaction);
        foreach (var child in GetChildren())
        {
            if (child is Control control)
            {
                control.Theme = theme;
            }
        }

        pause.Theme = theme;
        results.Theme = theme;
        sidebar.Setup(PlayerFaction, host.Simulation.World.Player(SimulationHost.LocalPlayer).Credits);
        sidebar.Bind(host, player);
        var notices = new Notifications { Name = "Notifications" };
        AddChild(notices);
        notices.Bind(host);
        // The shutters of the radar move: EVA says so, in a match where the radar counts (M6 step 11).
        sidebar.Minimap.OnlineChanged += online => notices.Say(online ? "UI_EVA_RADAR_ONLINE" : "UI_EVA_RADAR_OFFLINE");
        // Watching a replay (M6 step 15): its controls over the field.
        var replayBar = new ReplayBar { Name = "ReplayBar", Theme = theme };
        AddChild(replayBar);
        replayBar.Bind(host);
        var banner = new MatchBanner { Name = "MatchBanner" };
        AddChild(banner);
        banner.Bind(host);
        bar.Bind(player);
        bar.Requested += button => player.RunCommand((CommandBarButton)button);
    }
}
