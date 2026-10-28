using Godot;

namespace Rts.Client.Hud;

/// <summary>
/// The power strip along the left edge of the sidebar (docs/implementation/M3_base_economy.md, 4.17; M6_menus_skirmish.md, 4.7): the
/// output fills it from the bottom, a white mark shows the drain. Green while there is enough, yellow close to the limit, red when
/// the drain is more than the output; then the shortage between the output and the drain is shown in dim red.
/// </summary>
public partial class PowerBar : Control
{
    public const float BarWidth = 10f;

    private static readonly Color Empty = new("#24272D");
    private static readonly Color Shortage = new(0.82f, 0.2f, 0.17f, 0.35f);
    private static readonly Color Mark = new("#EAE6D8");

    private PowerGauge _gauge = PowerGauge.Of(0, 0, false);
    private (int Output, int Drain) _shown = (-1, -1);

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(BarWidth, 0f);
        MouseFilter = MouseFilterEnum.Stop;
        Show(0, 0, false);
    }

    /// <summary>
    /// Shows the power of the player; held under the mouse, the tooltip gives the numbers — what the power plants make, what the base
    /// uses, and what is spare or short (the owner, 2026-10-28).
    /// </summary>
    public void Show(int output, int drain, bool low)
    {
        if ((output, drain) != _shown)
        {
            _shown = (output, drain);
            TooltipText = string.Format(Tr("UI_SIDEBAR_POWER"), output, drain) + "\n" + (output >= drain
                ? string.Format(Tr("UI_SIDEBAR_POWER_SPARE"), output - drain)
                : string.Format(Tr("UI_SIDEBAR_POWER_SHORT"), drain - output));
        }

        var gauge = PowerGauge.Of(output, drain, low);
        if (gauge == _gauge)
        {
            return;
        }

        _gauge = gauge;
        QueueRedraw();
    }

    public override void _Draw()
    {
        using var frameTimer = Rts.Client.Debug.PerfLog.Time("PowerBar.draw");
        var height = Size.Y;
        DrawRect(new Rect2(Vector2.Zero, Size), Empty);
        var filled = height * Mathf.Min(1f, _gauge.Output);
        DrawRect(new Rect2(0f, height - filled, Size.X, filled), ColorOf(_gauge.Level));
        if (_gauge.Level == PowerLevel.Low)
        {
            var drained = height * Mathf.Min(1f, _gauge.Drain);
            DrawRect(new Rect2(0f, height - drained, Size.X, drained - filled), Shortage);
        }

        var drainAt = height - height * Mathf.Min(1f, _gauge.Drain);
        DrawRect(new Rect2(-2f, drainAt - 1.5f, Size.X + 4f, 3f), Mark);
    }

    private static Color ColorOf(PowerLevel level) => level switch
    {
        PowerLevel.Low => new Color("#D2342B"),
        PowerLevel.Tight => new Color("#E3B23C"),
        _ => new Color("#4FB04A"),
    };
}
