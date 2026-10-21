namespace Rts.Client.App;

/// <summary>The sides a player can take. Only Union can be played before M11 (docs/menu/README.md, section 6).</summary>
public enum Faction
{
    Union,
    Alliance,
    Cult,
}

/// <summary>
/// Accent colour of each faction as a hex string: the sidebar and the minimap frame use it (docs/08_ui_ux.md, section 4).
/// Union is red, the accent of the menus; Alliance is blue. Cult has no colour yet, so it is grey until M11.
/// </summary>
public static class FactionPalette
{
    public static string AccentHex(Faction faction) => faction switch
    {
        Faction.Union => "#B3121C",
        Faction.Alliance => "#2A62C8",
        _ => "#7D838C",
    };
}
