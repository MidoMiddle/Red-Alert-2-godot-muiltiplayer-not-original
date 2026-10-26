using System;
using System.Collections.Generic;
using Godot;
using Rts.Client.App;

namespace Rts.Client.UI;

/// <summary>
/// The backdrop of the match screen of RA2 (the pause menu and the statistics): rays from the middle in the dark colour of the
/// player's faction and its emblem over them, drawn in code until there are pictures — the Allies a steel eagle with spread wings
/// over a V, the Soviets a steel star in a ring. <see cref="Small"/> draws the emblem alone, for the box at the top of the sidebar.
/// </summary>
public partial class FactionEmblem : Control
{
    private const int Rays = 16;

    public Faction Faction { get; set; } = Faction.Union;

    /// <summary>The emblem without the rays' margin: the box at the top of the sidebar.</summary>
    public bool Small { get; set; }

    /// <summary>The emblem faint, behind the statistics (RA2).</summary>
    public bool Dim { get; set; }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        // The rays reach past the corners: they stay inside.
        ClipContents = true;
        Resized += QueueRedraw;
    }

    public override void _Draw()
    {
        using var frameTimer = Rts.Client.Debug.PerfLog.Time("FactionEmblem.draw");
        var (deep, light) = Faction switch
        {
            Faction.Alliance => (new Color(0.02f, 0.05f, 0.22f), new Color(0.07f, 0.13f, 0.38f)),
            Faction.Cult => (new Color(0.12f, 0.02f, 0.16f), new Color(0.26f, 0.06f, 0.32f)),
            _ => (new Color(0.22f, 0.02f, 0.02f), new Color(0.38f, 0.07f, 0.06f)),
        };
        var size = Size;
        DrawRect(new Rect2(Vector2.Zero, size), deep);
        var centre = size / 2f;
        var reach = size.Length();
        for (var i = 0; i < Rays; i += 2)
        {
            var a = i * Mathf.Tau / Rays;
            var b = (i + 1) * Mathf.Tau / Rays;
            DrawColoredPolygon([centre, centre + (Vector2.Right.Rotated(a) * reach), centre + (Vector2.Right.Rotated(b) * reach)], light);
        }

        // The eagle's wings reach twice as far as the star: it fits the width too.
        var scale = Faction == Faction.Alliance ? Mathf.Min(size.Y * (Small ? 0.45f : 0.38f), size.X * 0.23f) : Mathf.Min(size.X, size.Y) * (Small ? 0.45f : 0.38f);
        // Only the emblem dims, not what stands on it (the table).
        SelfModulate = Dim ? new Color(0.5f, 0.5f, 0.55f) : Colors.White;
        if (Faction == Faction.Alliance)
        {
            Eagle(centre, scale);
        }
        else
        {
            Star(centre, scale);
        }
    }

    // Steel, a shade darker in its shadows; the rim of the faction's colour.
    private static readonly Color Steel = new(0.55f, 0.58f, 0.64f, 0.9f);
    private static readonly Color Shadow = new(0.3f, 0.32f, 0.38f, 0.9f);

    private void Eagle(Vector2 c, float s)
    {
        var rim = new Color(0.3f, 0.55f, 1f, 0.9f);
        Vector2 P(float x, float y) => c + (new Vector2(x, y) * s);

        // The V under the eagle.
        Vector2[] v = [P(-1.05f, -0.2f), P(1.05f, -0.2f), P(0f, 1.2f)];
        DrawColoredPolygon(v, Shadow);
        DrawPolyline([.. v, v[0]], rim, 3f);
        DrawPolyline([P(-0.7f, -0.05f), P(0f, 0.9f), P(0.7f, -0.05f)], rim, 2f);

        // The wings, spread up and out, feathered with dark lines from the shoulder to the trailing edge.
        foreach (var side in new[] { -1f, 1f })
        {
            Vector2[] wing = [P(side * 0.14f, -0.42f), P(side * 2.0f, -1.0f), P(side * 2.05f, -0.78f), P(side * 1.25f, -0.18f), P(side * 0.22f, 0.02f)];
            DrawColoredPolygon(Clockwise(wing), Steel);
            DrawPolyline([.. wing, wing[0]], Shadow, 2f);
            for (var f = 1; f <= 7; f++)
            {
                var t = f / 8f;
                var edge = P(side * (2.05f - (t * 1.83f)), -0.78f + (t * 0.8f));
                DrawLine(P(side * 0.25f, -0.3f), edge.Lerp(P(side * 0.25f, -0.3f), 0.15f), Shadow, 2f);
            }
        }

        // The body: a chest narrowing to the tail, the head turned right with its beak.
        DrawColoredPolygon([P(-0.13f, -0.52f), P(0.13f, -0.52f), P(0.24f, -0.05f), P(0.12f, 0.45f), P(0f, 0.72f), P(-0.12f, 0.45f), P(-0.24f, -0.05f)], Steel);
        DrawPolyline([P(-0.13f, -0.52f), P(-0.24f, -0.05f), P(-0.12f, 0.45f), P(0f, 0.72f), P(0.12f, 0.45f), P(0.24f, -0.05f), P(0.13f, -0.52f)], Shadow, 2f);
        DrawCircle(P(0f, -0.64f), 0.15f * s, Steel);
        DrawColoredPolygon([P(0.12f, -0.7f), P(0.32f, -0.62f), P(0.12f, -0.56f)], new Color(0.85f, 0.75f, 0.35f, 0.95f));
        DrawCircle(P(0.04f, -0.68f), 0.03f * s, Shadow);
    }

    private void Star(Vector2 c, float s)
    {
        var rim = new Color(1f, 0.3f, 0.2f, 0.9f);
        DrawArc(c, 1.05f * s, 0f, Mathf.Tau, 64, Shadow, 0.14f * s);
        DrawArc(c, 1.13f * s, 0f, Mathf.Tau, 64, rim, 3f);
        var points = new List<Vector2>();
        for (var i = 0; i < 10; i++)
        {
            var angle = (-Mathf.Pi / 2f) + (i * Mathf.Pi / 5f);
            points.Add(c + (Vector2.Right.Rotated(angle) * (i % 2 == 0 ? 0.9f : 0.38f) * s));
        }

        // A star is not convex: a fan of triangles from the middle.
        for (var i = 0; i < points.Count; i++)
        {
            DrawColoredPolygon([c, points[i], points[(i + 1) % points.Count]], i % 2 == 0 ? Steel : Shadow);
        }

        DrawPolyline([.. points, points[0]], rim, 2.5f);
    }

    // Godot draws a polygon in either winding, but a self-touching one not: the wing is given in one order for both sides.
    private static Vector2[] Clockwise(Vector2[] points)
    {
        var area = 0f;
        for (var i = 0; i < points.Length; i++)
        {
            var (a, b) = (points[i], points[(i + 1) % points.Length]);
            area += (a.X * b.Y) - (b.X * a.Y);
        }

        if (area < 0)
        {
            Array.Reverse(points);
        }

        return points;
    }
}
