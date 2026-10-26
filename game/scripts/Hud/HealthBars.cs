using System.Collections.Generic;
using Godot;
using Rts.Client.App;
using Rts.Client.Controls;
using Rts.Client.Terrain;
using Rts.Client.Units;
using Rts.Simulation.Combat;
using Rts.Simulation.Systems;
using Rts.Simulation.World;

namespace Rts.Client.Hud;

/// <summary>
/// The health bars of the field (docs/implementation/M4_combat.md, 4.20, step 13): every bar in one <c>_Draw</c> on the screen, a row of
/// <see cref="HealthPips"/> over each unit and player building — by default only over what is selected or under the cursor, as in RA2
/// (the owner, 2026-10-25), else as the setting <c>interface/health_bars</c> says. Units are placed where they are drawn, between the
/// ticks; what the fog hides and the ghosts of buildings have no bar. Under a transport or a building that holds soldiers (stage 2e,
/// 3d) a row of boxes shows its room, the soldiers inside filled in, also only while it is selected or under the cursor.
/// </summary>
public partial class HealthBars : Control
{
    private const float PipWidth = 4f;
    private const float PipHeight = 5f;
    private const float Gap = 1f;
    private const float UnitHeight = 1.1f;

    // The names over the field: their size, and how many selected units are named at most (more would hide the field).
    private const int NameSize = 13;
    private const int MaxNamedSelection = 8;

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
        using var frameTimer = Rts.Client.Debug.PerfLog.Time("HealthBars.draw");
        if (_host is null || GetViewport().GetCamera3D() is not { } camera)
        {
            return;
        }

        var mode = AppSettings.Instance?.Values.HealthBars ?? HealthBarMode.Selected;
        var world = _host.Simulation.World;
        DrawRallyPoint(camera, world);
        DrawRallyChoice(camera, world);
        DrawNames(camera, world);
        DrawPrimaries(camera, world);
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

            // Selected, or under the cursor: its bar and its room show.
            var selected = Focused(world, entity);
            if (entity is Unit { Archetype.PassengerSlots: > 0 } transport && selected && world.AreAllies(SimulationHost.LocalPlayer, transport.Owner))
            {
                // The slots of a transport (stage 3d), for its own team.
                var top = WorldCoords.ToGodotOnUnit(transport, transport.Position, UnitHeight + Units.UnitAltitude.Of(transport));
                DrawGarrison(camera, top, TransportSystem.SlotsTaken(world, transport), transport.Archetype.PassengerSlots);
            }

            if (entity is Building { Def: { GarrisonCapacity: > 0 } bunker } garrisoned && selected)
            {
                var top = WorldCoords.ToGodotOnGround(Aim.CentreOf(garrisoned), BuildingShape.Of(bunker).Height + 0.6f);
                DrawGarrison(camera, top, garrisoned.Occupants.Count, bunker.GarrisonCapacity);
            }

            // A civilian building of the map with soldiers inside: how full it is, for everyone (RA2).
            if (entity is Building { Def: null, Occupants.Count: > 0 } civilian && selected && Rts.Simulation.Systems.GarrisonSystem.Civilian(civilian))
            {
                var type = civilian.Type;
                var height = BuildingLooks.Known(type.Id) ? BuildingLooks.Top(BuildingLooks.Of(type.Id, type.Width, type.Height, false)) : 1f;
                var top = WorldCoords.ToGodotOnGround(Aim.CentreOf(civilian), height + 0.6f);
                DrawGarrison(camera, top, civilian.Occupants.Count, Rts.Simulation.Systems.GarrisonSystem.Capacity(civilian));
            }

