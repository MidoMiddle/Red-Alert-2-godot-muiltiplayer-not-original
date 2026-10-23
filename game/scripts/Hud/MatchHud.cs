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
        var stats = GetNode<StatsPanel>("StatsPanel");
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
        sidebar.MenuRequested += pause.Open;
        // Built: the buildings of the match statistics (M5 step 11), as the panel opens; captured comes with M11.
        sidebar.StatsRequested += () =>
        {
            stats.Setup(host.Simulation.World.Stats.Of(SimulationHost.LocalPlayer).BuildingsBuilt, 0);
            stats.Toggle();
        };
        sidebar.Setup(PlayerFaction, host.Simulation.World.Player(SimulationHost.LocalPlayer).Credits);
        sidebar.Bind(host, player);
        var notices = new Notifications { Name = "Notifications" };
        AddChild(notices);
        notices.Bind(host);
        var banner = new MatchBanner { Name = "MatchBanner" };
        AddChild(banner);
        banner.Bind(host);
        stats.Setup(0, 0);
        bar.Bind(player);
        bar.Requested += button => player.RunCommand((CommandBarButton)button);
    }
}
