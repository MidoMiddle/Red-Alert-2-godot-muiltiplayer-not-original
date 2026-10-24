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
/// buildings have no bar. Under a building that holds soldiers (stage 2e) a row of boxes shows its room, the soldiers inside filled in,
/// whatever the setting.
/// </summary>
public partial class HealthBars : Control
{
    private const float PipWidth = 4f;
    private const float PipHeight = 5f;
    private const float Gap = 1f;
    private const float UnitHeight = 0.9f;

    private static readonly Color Frame = new(0.05f, 0.05f, 0.05f, 0.85f);
    private static readonly Color Empty = new(0.25f, 0.25f, 0.25f, 0.9f);
    private static readonly Color Soldier = new(0.95f, 0.85f, 0.3f);

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

            if (entity.BombTicks > 0)
            {
                // A bomb on it (stage 3b): a red square that blinks faster as the time runs out.
                DrawBomb(camera, entity, world.CurrentTick);
            }

            if (entity is Building { Def: { GarrisonCapacity: > 0 } bunker } garrisoned)
            {
                var top = WorldCoords.ToGodotOnGround(Aim.CentreOf(garrisoned), BuildingShape.Of(bunker).Height + 0.6f);
                DrawGarrison(camera, top, garrisoned.Occupants.Count, bunker.GarrisonCapacity);
            }

            var selected = _input?.Selection.Contains(entity.Id) == true;
            switch (entity)
            {
                case Unit unit when HealthPips.Shows(mode, selected, unit.Hp, unit.MaxHp):
                    {
                        var previous = _host.TryGetPreviousPose(unit.Id, out var before) ? before.Position : unit.Position;
                        var height = UnitHeight + Units.UnitAltitude.Of(unit.Archetype);
                        var at = WorldCoords.ToGodotOnGround(previous, height).Lerp(WorldCoords.ToGodotOnGround(unit.Position, height), _host.Alpha);
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

    private void DrawBomb(Camera3D camera, Entity entity, int tick)
    {
        var at = entity switch
        {
            Building { Def: { } def } building => WorldCoords.ToGodotOnGround(Aim.CentreOf(building), BuildingShape.Of(def).Height + 1f),
            Unit unit => WorldCoords.ToGodotOnGround(unit.Position, UnitHeight + Units.UnitAltitude.Of(unit.Archetype) + 0.4f),
            _ => (Vector3?)null,
        };
        var period = entity.BombTicks > 60 ? 15 : 4;
        if (at is not { } point || camera.IsPositionBehind(point) || tick / period % 2 == 1)
        {
            return;
        }

        var size = 7f;
        DrawRect(new Rect2(camera.UnprojectPosition(point) - new Vector2(size / 2f, size), new Vector2(size, size)), new Color(1f, 0.15f, 0.1f));
    }

    // The room of a building that holds soldiers, just under where its health bar goes.
    private void DrawGarrison(Camera3D camera, Vector3 at, int inside, int room)
    {
        if (camera.IsPositionBehind(at))
        {
            return;
        }

        var size = PipHeight + 1f;
        var width = room * (size + Gap) + Gap;
        var corner = camera.UnprojectPosition(at) + new Vector2(-width / 2f, 2 * Gap);
        DrawRect(new Rect2(corner, new Vector2(width, size + 2 * Gap)), Frame);
        for (var i = 0; i < room; i++)
        {
            DrawRect(new Rect2(corner + new Vector2(Gap + i * (size + Gap), Gap), new Vector2(size, size)), i < inside ? Soldier : Empty);
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
