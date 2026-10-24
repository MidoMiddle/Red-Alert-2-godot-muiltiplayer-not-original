using System;
using System.Collections.Generic;
using Godot;

namespace Rts.Client.Hud;

/// <summary>
/// One square of the item grid (docs/implementation/M3_base_economy.md, 4.17): until the models are rendered into icons (decision 6),
/// a plate with the name, a big short code in the colour of its role (<see cref="CameoRole"/>) and a band of that colour on the left,
/// and the cost, in the faction's frame. Over it the clock darkens what is left to build, the number in the
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
        var role = RoleColor(cameo.Role);
        DrawRect(new Rect2(0f, 0f, 5f, Size.Y), role);
        DrawString(font, new Vector2(5f, 15f), cameo.Name, HorizontalAlignment.Center, Size.X - 5f, NameSize(font, cameo.Name), NameColor);
        DrawString(font, new Vector2(5f, 39f), cameo.Code, HorizontalAlignment.Center, Size.X - 5f, 22, role.Lightened(0.25f));
        DrawString(font, new Vector2(9f, Size.Y - 5f), HudFormat.Credits(cameo.Cost), HorizontalAlignment.Left, Size.X - 14f, 12, CostColor);

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

    /// <summary>The colour of a role: green infantry, blue vehicles, teal ships, purple aircraft; for buildings yellow power, orange
    /// production, red defence, magenta superweapons, grey walls, light blue the rest.</summary>
    private static Color RoleColor(CameoRole role) => role switch
    {
        CameoRole.Infantry => new Color("#3FA34D"),
        CameoRole.Vehicle => new Color("#3B7DD8"),
        CameoRole.Naval => new Color("#2BB3B3"),
        CameoRole.Aircraft => new Color("#9B59D0"),
        CameoRole.Power => new Color("#E0B020"),
        CameoRole.Production => new Color("#E07A1F"),
        CameoRole.Defense => new Color("#C8352E"),
        CameoRole.Superweapon => new Color("#D63AAF"),
        CameoRole.Wall => new Color("#7A7F87"),
        _ => new Color("#4FA3C7"),
    };

    /// <summary>The font size at which the name fits the square: 12, smaller for a long name, 9 at least.</summary>
    private int NameSize(Font font, string name)
    {
        var size = 12;
        while (size > 9 && font.GetStringSize(name, HorizontalAlignment.Left, -1f, size).X > Size.X - 10f)
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
        if (cameo.AvailableLater)
        {
            return text + "\n" + Tr("UI_SIDEBAR_AVAILABLE_LATER");
        }

        if (cameo.AbilityLater)
        {
            text += "\n" + Tr(cameo.Kind == Rts.Simulation.World.ProductionKind.Unit ? "UI_SIDEBAR_WEAPON_LATER" : "UI_SIDEBAR_ABILITY_LATER");
        }

        return cameo.Missing.Length == 0 ? text : text + "\n" + string.Format(Tr("UI_SIDEBAR_REQUIRES"), cameo.Missing);
    }
}
