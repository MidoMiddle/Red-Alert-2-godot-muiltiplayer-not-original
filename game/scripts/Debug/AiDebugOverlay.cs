using System;
using System.Collections.Generic;
using Godot;
using Rts.AI;
using Rts.AI.Debug;
using Rts.AI.Squads;
using Rts.Simulation.Geometry;

namespace Rts.Client.Debug;

/// <summary>
/// The debug overlay of the bots (docs/implementation/M5_first_bot.md, 4.13, step 13): F12, in debug builds, shows what the bot of the
/// player in view thinks — the bot of the player F11 plays, else the first bot of the match. A panel by the sidebar gives the lines of
/// its <see cref="AiDebugSnapshot"/>; the map shows its squads (a cross at the middle, a line to the target, dashed to where it
/// regroups), the gathering point, the scout's errand, the enemy buildings it remembers (paler the older), the enemy armies it
/// remembers (a diamond by strength), the threat to its base and the building it placed last. The snapshot is asked for
/// <see cref="RefreshesPerSecond"/> times a second; it changes nothing in the match.
/// M8 step 12: Shift with the key opens the <see cref="AiDebugWindow"/> and Tab passes to the next bot of the match; the squads are
/// coloured by their task — a raid, a squad helping an ally, the defense — a raid waiting at a field is ringed, and a bot hitting back
/// after an attack beaten at its base has a ring round its gathering point. In a release build with <c>--ai-debug</c> too.
/// </summary>
public partial class AiDebugOverlay : Node3D
{
    public const int RefreshesPerSecond = 4;
    private const float Lift = 0.3f;

    // Lines of a mesh are one pixel thin: every segment is a flat ribbon this wide instead.
    private const float Width = 0.22f;

    private static readonly Color GatherColor = new(0.3f, 0.9f, 1f);
    private static readonly Color ScoutColor = new(0.8f, 0.8f, 0.8f);
    private static readonly Color ThreatColor = new(1f, 0.15f, 0.15f);
    private static readonly Color MemoryColor = new(1f, 0.45f, 0.3f);
    private static readonly Color PlacedColor = new(0.5f, 1f, 0.4f);
    private static readonly Color RaidColor = new(1f, 0.55f, 1f);
    private static readonly Color HelpColor = new(0.3f, 1f, 0.6f);
    private static readonly Color CounterColor = new(1f, 0.85f, 0.4f);

    private readonly ImmediateMesh _mesh = new();
    private readonly List<(Vector3 From, Vector3 To, Color Color)> _segments = new();
    private SimulationHost _host = null!;
    private Label _panel = null!;
    private double _sinceRefresh;
    private AiDebugSnapshot? _snapshot;
    private AiDebugWindow _window = null!;
    private bool _windowOpen;
    private int _index = -1;

    public bool Enabled { get; private set; }

    public override void _Ready()
    {
        _host = GetNode<SimulationHost>("../SimulationHost");
        AddChild(new MeshInstance3D
        {
            Mesh = _mesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                VertexColorUseAsAlbedo = true,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                NoDepthTest = true,
            },
        });

        var layer = new CanvasLayer { Layer = 5 };
        AddChild(layer);
        _panel = new Label { Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        _panel.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        _panel.GrowHorizontal = Control.GrowDirection.Begin;
        _panel.OffsetRight = -Hud.SidebarLayout.Width - 12;
        _panel.OffsetTop = 12;
        _panel.AddThemeFontSizeOverride("font_size", 18);
        _panel.AddThemeColorOverride("font_color", new Color(0.75f, 1f, 0.85f));
        _panel.AddThemeColorOverride("font_outline_color", Colors.Black);
        _panel.AddThemeConstantOverride("outline_size", 6);
        layer.AddChild(_panel);
        _window = new AiDebugWindow();
        layer.AddChild(_window);
    }

