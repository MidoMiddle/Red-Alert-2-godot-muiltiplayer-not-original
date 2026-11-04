using Godot;

namespace Rts.Client;

/// <summary>
/// The crosshair of the player inside a unit (Possession): four strokes around the middle of the screen, as far apart as the shot may
/// stray (<see cref="Gap"/>), and under them the reload of the weapon as a bar that fills (<see cref="Reload"/>).
/// </summary>
public partial class Crosshair : Control
{
    private const float Arm = 9f;
    private const float Width = 2f;
    private const float BarWidth = 44f;
    private static readonly Color Ink = new(1f, 1f, 1f, 0.9f);
    private static readonly Color Shade = new(0f, 0f, 0f, 0.45f);

    /// <summary>Pixels from the middle to the inner end of each stroke.</summary>
    public float Gap { get; set; } = 6f;

    /// <summary>The part of the reload still to go: 0 loaded (no bar), 1 just fired.</summary>
    public float Reload { get; set; }

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Draw()
    {
        // The middle of the screen: a control on a layer of its own may be given no size.
        var middle = GetViewportRect().Size / 2f;
        foreach (var along in new[] { Vector2.Right, Vector2.Left, Vector2.Down, Vector2.Up })
        {
            DrawLine(middle + (along * Gap), middle + (along * (Gap + Arm)), Ink, Width);
        }

        DrawRect(new Rect2(middle - new Vector2(1f, 1f), new Vector2(2f, 2f)), Ink);
        if (Reload > 0f)
        {
            var at = middle + new Vector2(-BarWidth / 2f, Gap + Arm + 8f);
            DrawRect(new Rect2(at, new Vector2(BarWidth, 4f)), Shade);
            DrawRect(new Rect2(at, new Vector2(BarWidth * (1f - Reload), 4f)), Ink);
        }
    }
}
