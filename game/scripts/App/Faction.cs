namespace Rts.Client.App;

/// <summary>The sides a player can take: Union and Alliance; Cult with M11d (docs/menu/README.md, section 6).</summary>
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
    /// <summary>The side of a faction id of the data: <c>alliance</c> the Alliance, <c>cult</c> the Cult, anything else the Union.</summary>
    public static Faction Of(string id) => id switch
    {
        "alliance" => Faction.Alliance,
        "cult" => Faction.Cult,
        _ => Faction.Union,
    };

    public static string AccentHex(Faction faction) => faction switch
    {
        Faction.Union => "#B3121C",
        Faction.Alliance => "#2A62C8",
        _ => "#7D838C",
    };
}
