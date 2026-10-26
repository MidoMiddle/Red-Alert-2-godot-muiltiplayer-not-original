using System.Collections.Generic;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Math;
using Rts.Simulation.World;

namespace Rts.Simulation.Systems;

/// <summary>
/// Take-off and landing (rules v102, the owner's wish of 2026-10-26: a flier climbs to its flying height before it sets out). Before the
/// movement. A flier that lands (<see cref="Lands"/>: the helicopter, RA2's Night Hawk; a strike jet of an air force command) is
/// <see cref="Unit.Grounded"/> while it has nothing to do — the helicopter over a cell it can set down on (<see cref="CanLand"/>), a
/// jet on its pad (<see cref="AmmoSystem.OnPad"/>). Given anything to do — a move, a path, a target, a building to enter — it first
/// climbs for <see cref="ClimbTicks"/> (<see cref="Unit.ClimbTicks"/>) and only then moves. A Hornet launched from its carrier climbs off
/// the deck the same way (<see cref="AmmoSystem"/>). Climbing, a unit neither moves nor fires.
/// <para>
/// In the air, the fliers that hover (<see cref="Hovers"/>) keep apart (rules v103, <see cref="KeepApart"/>): flying straight to one
/// point and in no cell, the Kirovs of a bot came out of their factory and hung over its rally point one in another, tens of them that
/// looked one.
/// </para>
/// </summary>
public static class FlightSystem
{
    /// <summary>
    /// The ticks a flier climbs from the ground to its flying height: 1.5 cells at 1.2 cells a second (the height and the rate the client
    /// draws), 1.25 s.
    /// </summary>
    public const int ClimbTicks = 19;

    /// <summary>How far apart two hovering fliers keep, in leptons: a cell (<see cref="Spacing"/>).</summary>
    public const int SpacingLeptons = WorldPos.LeptonsPerCell;

    // Away from a flier in the same place: one of eight ways a cell long, by the id of the one that moves off.
    private static readonly (int X, int Y)[] Ways = [(256, 0), (181, 181), (0, 256), (-181, 181), (-256, 0), (-181, -181), (0, -256), (181, -181)];

    public static void Update(GameWorld world)
    {
        foreach (var entity in world.Entities)
        {
            if (entity is not Unit { IsAlive: true } unit || !Lands(unit.Archetype))
            {
                continue;
            }

            var busy = unit.MoveTarget is not null || unit.Path is not null || unit.Target is not null || unit.EnterTarget is not null;
            if (unit.Grounded && busy)
            {
                (unit.Grounded, unit.ClimbTicks) = (false, ClimbTicks);
            }
            else if (!unit.Grounded && !busy && unit.ClimbTicks == 0
                && (unit.Archetype.Type == UnitType.AllianceNighthawk ? CanLand(world, unit.Position.ToCell(), unit.Id) : AmmoSystem.OnPad(world, unit)))
            {
                unit.Grounded = true;
            }
        }
    }

    /// <summary>
    /// Whether a flier hovers and so keeps apart from the others (<see cref="KeepApart"/>): the Kirov, the helicopter, the jetpack trooper,
    /// like RA2's, each over a cell of its own; not a jet, which flies its runs at a target and goes back to rearm, nor a launched one
    /// (a carrier's Hornet, a plane of a paradrop).
    /// </summary>
    public static bool Hovers(UnitArchetype archetype) => archetype.Flies && archetype.Ammo == 0 && !archetype.Has(UnitTalents.Spawned);

    /// <summary>How far a hovering flier keeps from another, in leptons: a cell, half of it for a jetpack trooper.</summary>
    public static int Spacing(UnitArchetype archetype) => archetype.Category == UnitCategory.Infantry ? SpacingLeptons / 2 : SpacingLeptons;

