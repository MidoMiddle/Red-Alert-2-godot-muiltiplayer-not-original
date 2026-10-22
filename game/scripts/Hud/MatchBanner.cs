using Godot;
using Rts.Client.Terrain;
using Rts.Simulation.Events;

namespace Rts.Client.Hud;

/// <summary>
/// The end of the match for the person at the keyboard (docs/implementation/M5_first_bot.md, 4.8, step 10): «DEFEAT» in the middle of
/// the field once they lose — the fog lifts then, they watch the rest of the match — and «VICTORY» when the match ends with them among
/// the winners. Until the results screen of step 12.
/// </summary>
public partial class MatchBanner : Label
{
    private SimulationHost? _host;

    /// <summary>Listens to the events of the match.</summary>
    public void Bind(SimulationHost host)
    {
        _host = host;
        host.EventRaised += OnEvent;
    }

    /// <summary>The key the banner shows for an event, or null when it changes nothing.</summary>
    public static string? KeyOf(SimEvent simEvent, Rts.Simulation.World.PlayerId player) => simEvent switch
    {
        PlayerDefeated defeated when defeated.Player == player => "UI_MATCH_DEFEAT",
        MatchEnded ended => ended.Winners.Contains(player) ? "UI_MATCH_VICTORY" : "UI_MATCH_DEFEAT",
        _ => null,
    };

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        SetAnchorsPreset(LayoutPreset.FullRect);
        OffsetRight = -SidebarLayout.Width;
        OffsetBottom = -CommandBarLayout.Height;
        AddThemeFontSizeOverride("font_size", 72);
        AddThemeColorOverride("font_color", new Color("#F4C843"));
        AddThemeColorOverride("font_outline_color", Colors.Black);
        AddThemeConstantOverride("outline_size", 12);
    }

    public override void _ExitTree()
    {
        if (_host is not null)
        {
            _host.EventRaised -= OnEvent;
        }
    }

    private void OnEvent(SimEvent simEvent)
    {
        if (KeyOf(simEvent, SimulationHost.LocalPlayer) is not { } key)
        {
            return;
        }

        if (simEvent is PlayerDefeated)
        {
            FogOfWar.Current?.Reveal();
        }

        Text = Tr(key);
        Visible = true;
    }
}
