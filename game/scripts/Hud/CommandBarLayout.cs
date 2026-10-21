namespace Rts.Client.Hud;

/// <summary>The buttons of the command bar, left to right: the order of the RA2 bar (docs/08_ui_ux.md, section 5).</summary>
public enum CommandBarButton
{
    /// <summary>I: the units of the type of the selection on the field.</summary>
    TypeOnScreen,

    /// <summary>II: the units of the type of the selection on the whole map, at most one command's worth.</summary>
    TypeOnMap,

    /// <summary>The dotted icon: every combat unit on the field. Waits for M4, when units have weapons.</summary>
    SelectCombat,

    /// <summary>X: the selected units scatter.</summary>
    Scatter,

    /// <summary>The shield: guard. Waits for M4, the guard stance.</summary>
    Guard,

    /// <summary>S: the selected units stop.</summary>
    Stop,

    /// <summary>The flag: waypoints mode. The next orders lay out a route, and the order after the points sends it.</summary>
    Waypoints,

    /// <summary>The bug: a placeholder. The game has no report to send yet.</summary>
    Bug,
}

/// <summary>Size, rules and camera offset of the command bar at the bottom of the field, in design pixels. Pure, tested in Rts.Simulation.Tests.</summary>
public static class CommandBarLayout
{
    /// <summary>Height of the bar at the bottom of the field. The bar is as wide as the field, left of the sidebar.</summary>
    public const float Height = 72f;

    /// <summary>Whether a button can be pressed now. The unit orders need a selection; the buttons that wait for a milestone stay greyed.</summary>
    public static bool Enabled(CommandBarButton button, bool hasSelection) => button switch
    {
        CommandBarButton.TypeOnScreen or CommandBarButton.TypeOnMap or CommandBarButton.Scatter or CommandBarButton.Stop
            or CommandBarButton.Waypoints => hasSelection,
        _ => false,
    };

    /// <summary>
    /// The camera offset in world units that puts the rig point in the middle of the field. The strip on the right and the bar at the
    /// bottom move the field's middle, so the camera moves right by half the strip and down by half the bar. The camera keeps the view
    /// height, so one design pixel is <paramref name="size"/> / <paramref name="viewHeight"/> world units.
    /// </summary>
    public static (float X, float Y) CentreOffset(float reservedRight, float reservedBottom, float size, float viewHeight) =>
        (reservedRight / 2f * size / viewHeight, -reservedBottom / 2f * size / viewHeight);

    /// <summary>The height of the field: the view without the bar.</summary>
    public static float FieldHeight(float viewHeight) => viewHeight - Height;
}