    /// <summary>
    /// The hovering fliers nearer each other than their <see cref="Spacing"/> (the larger of the two) move apart, after the movement:
    /// each half of what they lack, from where they all were at the start, by at most half its speed a tick — a Kirov a cell in
    /// about a second; a flier on the ground or climbing stays and the other moves the whole of it (two helicopters do not set down on one
    /// cell). In the order of <see cref="WorldPos.X"/>, then of the ids: deterministic.
    /// </summary>
    public static void KeepApart(GameWorld world)
    {
        List<(Unit Unit, WorldPos At, bool Stays)>? fliers = null;
        foreach (var entity in world.Entities)
        {
            if (entity is Unit { IsAlive: true, ErasedBy: null } unit && Hovers(unit.Archetype))
            {
                (fliers ??= []).Add((unit, unit.Position, unit.Grounded || unit.ClimbTicks > 0));
            }
        }

        if (fliers is null || fliers.Count < 2)
        {
            return;
        }

        fliers.Sort((a, b) => a.At.X != b.At.X ? a.At.X.CompareTo(b.At.X) : a.Unit.Id.Value.CompareTo(b.Unit.Id.Value));
        var pushes = new (long X, long Y)[fliers.Count];
        for (var i = 0; i < fliers.Count; i++)
        {
            var (first, at, stays) = fliers[i];
            for (var j = i + 1; j < fliers.Count && fliers[j].At.X - at.X < SpacingLeptons; j++)
            {
                var (second, other, otherStays) = fliers[j];
                // On the ground or climbing off it, a helicopter is in the way only of another that would set down there.
                if ((stays && !Lands(second.Archetype)) || (otherStays && !Lands(first.Archetype)) || (stays && otherStays))
                {
                    continue;
                }

                var spacing = IntMath.Max(Spacing(first.Archetype), Spacing(second.Archetype));
                var delta = other - at;
                if (delta.LengthSquared >= (long)spacing * spacing)
                {
                    continue;
                }

                var distance = IntMath.Sqrt(delta.LengthSquared);
                var (wayX, wayY) = distance > 0 ? (delta.X, delta.Y) : Ways[second.Id.Value & 7];
                var length = distance > 0 ? distance : SpacingLeptons;
                var lack = spacing - distance;
                // Each moves half of what they lack; one that stays leaves the whole of it to the other.
                (long Mine, long Theirs) share = stays ? (0, lack) : otherStays ? (lack, 0) : (lack / 2, lack - lack / 2);
                var (mine, theirs) = share;
                pushes[i] = (pushes[i].X - (long)wayX * mine / length, pushes[i].Y - (long)wayY * mine / length);
                pushes[j] = (pushes[j].X + (long)wayX * theirs / length, pushes[j].Y + (long)wayY * theirs / length);
            }
        }

        for (var i = 0; i < fliers.Count; i++)
        {
            var (unit, at, stays) = fliers[i];
            var (x, y) = pushes[i];
            if (stays || (x == 0 && y == 0))
            {
                continue;
            }

            var most = IntMath.Max(1, unit.Speed / 2);
            var squared = x * x + y * y;
            if (squared > (long)most * most)
            {
                var length = IntMath.Sqrt(squared);
                (x, y) = (x * most / length, y * most / length);
            }

            var next = new WorldPos(at.X + (int)x, at.Y + (int)y);
            if (world.Contains(next))
            {
                unit.Position = next;
            }
        }
    }

    /// <summary>Whether a flier lands: the helicopter when idle, a jet of an air force command on its pad (RA2); not a carrier's Hornet.</summary>
    public static bool Lands(UnitArchetype archetype) =>
        archetype.Type == UnitType.AllianceNighthawk || (archetype.Flies && archetype.Ammo > 0 && !archetype.Has(UnitTalents.Spawned));

    /// <summary>
    /// Whether a helicopter can set down on <paramref name="cell"/>: open land — not water, rock or a bridge — on one level, with no
    /// building, no standing map object and no other unit on it — nor a helicopter set down there.
    /// </summary>
    public static bool CanLand(GameWorld world, CellPos cell, EntityId self)
    {
        var map = world.Map;
        if (!map.Contains(cell) || map.LandAt(cell) is not (LandType.Clear or LandType.Rough or LandType.Road or LandType.Beach or LandType.Track or LandType.Path)
            || map.BridgeAt(cell) is not null || world.BuildingAt(cell) is not null)
        {
            return false;
        }

        var heights = map.Heights;
        var level = heights.CornerLevel(cell.X, cell.Y);
        if (heights.CornerLevel(cell.X + 1, cell.Y) != level || heights.CornerLevel(cell.X, cell.Y + 1) != level
            || heights.CornerLevel(cell.X + 1, cell.Y + 1) != level)
        {
            return false;
        }

        foreach (var mapObject in map.ObjectsAt(cell))
        {
            if (mapObject.IsAlive)
            {
                return false;
            }
        }

        foreach (var entity in world.Entities)
        {
            if (entity is Unit { IsAlive: true } other && other.Id != self && (!other.Archetype.Flies || other.Grounded) && other.Position.ToCell() == cell)
            {
                return false;
            }
        }

        return true;
    }
}
