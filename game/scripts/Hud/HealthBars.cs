using Godot;
using Rts.Client.App;
using Rts.Client.Terrain;
using Rts.Client.Units;
using Rts.Simulation.Combat;
using Rts.Simulation.Systems;
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
        DrawRallyPoint(camera, world);
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

            if (entity is Unit { Archetype.PassengerSlots: > 0 } transport && world.AreAllies(SimulationHost.LocalPlayer, transport.Owner))
            {
                // The slots of a transport (stage 3d), for its own team.
                var top = WorldCoords.ToGodotOnGround(transport.Position, UnitHeight + Units.UnitAltitude.Of(transport.Archetype));
                DrawGarrison(camera, top, TransportSystem.SlotsTaken(world, transport), transport.Archetype.PassengerSlots);
            }

            if (entity is Building { Def: { GarrisonCapacity: > 0 } bunker } garrisoned)
            {
                var top = WorldCoords.ToGodotOnGround(Aim.CentreOf(garrisoned), BuildingShape.Of(bunker).Height + 0.6f);
                DrawGarrison(camera, top, garrisoned.Occupants.Count, bunker.GarrisonCapacity);
            }

            var selected = _input?.Selection.Contains(entity.Id) == true || _input?.SelectedFactory == entity.Id;
            switch (entity)
            {
                case Unit unit when HealthPips.Shows(mode, selected, unit.Hp, unit.MaxHp) && !Units.Disguises.LooksLikeTree(world, unit):
                    {
                        var previous = _host.TryGetPreviousPose(unit.Id, out var before) ? before.Position : unit.Position;
                        var height = UnitHeight + Units.UnitAltitude.Of(unit.Archetype);
                        var at = WorldCoords.ToGodotOnGround(previous, height).Lerp(WorldCoords.ToGodotOnGround(unit.Position, height), _host.Alpha);
                        DrawBar(camera, at, HealthPips.CountOf(unit), unit.Hp, unit.MaxHp);
                        DrawRank(camera, at, HealthPips.CountOf(unit), Rts.Simulation.Systems.Veterancy.RankOf(unit));
                        break;
                    }

                case Building { Def: { } def } building when HealthPips.Shows(mode, selected, building.Hp, building.MaxHp):
                    {
                        var height = BuildingShape.Of(def).Height + 0.6f;
                        DrawBar(camera, WorldCoords.ToGodotOnGround(Aim.CentreOf(building), height), HealthPips.CountOf(building), building.Hp, building.MaxHp);
                        break;
                    }

                // A tech building of the map a player took (loose ends, stage 1).
                case Building { Def: null, Owner.IsNeutral: false } taken when HealthPips.Shows(mode, selected, taken.Hp, taken.MaxHp):
                    DrawBar(camera, WorldCoords.ToGodotOnGround(Aim.CentreOf(taken), 1.4f), HealthPips.CountOf(taken), taken.Hp, taken.MaxHp);
                    break;
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

    /// <summary>
    /// The rally point of the selected building that produces: a line from its centre and a flag on the point; none while its units
    /// stop just outside.
    /// </summary>
    private void DrawRallyPoint(Camera3D camera, GameWorld world)
    {
        if (_input?.SelectedFactory is not { } id || !world.TryGet(id, out var entity) || entity is not Building { RallyPoint: { } rally } factory)
        {
            return;
        }

        var from = WorldCoords.ToGodotOnGround(Aim.CentreOf(factory), 0.3f);
        var to = WorldCoords.ToGodotOnGround(rally, 0.05f);
        var top = WorldCoords.ToGodotOnGround(rally, 1.1f);
        if (camera.IsPositionBehind(from) || camera.IsPositionBehind(to))
        {
            return;
        }

        var green = new Color(0.35f, 1f, 0.45f);
        var (start, end, pole) = (camera.UnprojectPosition(from), camera.UnprojectPosition(to), camera.UnprojectPosition(top));
        DrawDashedLine(start, end, green, 2f, 8f);
        DrawLine(end, pole, green, 2f);
        DrawColoredPolygon([pole, pole + new Vector2(14f, 5f), pole + new Vector2(0f, 10f)], green);
    }

    /// <summary>The rank of a unit left of its bar (loose ends, stage 3): one gold chevron for a veteran, two for an elite.</summary>
    private void DrawRank(Camera3D camera, Vector3 at, int pips, Rts.Simulation.Systems.Rank rank)
    {
        if (rank == Rts.Simulation.Systems.Rank.Rookie || camera.IsPositionBehind(at))
        {
            return;
        }

        var centre = camera.UnprojectPosition(at);
        var left = centre.X - ((pips * (PipWidth + Gap)) + Gap) / 2f - 4f;
        var gold = new Color(1f, 0.82f, 0.25f);
        for (var i = 0; i < (int)rank; i++)
        {
            var y = centre.Y - PipHeight - (i * 4f);
            DrawPolyline([new Vector2(left - 6f, y - 3f), new Vector2(left - 3f, y), new Vector2(left, y - 3f)], gold, 1.5f);
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