    public override void _UnhandledKeyInput(InputEvent inputEvent)
    {
        if (!AiJournalFiles.Enabled)
        {
            return;
        }

        if (inputEvent.IsActionPressed("debug_ai", false, false) && inputEvent is InputEventKey key)
        {
            // Shift with the key: the window (M8 step 12); alone: the overlay.
            if (key.ShiftPressed)
            {
                _windowOpen = !_windowOpen;
            }
            else
            {
                Enabled = !Enabled;
            }

            _sinceRefresh = double.MaxValue;
            GetViewport().SetInputAsHandled();
        }
        else if (_windowOpen && inputEvent is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Tab })
        {
            _index = (_host.Bots.Count == 0 ? 0 : (Math.Max(_index, 0) + 1) % _host.Bots.Count);
            _sinceRefresh = double.MaxValue;
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        using var frameTimer = Rts.Client.Debug.PerfLog.Time("AiDebugOverlay.process");
        _panel.Visible = Enabled && !_windowOpen;
        _window.Visible = _windowOpen;
        if (!Enabled && !_windowOpen)
        {
            _mesh.ClearSurfaces();
            return;
        }

        _sinceRefresh += delta;
        if (_sinceRefresh >= 1.0 / RefreshesPerSecond)
        {
            _sinceRefresh = 0;
            _snapshot = Bot()?.Snapshot();
            // No bot — the sandbox, a replay — no lines.
            _panel.Text = _snapshot?.ToText() ?? string.Empty;
            if (_windowOpen)
            {
                _window.Show(_snapshot, Math.Max(_index, 0), _host.Bots.Count);
            }
        }

        if (Enabled)
        {
            Draw(_snapshot);
        }
        else
        {
            _mesh.ClearSurfaces();
        }
    }

    /// <summary>The bot Tab chose; else the bot of the player in view, else the first bot of the match; null without one.</summary>
    private AiPlayer? Bot()
    {
        var bots = _host.Bots;
        if (_index >= 0 && _index < bots.Count)
        {
            return bots[_index];
        }

        foreach (var bot in bots)
        {
            if (bot.Player == SimulationHost.LocalPlayer)
            {
                return bot;
            }
        }

        return bots.Count > 0 ? bots[0] : null;
    }

    private void Draw(AiDebugSnapshot? snapshot)
    {
        _mesh.ClearSurfaces();
        _segments.Clear();
        if (snapshot is null)
        {
            return;
        }

        foreach (var building in snapshot.EnemyBuildings)
        {
            var age = Mathf.Clamp((snapshot.Tick - building.SeenTick) / 1800f, 0f, 0.7f);
            Rectangle(building.Cell, building.FarCell, new Color(MemoryColor, 1f - age));
        }

        foreach (var army in snapshot.EnemyArmies)
        {
            Diamond(army.Centre.ToCell(), 1f + Mathf.Min(army.Strength / 1000f, 4f), MemoryColor);
        }

        foreach (var squad in snapshot.Squads)
        {
            // By the task (M8 step 12): a raid, a squad helping an ally; else by the state.
            var color = squad.Task switch
            {
                "raid" or "ambush" => RaidColor,
                "help" => HelpColor,
                _ => SquadColor(squad.State),
            };
            Cross(squad.Centre, 0.8f, color);
            if (squad.Task == "ambush")
            {
                Diamond(squad.Centre, 2.5f, RaidColor);
            }

            if (squad.Target is { } target && squad.State is not (SquadState.Gathering or SquadState.Ready))
            {
                Line(squad.Centre, target, color);
            }

            if (squad.Regroup is { } regroup)
            {
                Dashed(squad.Centre, regroup, color);
            }
        }

        if (snapshot.GatherPoint is { } gather)
        {
            Rectangle(new CellPos(gather.X - 1, gather.Y - 1), new CellPos(gather.X + 1, gather.Y + 1), GatherColor);
            if (snapshot.CounterUntil > snapshot.Tick)
            {
                // Hitting back after an attack beaten at the base (M8 step 10a).
                Rectangle(new CellPos(gather.X - 3, gather.Y - 3), new CellPos(gather.X + 3, gather.Y + 3), CounterColor);
            }
        }

        if (snapshot.ScoutErrand is { } errand && _host.Simulation.World.TryGet(snapshot.Scout ?? default, out var scout)
            && scout is Rts.Simulation.World.Unit { IsAlive: true } unit)
        {
            Line(unit.Position.ToCell(), errand, ScoutColor);
        }

        if (snapshot.Threat is { } threat)
        {
            Cross(threat.Cell, 1.5f, ThreatColor);
        }

        if (snapshot.LastPlacement is { } placed)
        {
            Cross(placed.Cell, 0.6f, PlacedColor);
        }

        // An empty surface is an error to Godot: nothing to draw, no surface.
        if (_segments.Count == 0)
        {
            return;
        }

        _mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        foreach (var (from, to, color) in _segments)
        {
            var along = to - from;
            var side = new Vector3(-along.Z, 0, along.X).Normalized() * (Width / 2);
            Vector3[] corners = [from + side, to + side, to - side, from + side, to - side, from - side];
            foreach (var corner in corners)
            {
                _mesh.SurfaceSetColor(color);
                _mesh.SurfaceAddVertex(corner);
            }
        }

        _mesh.SurfaceEnd();
    }

