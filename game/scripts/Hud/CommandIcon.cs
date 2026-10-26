using Godot;

namespace Rts.Client.Hud;

/// <summary>
/// One button of the command bar: an icon drawn from our own shapes (no RA2 art), greyed when it cannot be pressed and highlighted
/// while its mode is on. A press on an enabled button emits <see cref="ActivatedEventHandler"/> (docs/08_ui_ux.md, section 5).
/// </summary>
public partial class CommandIcon : Control
{
    private static readonly Color BackgroundColor = new("#1B1D22");
    private static readonly Color FrameColor = new("#6E747D");
    private static readonly Color LitColor = new("#DCE8FF");
    private static readonly Color DimColor = new("#5C6470");
    private static readonly Color ModeColor = new("#B3121C");

    private bool _enabled = true;
    private bool _active;

    [Signal]
    public delegate void ActivatedEventHandler();

    public CommandBarButton Kind { get; init; }

    /// <summary>Whether the button can be pressed now. A greyed button draws dim and ignores the press.</summary>
    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled != value)
            {
                _enabled = value;
                QueueRedraw();
            }
        }
    }

    /// <summary>Whether the mode of the button is on (the waypoints mode): the button is highlighted.</summary>
    public bool Active
    {
        get => _active;
        set
        {
            if (_active != value)
            {
                _active = value;
                QueueRedraw();
            }
        }
    }

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(56f, 56f);
        MouseFilter = MouseFilterEnum.Stop;
        FocusMode = FocusModeEnum.None;
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } && _enabled)
        {
            EmitSignal(SignalName.Activated);
            AcceptEvent();
        }
    }

    public override void _Draw()
    {
        using var frameTimer = Rts.Client.Debug.PerfLog.Time("CommandIcon.draw");
        var side = Size;
        var lit = _enabled ? LitColor : DimColor;
        DrawRect(new Rect2(Vector2.Zero, side), _active ? ModeColor : BackgroundColor);
        DrawRect(new Rect2(Vector2.Zero, side), FrameColor, false, 1f);

        var c = side / 2f;
        switch (Kind)
        {
            case CommandBarButton.TypeOnScreen:
                // I: one oval with a bar inside.
                DrawArc(c, 16f, 0f, Mathf.Tau, 32, lit, 2f);
                DrawLine(c + new Vector2(0f, -10f), c + new Vector2(0f, 10f), lit, 3f);
                break;
            case CommandBarButton.TypeOnMap:
                // II: two ovals with a bar each.
                DrawArc(c + new Vector2(-7f, 0f), 11f, 0f, Mathf.Tau, 32, lit, 2f);
                DrawArc(c + new Vector2(7f, 0f), 11f, 0f, Mathf.Tau, 32, lit, 2f);
                DrawLine(c + new Vector2(-7f, -8f), c + new Vector2(-7f, 8f), lit, 3f);
                DrawLine(c + new Vector2(7f, -8f), c + new Vector2(7f, 8f), lit, 3f);
                break;
            case CommandBarButton.SelectCombat:
                // A dotted square: every combat unit.
                for (var i = -1; i <= 1; i++)
                {
                    for (var j = -1; j <= 1; j++)
                    {
                        DrawCircle(c + new Vector2(i * 9f, j * 9f), 2.5f, lit);
                    }
                }

                break;
            case CommandBarButton.Scatter:
                // Four arrows going out from the middle.
                foreach (var direction in new[] { new Vector2(-1f, -1f), new Vector2(1f, -1f), new Vector2(-1f, 1f), new Vector2(1f, 1f) })
                {
                    var unit = direction.Normalized();
                    DrawLine(c + unit * 6f, c + unit * 18f, lit, 2.5f);
                    DrawCircle(c + unit * 18f, 3f, lit);
                }

                break;
            case CommandBarButton.Guard:
                // A shield.
                DrawColoredPolygon(
                    new[]
                    {
                        c + new Vector2(-13f, -15f), c + new Vector2(13f, -15f), c + new Vector2(13f, 2f),
                        c + new Vector2(0f, 17f), c + new Vector2(-13f, 2f),
                    },
                    lit);
                break;
            case CommandBarButton.Stop:
                // A circle with a slash.
                DrawArc(c, 16f, 0f, Mathf.Tau, 32, lit, 2f);
                DrawLine(c + new Vector2(-11f, 11f), c + new Vector2(11f, -11f), lit, 2.5f);
                break;
            case CommandBarButton.Waypoints:
                // A flag with its route dots.
                DrawLine(c + new Vector2(-12f, -16f), c + new Vector2(-12f, 16f), lit, 2f);
                DrawColoredPolygon(new[] { c + new Vector2(-12f, -16f), c + new Vector2(12f, -10f), c + new Vector2(-12f, -4f) }, lit);
                for (var i = 0; i < 3; i++)
                {
                    DrawCircle(c + new Vector2(-4f + i * 9f, 14f - i * 4f), 2f, lit);
                }

                break;
            case CommandBarButton.Bug:
                // A placeholder: a small bug.
                DrawCircle(c + new Vector2(0f, 3f), 9f, lit);
                DrawCircle(c + new Vector2(0f, -8f), 4f, lit);
                for (var i = -1; i <= 1; i += 2)
                {
                    DrawLine(c + new Vector2(0f, -2f), c + new Vector2(i * 14f, -8f), lit, 1.5f);
                    DrawLine(c + new Vector2(0f, 6f), c + new Vector2(i * 14f, 10f), lit, 1.5f);
                }

                break;
        }
    }
}
