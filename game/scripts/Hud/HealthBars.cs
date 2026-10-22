using Godot;
using Rts.Client.App;
using Rts.Client.Terrain;
using Rts.Client.Units;
using Rts.Simulation.Combat;
using Rts.Simulation.World;

namespace Rts.Client.Hud;

/// <summary>
/// The health bars of the field (docs/implementation/M4_combat.md, 4.20, step 13): every bar in one <c>_Draw</c> on the screen, a row of
/// <see cref="HealthPips"/> over each unit and player building, for the selected and the damaged or for all (the setting
/// <c>interface/health_bars</c>). Units are placed where they are drawn, between the ticks; what the fog hides and the ghosts of
/// buildings have no bar.
/// </summary>
public partial class HealthBars : Control
{
    private const float PipWidth = 4f;
    private const float PipHeight = 5f;
    private const float Gap = 1f;
    private const float UnitHeight = 0.9f;

    private static readonly Color Frame = new(0.05f, 0.05f, 0.05f, 0.85f);
    private static readonly Color Empty = new(0.25f, 0.25f, 0.25f, 0.9f);

    private SimulationHost? _host;
    private PlayerInput? _input;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);
        _host = GetNodeOrNull<SimulationHost>("../../SimulationHost");
        _input = GetNodeOrNull<PlayerInput>("../../PlayerInput");
    }

    public override void _Process(double delta) => QueueRedraw();

    public override void _Draw()
    {
        if (_host is null || GetViewport().GetCamera3D() is not { } camera)
        {
            return;
        }

        var mode = AppSettings.Instance?.Values.HealthBars ?? HealthBarMode.Damaged;
        var world = _host.Simulation.World;
        foreach (var entity in world.Entities)
        {
            if (!entity.IsAlive || !FogOfWar.Shows(world, entity))
            {
                continue;
            }

            var selected = _input?.Selection.Contains(entity.Id) == true;
            switch (entity)
            {
                case Unit unit when HealthPips.Shows(mode, selected, unit.Hp, unit.MaxHp):
                    {
                        var previous = _host.TryGetPreviousPose(unit.Id, out var before) ? before.Position : unit.Position;
                        var at = WorldCoords.ToGodotOnGround(previous, UnitHeight).Lerp(WorldCoords.ToGodotOnGround(unit.Position, UnitHeight), _host.Alpha);
                        DrawBar(camera, at, HealthPips.CountOf(unit), unit.Hp, unit.MaxHp);
                        break;
                    }

                case Building { Def: { } def } building when HealthPips.Shows(mode, selected, building.Hp, building.MaxHp):
                    {
                        var height = BuildingShape.Of(def).Height + 0.6f;
                        DrawBar(camera, WorldCoords.ToGodotOnGround(Aim.CentreOf(building), height), HealthPips.CountOf(building), building.Hp, building.MaxHp);
                        break;
                    }
            }
        }
    }

    private void DrawBar(Camera3D camera, Vector3 at, int pips, int hp, int max)
    {
        if (camera.IsPositionBehind(at))
        {
            return;
        }

        var centre = camera.UnprojectPosition(at);
        var width = pips * (PipWidth + Gap) + Gap;
        var corner = centre - new Vector2(width / 2f, PipHeight + 2 * Gap);
        DrawRect(new Rect2(corner, new Vector2(width, PipHeight + 2 * Gap)), Frame);
        var filled = HealthPips.Filled(pips, hp, max);
        var (r, g, b) = HealthPips.ColorOf(hp, max);
        var full = new Color(r, g, b);
        for (var i = 0; i < pips; i++)
        {
            var pip = new Rect2(corner + new Vector2(Gap + i * (PipWidth + Gap), Gap), new Vector2(PipWidth, PipHeight));
            DrawRect(pip, i < filled ? full : Empty);
        }
    }
}
