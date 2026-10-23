using Godot;
using Rts.Client.Matches;

namespace Rts.Client.UI;

/// <summary>The strength of every player's army over the match: a line in the player's colour, the time from left to right.</summary>
public partial class ArmyGraph : Control
{
    private MatchSummary? _summary;

    /// <summary>Draws the series of <paramref name="summary"/>.</summary>
    public void Plot(MatchSummary summary)
    {
        _summary = summary;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var size = Size;
        DrawRect(new Rect2(Vector2.Zero, size), new Color(0, 0, 0, 0.35f));
        DrawLine(new Vector2(0, size.Y), new Vector2(size.X, size.Y), new Color(1, 1, 1, 0.5f), 1);
        if (_summary is null)
        {
            return;
        }

        var font = ThemeDB.FallbackFont;
        var max = _summary.MaxStrength;
        for (var i = 0; i < _summary.Rows.Length; i++)
        {
            var points = MatchSummary.GraphPoints(_summary.Series[i], size.X, size.Y, max, _summary.DurationTicks);
            if (points.Count < 2)
            {
                continue;
            }

            var line = new Vector2[points.Count];
            for (var p = 0; p < points.Count; p++)
            {
                line[p] = new Vector2(points[p].X, points[p].Y);
            }

            DrawPolyline(line, PlayerColors.Of(_summary.Rows[i].Player), 3, true);
        }

        DrawString(font, new Vector2(4, size.Y - 6), "0:00", HorizontalAlignment.Left, -1, 14, new Color(1, 1, 1, 0.8f));
        DrawString(font, new Vector2(size.X - 60, size.Y - 6), _summary.Duration, HorizontalAlignment.Right, 56, 14, new Color(1, 1, 1, 0.8f));
        DrawString(font, new Vector2(4, 16), max.ToString(System.Globalization.CultureInfo.InvariantCulture), HorizontalAlignment.Left, -1, 14, new Color(1, 1, 1, 0.8f));
    }
}