    private static Color SquadColor(SquadState state) => state switch
    {
        SquadState.Gathering => new Color(0.6f, 0.6f, 1f),
        SquadState.Ready => new Color(0.4f, 0.8f, 1f),
        SquadState.Moving => new Color(1f, 0.9f, 0.2f),
        SquadState.Fighting => new Color(1f, 0.35f, 0.1f),
        SquadState.Defending => new Color(1f, 0.2f, 0.6f),
        _ => new Color(0.7f, 0.7f, 0.7f),
    };

    private static Vector3 At(CellPos cell) => WorldCoords.ToGodotOnGround(cell.Center, Lift);

    private static Vector3 Corner(int x, int y) => WorldCoords.ToGodotOnGround(new CellPos(x, y).Origin, Lift);

    private void Segment(Vector3 from, Vector3 to, Color color) => _segments.Add((from, to, color));

    private void Line(CellPos from, CellPos to, Color color) => Segment(At(from), At(to), color);

    private void Dashed(CellPos from, CellPos to, Color color)
    {
        var (a, b) = (At(from), At(to));
        const int Dashes = 8;
        for (var i = 0; i < Dashes; i++)
        {
            Segment(a.Lerp(b, i / (float)Dashes), a.Lerp(b, (i + 0.5f) / Dashes), color);
        }
    }

    /// <summary>The outline of the cells from <paramref name="from"/> to <paramref name="to"/>, both inclusive.</summary>
    private void Rectangle(CellPos from, CellPos to, Color color)
    {
        var corners = new List<Vector3>
        {
            Corner(from.X, from.Y), Corner(to.X + 1, from.Y), Corner(to.X + 1, to.Y + 1), Corner(from.X, to.Y + 1),
        };
        for (var i = 0; i < corners.Count; i++)
        {
            Segment(corners[i], corners[(i + 1) % corners.Count], color);
        }
    }

    private void Cross(CellPos cell, float size, Color color)
    {
        var middle = At(cell);
        Segment(middle + new Vector3(-size, 0, -size), middle + new Vector3(size, 0, size), color);
        Segment(middle + new Vector3(-size, 0, size), middle + new Vector3(size, 0, -size), color);
    }

    private void Diamond(CellPos cell, float size, Color color)
    {
        var middle = At(cell);
        Vector3[] points = [middle + new Vector3(size, 0, 0), middle + new Vector3(0, 0, size), middle + new Vector3(-size, 0, 0), middle + new Vector3(0, 0, -size)];
        for (var i = 0; i < points.Length; i++)
        {
            Segment(points[i], points[(i + 1) % points.Length], color);
        }
    }
}
