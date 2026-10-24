using System.Collections.Generic;
using System.Collections.Immutable;
using Rts.Simulation.Combat;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Movement;
using Rts.Simulation.Rules;
using Rts.Simulation.World;

namespace Rts.Simulation.Systems;

/// <summary>
/// The commando's C4 on a bridge (RA2): a unit forced to attack a point on a standing bridge segment, with a weapon whose warhead plants
/// a bomb on bridges (<see cref="WarheadDef.Bridges"/>), fights with that weapon only — it walks up and plants. The bomb waits
/// <see cref="WarheadDef.BombDelay"/> ticks in <see cref="GameWorld.BridgeBombs"/>, one a segment, and brings the segment down whatever
/// its strength; the planter forgets the point. An engineer mends the bridge through its repair hut (<see cref="BridgeRepair"/>).
/// With «Destroyable Bridges» off (<see cref="Matches.MatchOptions.DestroyableBridges"/>) no bomb goes on a bridge.
/// </summary>
public static class BridgeDemolition
{
    /// <summary>Whether a unit of the player could plant a bomb on the bridge at <paramref name="cell"/>: the cursor and the order of the client.</summary>
    public static bool CanBlow(GameWorld world, Unit unit, CellPos cell) =>
        unit.IsAlive && Blows(world.Rules, world.Rules.WeaponsOf(unit)) && StandingAt(world, cell) is not null;

    /// <summary>
    /// The weapons of a unit that fires at <paramref name="point"/> with no target: on a standing bridge, those that blow bridges up, the
    /// others left out (-1, so the slots keep their places) — or all of them, as for any ground, when none does.
    /// </summary>
    internal static ImmutableArray<int> WeaponsAt(GameWorld world, ImmutableArray<int> weapons, WorldPos point)
    {
        var rules = world.Rules;
        if (!Blows(rules, weapons) || StandingAt(world, point.ToCell()) is null)
        {
            return weapons;
        }

        var builder = ImmutableArray.CreateBuilder<int>(weapons.Length);
        foreach (var weapon in weapons)
        {
            builder.Add(weapon >= 0 && Blows(rules, weapon) ? weapon : -1);
        }

        return builder.MoveToImmutable();
    }

    /// <summary>A bomb shot landed on the ground: on a standing bridge segment with none, the bomb goes on it.</summary>
    internal static void Plant(GameWorld world, Hit hit, WarheadDef warhead)
    {
        if (!warhead.Bridges || StandingAt(world, hit.Point.ToCell()) is not { } segment)
        {
            return;
        }

        if (!world.BridgeBombs.Exists(bomb => bomb.Bridge == segment.Bridge.Id && bomb.Segment == segment.Index))
        {
            world.BridgeBombs.Add(new BridgeBomb(hit.Owner, hit.Shooter, segment.Bridge.Id, segment.Index, world.CurrentTick + warhead.BombDelay));
        }

        // The planter is done with the segment, its bomb or another's on it.
        if (world.TryGet(hit.Shooter, out var shooter) && shooter is Unit { Target: null, TargetPoint: { } point } planter
            && world.Map.BridgeAt(point.ToCell()) == segment)
        {
            UnitOrders.ForgetTarget(planter);
        }
    }

    /// <summary>The bombs whose time came bring their segments down, in the order planted.</summary>
    internal static void Update(GameWorld world, List<SimEvent>? events)
    {
        for (var i = 0; i < world.BridgeBombs.Count;)
        {
            var bomb = world.BridgeBombs[i];
            if (bomb.GoesOff > world.CurrentTick)
            {
                i++;
                continue;
            }

            world.BridgeBombs.RemoveAt(i);
            foreach (var bridge in world.Map.Bridges)
            {
                if (bridge.Id == bomb.Bridge && bridge.Segments[bomb.Segment] is { IsIntact: true } segment)
                {
                    world.DestroyBridgeSegment(segment, events);
                }
            }
        }
    }

    /// <summary>The standing bridge segment at the cell that a bomb may bring down; null when none, or when bridges cannot fall.</summary>
    private static BridgeSegment? StandingAt(GameWorld world, CellPos cell) =>
        world.Match is not { Options.DestroyableBridges: false } && world.Map.Contains(cell) && world.Map.BridgeAt(cell) is { IsIntact: true } segment
            ? segment
            : null;

    private static bool Blows(GameRules rules, ImmutableArray<int> weapons)
    {
        foreach (var weapon in weapons)
        {
            if (weapon >= 0 && Blows(rules, weapon))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Blows(GameRules rules, int weapon) =>
        rules.Database.Warheads[rules.Database.IndexOfWarhead(rules.Database.Weapons[weapon].Warhead)].Bridges;
}
