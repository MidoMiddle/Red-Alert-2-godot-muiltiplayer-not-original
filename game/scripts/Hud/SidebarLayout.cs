namespace Rts.Client.Hud;

/// <summary>
/// Sizes of the sidebar in design pixels, the 1920×1080 base (docs/08_ui_ux.md, section 4; docs/implementation/M6_menus_skirmish.md, 4.7).
/// </summary>
public static class SidebarLayout
{
    /// <summary>Width of the sidebar on the right side of the view. The field is what is left of it.</summary>
    public const float Width = 230f;

    /// <summary>Side of the square minimap.</summary>
    public const float MinimapSize = 180f;

    /// <summary>Item squares: two in a row, ten rows on a 1080 px base.</summary>
    public const int Columns = 2;

    public const int Rows = 10;
    public const int Slots = Columns * Rows;

    /// <summary>Width of the field for a view of the given width: the view minus the sidebar.</summary>
    public static float FieldWidth(float viewportWidth) => viewportWidth - Width;
}

/// <summary>The build groups, shown as tabs in the order of the docs.</summary>
public enum BuildGroup
{
    Buildings,
    Defense,
    Infantry,
    Vehicles,
}

public static class BuildGroups
{
    /// <summary>Localization key of the tab text.</summary>
    public static string LocalizationKey(BuildGroup group) => group switch
    {
        BuildGroup.Buildings => "UI_TAB_BUILDINGS",
        BuildGroup.Defense => "UI_TAB_DEFENSE",
        BuildGroup.Infantry => "UI_TAB_INFANTRY",
        _ => "UI_TAB_VEHICLES",
    };
}
