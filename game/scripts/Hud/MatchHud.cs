using Godot;
using Rts.Client.App;
using Rts.Client.UI;

namespace Rts.Client.Hud;

/// <summary>
/// The HUD of the match scene: the sidebar and the statistics panel, wired to the rest of the scene. The Menu button opens the
/// pause menu, the Stats button shows the statistics, the minimap moves the camera, and the camera keeps the strip under the sidebar
/// out of the field. The money and the statistics are placeholders until M3 and M11.
/// </summary>
public partial class MatchHud : CanvasLayer
{
    /// <summary>The faction of the player: its colour is the accent of the sidebar.</summary>
    [Export]
    public Faction PlayerFaction { get; set; } = Faction.Union;

    /// <summary>The money shown until the economy exists (M3).</summary>
    [Export]
    public int Credits { get; set; } = 10000;

    public override void _Ready()
    {
        var sidebar = GetNode<Sidebar>("Sidebar");
        var stats = GetNode<StatsPanel>("StatsPanel");
        var camera = GetNode<RtsCamera>("../CameraRig");

        camera.ReservedRight = SidebarLayout.Width;
        sidebar.Minimap.Bind(GetNode<SimulationHost>("../SimulationHost"), camera);
        sidebar.MenuRequested += GetNode<PauseMenu>("../PauseMenu").Open;
        sidebar.StatsRequested += stats.Toggle;
        sidebar.Setup(PlayerFaction, Credits);
        stats.Setup(0, 0);
    }
}
