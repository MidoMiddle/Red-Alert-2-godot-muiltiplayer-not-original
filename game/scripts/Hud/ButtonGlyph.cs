using System;
using Godot;

namespace Rts.Client.Hud;

/// <summary>The pictures of the sidebar's buttons (the owner, 2026-10-30): drawn in code, no files.</summary>
public enum Glyph
{
    Wrench,
    Dollar,
    House,
    Shield,
    Person,
    Tank,
    Menu,
    Stats,
}

/// <summary>
/// A picture in place of a button's text (the owner, 2026-10-30): the wrench of repair, the dollar of selling, a house, a shield, a
/// soldier and a tank on the four tabs, as in RA2; three bars for the menu and a bar chart for the statistics, of our own. Laid over the
/// whole button, it lets the clicks through and is drawn in the button's text colour, dimmed while the button is disabled — or, on a tab,
/// gone: a tab shut for want of a factory is an empty square (the owner, 2026-10-30); the button's name goes to its tooltip.
/// </summary>
public partial class ButtonGlyph : Control
{
    private static readonly Color Light = new("#EAE6D8");
    private static readonly Color Dim = new(0.55f, 0.56f, 0.58f, 0.6f);

    /// <summary>The height of a button with a picture: that of the sidebar's buttons with a line of text.</summary>
    public const float Height = 31f;

    private readonly Glyph _glyph;
    private readonly bool _hideWhenDisabled;
    private bool _dim;

    public ButtonGlyph(Glyph glyph, bool hideWhenDisabled = false)
    {
        _glyph = glyph;
        _hideWhenDisabled = hideWhenDisabled;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    /// <summary>
    /// Puts <paramref name="glyph"/> on <paramref name="button"/>: its text becomes its tooltip. With
    /// <paramref name="hideWhenDisabled"/> the picture is not drawn while the button is disabled.
    /// </summary>
    public static void Put(Button button, Glyph glyph, string name, bool hideWhenDisabled = false)
    {
        button.TooltipText = Tooltip(name, button.TooltipText);
        button.Text = string.Empty;
        // As tall as with its words.
        button.CustomMinimumSize = new Vector2(button.CustomMinimumSize.X, Math.Max(button.CustomMinimumSize.Y, Height));
        var picture = new ButtonGlyph(glyph, hideWhenDisabled);
        button.AddChild(picture);
        picture.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }

    // The name of the button, then what its tooltip said.
    private static string Tooltip(string name, string hint) => hint.Length > 0 ? name + "\n" + hint : name;

    public override void _Process(double delta)
    {
        var dim = GetParent() is BaseButton { Disabled: true };
        if (dim != _dim)
        {
            _dim = dim;
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        if (_dim && _hideWhenDisabled)
        {
            return;
        }

        var colour = _dim ? Dim : Light;
        // The picture stands in a square of 70% of the button's height, in its middle.
        var side = Math.Min(Size.X, Size.Y) * 0.7f;
        var origin = (Size - new Vector2(side, side)) / 2f;
        Vector2 At(float x, float y) => origin + new Vector2(x, y) * side;
        void Fill(params Vector2[] points) => DrawColoredPolygon(points, colour);

        switch (_glyph)
        {
            case Glyph.Wrench:
                // A handle from the lower left, a ring with its jaw open to the upper right.
                DrawLine(At(0.12f, 0.88f), At(0.58f, 0.42f), colour, side * 0.16f);
                DrawArc(At(0.68f, 0.32f), side * 0.2f, -0.2f, 4.9f, 16, colour, side * 0.12f);
                break;
            case Glyph.Dollar:
                var font = GetThemeFont("font", "Label");
                var size = (int)(side * 1.15f);
                DrawString(font, new Vector2(0f, origin.Y + side * 0.92f), "$", HorizontalAlignment.Center, Size.X, size, colour);
                break;
            case Glyph.House:
                Fill(At(0.5f, 0.05f), At(0.95f, 0.45f), At(0.05f, 0.45f));
                Fill(At(0.18f, 0.45f), At(0.82f, 0.45f), At(0.82f, 0.95f), At(0.18f, 0.95f));
                break;
            case Glyph.Shield:
                Fill(At(0.5f, 0.02f), At(0.92f, 0.16f), At(0.88f, 0.55f), At(0.5f, 0.98f), At(0.12f, 0.55f), At(0.08f, 0.16f));
                break;
            case Glyph.Person:
                DrawCircle(At(0.5f, 0.15f), side * 0.14f, colour);
                Fill(At(0.28f, 0.34f), At(0.72f, 0.34f), At(0.68f, 0.62f), At(0.62f, 0.62f), At(0.62f, 0.98f), At(0.38f, 0.98f),
                    At(0.38f, 0.62f), At(0.32f, 0.62f));
                break;
            case Glyph.Tank:
                // Tracks, the hull, the turret and its barrel, from the side.
                Fill(At(0.02f, 0.62f), At(0.98f, 0.62f), At(0.88f, 0.9f), At(0.12f, 0.9f));
                Fill(At(0.3f, 0.36f), At(0.66f, 0.36f), At(0.7f, 0.58f), At(0.26f, 0.58f));
                DrawLine(At(0.62f, 0.45f), At(1.0f, 0.4f), colour, side * 0.09f);
                break;
            case Glyph.Menu:
                foreach (var y in new[] { 0.2f, 0.5f, 0.8f })
                {
                    Fill(At(0.1f, y - 0.07f), At(0.9f, y - 0.07f), At(0.9f, y + 0.07f), At(0.1f, y + 0.07f));
                }

                break;
            case Glyph.Stats:
                foreach (var (x, top) in new[] { (0.1f, 0.55f), (0.4f, 0.15f), (0.7f, 0.35f) })
                {
                    Fill(At(x, top), At(x + 0.2f, top), At(x + 0.2f, 0.95f), At(x, 0.95f));
                }

                break;
        }
    }
}
