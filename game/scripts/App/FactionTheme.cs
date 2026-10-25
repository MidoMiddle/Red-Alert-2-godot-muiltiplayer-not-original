using System.Collections.Generic;
using Godot;

namespace Rts.Client.App;

/// <summary>
/// The theme of a match by the side the player took (RA2: the Allies' screens are blue, the Soviets' red; the owner's wish,
/// 2026-10-25): the theme of the game (<c>res://theme/ra2_theme.tres</c>, red, the menus' outside a match) with its buttons, panels
/// and tabs in the colours of the faction — the Alliance blue buttons with yellow text on blue-steel panels, the Union as it is.
/// Given to the controls of the match: the sidebar, the command bar, the match screen, its dialogs, the results.
/// </summary>
public static class FactionTheme
{
    private const string BasePath = "res://theme/ra2_theme.tres";

    private static readonly Dictionary<Faction, Theme> Themes = new();

    public static Theme Of(Faction faction)
    {
        if (Themes.TryGetValue(faction, out var theme))
        {
            return theme;
        }

        theme = (Theme)GD.Load<Theme>(BasePath).Duplicate(true);
        if (faction == Faction.Alliance)
        {
            Recolour(theme, "Button", "normal", new Color(0.13f, 0.32f, 0.72f), new Color(0.03f, 0.09f, 0.28f));
            Recolour(theme, "Button", "hover", new Color(0.22f, 0.45f, 0.9f), null);
            Recolour(theme, "Button", "pressed", new Color(0.07f, 0.19f, 0.47f), new Color(0.03f, 0.09f, 0.28f));
            var panel = new Color(0.1f, 0.13f, 0.2f);
            var steel = new Color(0.45f, 0.56f, 0.72f);
            Recolour(theme, "Panel", "panel", panel, steel);
            Recolour(theme, "PanelContainer", "panel", panel, steel);
            Recolour(theme, "AcceptDialog", "panel", panel, steel);
            Recolour(theme, "TabContainer", "tab_selected", new Color(0.22f, 0.45f, 0.9f), new Color(0.03f, 0.09f, 0.28f));
        }

        Themes[faction] = theme;
        return theme;
    }

    // A style box of the theme in other colours; its margins, borders and corners stay. The boxes are the duplicate's own.
    private static void Recolour(Theme theme, string type, string name, Color background, Color? border)
    {
        if (theme.GetStylebox(name, type) is not StyleBoxFlat box)
        {
            return;
        }

        box = (StyleBoxFlat)box.Duplicate();
        box.BgColor = background;
        if (border is { } edge)
        {
            box.BorderColor = edge;
        }

        theme.SetStylebox(name, type, box);
    }
}