            switch (entity)
            {
                // The bar by the mode; the rank of a veteran or an elite always, as in RA2.
                case Unit unit when !Units.Disguises.LooksLikeTree(world, unit):
                    {
                        var bar = HealthPips.Shows(mode, selected, unit.Hp, unit.MaxHp);
                        var rank = Rts.Simulation.Systems.Veterancy.RankOf(unit);
                        if (!bar && rank == Rts.Simulation.Systems.Rank.Rookie)
                        {
                            break;
                        }

                        var previous = _host.TryGetPreviousPose(unit.Id, out var before) ? before.Position : unit.Position;
                        var height = UnitHeight + Units.UnitAltitude.Of(unit);
                        var at = WorldCoords.ToGodotOnUnit(unit, Units.SoldierSpots.Drawn(unit, previous), height)
                            .Lerp(WorldCoords.ToGodotOnUnit(unit, Units.SoldierSpots.Drawn(unit, unit.Position), height), _host.Alpha);
                        if (bar)
                        {
                            DrawBar(camera, at, HealthPips.CountOf(unit), unit.Hp, unit.MaxHp);
                        }

                        DrawRank(camera, at, HealthPips.CountOf(unit), rank);
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

    // Whether the entity is selected or under the cursor: a unit picked by the pointer, or a building standing on the cell under it.
    private bool Focused(GameWorld world, Entity entity)
    {
        if (_input is null)
        {
            return false;
        }

        if (_input.Selection.Contains(entity.Id) || _input.SelectedFactory == entity.Id || _input.Hovered == entity.Id || _input.Inspected == entity.Id)
        {
            return true;
        }

        return _input.Hovered is null && entity is Building building && _input.CellUnderPointer is { } cell && building.Covers(cell);
    }

    private void DrawBomb(Camera3D camera, Entity entity, int tick)
    {
        var at = entity switch
        {
            Building { Def: { } def } building => WorldCoords.ToGodotOnGround(Aim.CentreOf(building), BuildingShape.Of(def).Height + 1f),
            Unit unit => WorldCoords.ToGodotOnUnit(unit, unit.Position, UnitHeight + Units.UnitAltitude.Of(unit) + 0.4f),
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
    /// The names over the field (the owner's wish, 2026-10-24: there are no models yet): over the unit or building under the cursor,
    /// over the selected units while they are few, and over the selected building — RA2's names for now (<see cref="DisplayNames"/>).
    /// A disguised infiltrator is named as what it looks like, a holo tank that passes for a tree not at all.
    /// </summary>
    private void DrawNames(Camera3D camera, GameWorld world)
    {
        if (_input is null)
        {
            return;
        }

        var named = new HashSet<EntityId>();
        if (_input.Hovered is { } hovered)
        {
            named.Add(hovered);
        }
        else if (_input.CellUnderPointer is { } cell && world.BuildingAt(cell) is { } underCursor)
        {
            named.Add(underCursor.Id);
        }

        if (_input.Selection.Units.Count <= MaxNamedSelection)
        {
            named.UnionWith(_input.Selection.Units);
        }

        if (_input.SelectedFactory is { } factory)
        {
            named.Add(factory);
        }

        foreach (var id in named)
        {
            if (!world.TryGet(id, out var entity) || !entity.IsAlive || !FogOfWar.Shows(world, entity) || NameAndPlace(world, entity) is not var (name, at)
                || camera.IsPositionBehind(at))
            {
                continue;
            }

            var font = GetThemeDefaultFont();
            var width = font.GetStringSize(name, HorizontalAlignment.Left, -1f, NameSize).X;
            var point = camera.UnprojectPosition(at) - new Vector2(width / 2f, PipHeight + 8f);
            DrawStringOutline(font, point, name, HorizontalAlignment.Left, -1f, NameSize, 4, new Color(0f, 0f, 0f, 0.85f));
            DrawString(font, point, name, HorizontalAlignment.Left, -1f, NameSize, new Color(0.97f, 0.95f, 0.86f));
        }
    }

    /// <summary>
    /// «Primary» over the player's main factory of a kind it has more than one of (RA2): what that kind finishes comes out there.
    /// </summary>
    private void DrawPrimaries(Camera3D camera, GameWorld world)
    {
        var shown = new HashSet<EntityId>();
        foreach (var category in new[] { Rts.Simulation.Rules.ProductionCategory.Infantry, Rts.Simulation.Rules.ProductionCategory.Vehicle,
                     Rts.Simulation.Rules.ProductionCategory.Naval, Rts.Simulation.Rules.ProductionCategory.Aircraft })
        {
            if (ProductionSystem.MainFactory(world, SimulationHost.LocalPlayer, category, out var count) is not { Def: { } def } main || count < 2
                || !shown.Add(main.Id))
            {
                continue;
            }

            var at = WorldCoords.ToGodotOnGround(Aim.CentreOf(main), BuildingShape.Of(def).Height + 0.3f);
            if (camera.IsPositionBehind(at))
            {
                continue;
            }

            var text = Tr("UI_PRIMARY");
            var font = GetThemeDefaultFont();
            var width = font.GetStringSize(text, HorizontalAlignment.Left, -1f, NameSize).X;
            var point = camera.UnprojectPosition(at) - new Vector2(width / 2f, 0f);
            DrawStringOutline(font, point, text, HorizontalAlignment.Left, -1f, NameSize, 4, new Color(0f, 0f, 0f, 0.85f));
            DrawString(font, point, text, HorizontalAlignment.Left, -1f, NameSize, new Color(1f, 0.85f, 0.25f));
        }
    }

    /// <summary>What the name over an entity says and where it stands, or null for one without a name to show.</summary>
    private (string Name, Vector3 At)? NameAndPlace(GameWorld world, Entity entity)
    {
        var ra2 = AppSettings.Instance?.Values.Ra2Names ?? true;
        switch (entity)
        {
            case Unit unit when !Units.Disguises.LooksLikeTree(world, unit):
                var shape = UnitRenderer.Disguise(world, unit).Shape;
                world.Rules.Database.TryGetUnit(shape.Id, out var def);
                var height = UnitHeight + Units.UnitAltitude.Of(unit);
                return (DisplayNames.Of(shape.Id, def?.Name ?? shape.Id, ra2, key => Tr(key)), WorldCoords.ToGodotOnUnit(unit, unit.Position, height + 0.2f));
            case Building { Def: { } building } placed:
                return (DisplayNames.Of(building.Id, building.Name, ra2, key => Tr(key)),
                    WorldCoords.ToGodotOnGround(Aim.CentreOf(placed), BuildingShape.Of(building).Height + 0.8f));
            case Building map:
                // The buildings of the map have plain names (Hospital, Oil Derrick), the same either way.
                var key = DisplayNames.KeyOf(map.Type.Id);
                var text = Tr(key);
                return text == key ? null : (text, WorldCoords.ToGodotOnGround(Aim.CentreOf(map), 1.6f));
            default:
                return null;
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
        Dashes(start, end, green);
        DrawLine(end, pole, green, 2f);
        DrawColoredPolygon([pole, pole + new Vector2(14f, 5f), pole + new Vector2(0f, 10f)], green);
    }

    /// <summary>
    /// While the rally point of the selected building is chosen (the cursor over the ground, no units selected): a dashed line from
    /// the building to the pointer, fainter than the one to the point set.
    /// </summary>
    private void DrawRallyChoice(Camera3D camera, GameWorld world)
    {
        if (_input is not { Selection.IsEmpty: true, Tool: PointerTool.None, PointerGround: { } ground, SelectedFactory: { } id }
            || _input.Hovered is not null || !world.TryGet(id, out var entity) || entity is not Building factory
            || GetViewport().GuiGetHoveredControl() is not null)
        {
            return;
        }

        var from = WorldCoords.ToGodotOnGround(Aim.CentreOf(factory), 0.3f);
        var to = WorldCoords.ToGodotOnGround(ground, 0.05f);
        if (!camera.IsPositionBehind(from) && !camera.IsPositionBehind(to))
        {
            Dashes(camera.UnprojectPosition(from), camera.UnprojectPosition(to), new Color(1f, 0.84f, 0.25f, 0.8f));
        }
    }

    // A dashed line in long dashes, so it never reads as a solid one: 12 px drawn, 8 px left out.
    private void Dashes(Vector2 from, Vector2 to, Color color)
    {
        const float dash = 12f;
        const float gap = 8f;
        var length = from.DistanceTo(to);
        var step = length > 0f ? (to - from) / length : Vector2.Zero;
        for (var at = 0f; at < length; at += dash + gap)
        {
            DrawLine(from + step * at, from + step * Mathf.Min(length, at + dash), color, 2.5f);
        }
    }

    /// <summary>
    /// The rank of a unit left of its bar, shown or not (loose ends, stage 3): one gold chevron pointing up for a veteran, three for an
    /// elite, as in RA2.
    /// </summary>
    private void DrawRank(Camera3D camera, Vector3 at, int pips, Rts.Simulation.Systems.Rank rank)
    {
        if (rank == Rts.Simulation.Systems.Rank.Rookie || camera.IsPositionBehind(at))
        {
            return;
        }

        var centre = camera.UnprojectPosition(at);
        var left = centre.X - ((pips * (PipWidth + Gap)) + Gap) / 2f - 4f;
        var gold = new Color(1f, 0.82f, 0.25f);
        var chevrons = rank == Rts.Simulation.Systems.Rank.Elite ? 3 : 1;
        for (var i = 0; i < chevrons; i++)
        {
            var y = centre.Y - PipHeight + 2f - (i * 4f);
            DrawPolyline([new Vector2(left - 6f, y), new Vector2(left - 3f, y - 3f), new Vector2(left, y)], gold, 1.5f);
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
