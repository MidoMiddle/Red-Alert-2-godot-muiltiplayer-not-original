using System;
using System.Collections.Generic;
using Godot;

namespace Rts.Client.Hud;

/// <summary>
/// One square of the item grid (docs/implementation/M3_base_economy.md, 4.17): until the models are rendered into icons (decision 6),
/// a plate with the name and the cost in the faction's frame. Over it the clock darkens what is left to build, the number in the
/// queue sits in the corner, and Ready or On Hold is written across. Unavailable and busy squares are greyed out; the tooltip tells
/// the name, the cost and what is missing. An empty square only shows its frame.
/// </summary>
public partial class BuildIcon : Control
{
    public const float IconWidth = 98f;
    public const float IconHeight = 58f;

    private static readonly Color Plate = new("#1E2126");
    private static readonly Color EmptyFrame = new("#3A3E45");
    private static readonly Color Shade = new(0f, 0f, 0f, 0.55f);
    private static readonly Color Greyed = new(0.08f, 0.09f, 0.1f, 0.65f);
    private static readonly Color NameColor = new("#EAE6D8");
    private static readonly Color CostColor = new("#B9B4A6");
    private static readonly Color StateColor = new("#F4C843");

    private Cameo? _cameo;
    private Color _accent = new("#B3121C");

    /// <summary>A click on the square: right or left, and whether Shift was held.</summary>
    public event Action<Cameo, bool, bool>? Clicked;

    /// <summary>The item of the square, or null for an empty one.</summary>
    public Cameo? Cameo
    {
        get => _cameo;
        set
        {
            if (Equals(_cameo, value))
            {
                return;
            }

            _cameo = value;
            TooltipText = value is null ? string.Empty : Tooltip(value);
            QueueRedraw();
        }
    }

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(IconWidth, IconHeight);
        ClipContents = true;
        MouseFilter = MouseFilterEnum.Stop;
    }

    /// <summary>The colour of the frame: the faction accent.</summary>
    public void SetAccent(Color accent)
    {
        _accent = accent;
        QueueRedraw();
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left or MouseButton.Right } button && _cameo is { } cameo)
        {
            Clicked?.Invoke(cameo, button.ButtonIndex == MouseButton.Right, button.ShiftPressed);
            AcceptEvent();
        }
    }

    public override void _Draw()
    {
        var rect = new Rect2(Vector2.Zero, Size);
        DrawRect(rect, Plate);
        if (_cameo is not { } cameo)
        {
            DrawRect(rect, EmptyFrame, false, 1f);
            return;
        }

        var font = GetThemeFont("font", "Label");
        DrawString(font, new Vector2(0f, 22f), cameo.Name, HorizontalAlignment.Center, Size.X, NameSize(font, cameo.Name), NameColor);
        DrawString(font, new Vector2(6f, Size.Y - 7f), HudFormat.Credits(cameo.Cost), HorizontalAlignment.Left, Size.X - 12f, 13, CostColor);

        if (cameo.State is CameoState.Building or CameoState.OnHold or CameoState.WaitingForFunds)
        {
            DrawClock(cameo.Progress);
        }

        if (cameo.State is CameoState.Unavailable or CameoState.Busy)
        {
            DrawRect(rect, Greyed);
        }

        if (cameo.Count > 1)
        {
            DrawString(font, new Vector2(0f, 16f), cameo.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                HorizontalAlignment.Right, Size.X - 5f, 14, StateColor);
        }

        var label = cameo.State switch
        {
            CameoState.Ready => Tr("UI_SIDEBAR_READY"),
            CameoState.OnHold => Tr("UI_SIDEBAR_ON_HOLD"),
            _ => "",
        };
        if (label.Length > 0)
        {
            DrawString(font, new Vector2(0f, Size.Y / 2f + 12f), label, HorizontalAlignment.Center, Size.X, 17, StateColor);
        }

        DrawRect(rect, cameo.State is CameoState.Unavailable ? EmptyFrame : _accent, false, 2f);
    }

    /// <summary>The font size at which the name fits the square: 15, smaller for a long name, 10 at least.</summary>
    private int NameSize(Font font, string name)
    {
        var size = 15;
        while (size > 10 && font.GetStringSize(name, HorizontalAlignment.Left, -1f, size).X > Size.X - 6f)
        {
            size--;
        }

        return size;
    }

    /// <summary>The clock of RA2: what is left to build is darkened, clockwise from the top.</summary>
    private void DrawClock(float progress)
    {
        if (progress >= 1f)
        {
            return;
        }

        var centre = Size / 2f;
        var radius = Size.Length();
        var start = -Mathf.Pi / 2f + progress * Mathf.Tau;
        var end = Mathf.Pi * 1.5f;
        var points = new List<Vector2> { centre };
        const int Steps = 48;
        var steps = Math.Max(2, (int)(Steps * (1f - progress)) + 1);
        for (var i = 0; i <= steps; i++)
        {
            var angle = start + (end - start) * i / steps;
            points.Add(centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
        }

        DrawColoredPolygon(points.ToArray(), Shade);
    }

    private string Tooltip(Cameo cameo)
    {
        var text = cameo.Name + "\n" + string.Format(Tr("UI_SIDEBAR_COST"), HudFormat.Credits(cameo.Cost));
        return cameo.Missing.Length == 0 ? text : text + "\n" + string.Format(Tr("UI_SIDEBAR_REQUIRES"), cameo.Missing);
    }
}
