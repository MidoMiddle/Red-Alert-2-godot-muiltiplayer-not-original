using System.Collections.Generic;
using System.Collections.Immutable;
using Rts.Simulation.Events;
using Rts.Simulation.Rules;
using Rts.Simulation.Systems;
using Rts.Simulation.World;

namespace Rts.Simulation.Matches;

/// <summary>How a match ended: on which tick, and who won — the players of one team still standing; none for a draw.</summary>
public sealed record MatchResult(int Tick, ImmutableArray<PlayerId> Winners);

/// <summary>
/// Victory and defeat (docs/implementation/M5_first_bot.md, 4.8, step 10), only in a match (<see cref="GameWorld.Match"/>): once in
/// <see cref="Period"/> ticks, late in the tick, before the fallen leave the world.
/// <list type="bullet">
/// <item>A player of the match loses in the normal game with no building but walls — a growing one counts — and no unit; in the short
/// game (<see cref="MatchOptions.ShortGame"/>) with no building but walls and no MCV.</item>
/// <item>The defeated (<see cref="Defeat"/>, also by <see cref="Commands.SurrenderCommand"/>): <see cref="PlayerState.DefeatedTick"/>,
/// a <see cref="PlayerDefeated"/>; whatever of theirs is left, walls too, is destroyed with <see cref="DeathCause.Defeat"/>; their queues
/// are emptied. Their commands are refused from then on, and their bot no longer thinks. One who left a network match
/// (<see cref="Abandon"/>, by <see cref="Commands.PlayerLeftCommand"/>) is defeated too, but what is theirs stays, idle.</item>
/// <item>The match ends when the players still standing are all of one team (<see cref="MatchPlayer.Team"/>), or none is left:
/// <see cref="GameWorld.Result"/> and a <see cref="MatchEnded"/>. The simulation goes on after it, with no more checks.</item>
/// </list>
/// The neutral player is not a player of the match: its buildings neither keep anybody alive nor stop the match from ending.
/// </summary>
public static class VictorySystem
{
    /// <summary>One second at 15 ticks a second.</summary>
    public const int Period = 15;

    public static void Update(GameWorld world, List<SimEvent>? events)
    {
        if (world.Match is not { } match || world.Result is not null || world.CurrentTick % Period != 0)
        {
            return;
        }

        foreach (var player in match.Players)
        {
            if (world.Player(player.Id).DefeatedTick is null && HasLost(world, player.Id, match.Options.ShortGame))
            {
                Defeat(world, player.Id, events);
            }
        }

        var team = -1;
        var standing = ImmutableArray.CreateBuilder<PlayerId>();
        foreach (var player in match.Players)
        {
            if (world.Player(player.Id).DefeatedTick is not null)
            {
                continue;
            }

            if (team >= 0 && player.Team != team)
            {
                return;
            }

            team = player.Team;
            standing.Add(player.Id);
        }

        world.Result = new MatchResult(world.CurrentTick, standing.ToImmutable());
        events?.Add(new MatchEnded(world.CurrentTick, world.Result.Winners));
    }

    /// <summary>
    /// Defeats <paramref name="player"/> now: <see cref="PlayerState.DefeatedTick"/>, a <see cref="PlayerDefeated"/>, everything of theirs
    /// destroyed, their queues emptied.
    /// </summary>
    public static void Defeat(GameWorld world, PlayerId player, List<SimEvent>? events)
    {
        var state = world.Player(player);
        state.DefeatedTick = world.CurrentTick;
        events?.Add(new PlayerDefeated(world.CurrentTick, player));
        foreach (var entity in world.Entities)
        {
            if (!entity.IsAlive || entity.Owner != player)
            {
                continue;
            }

            switch (entity)
            {
                case Unit unit:
                    DeathSystem.Kill(world, unit, EntityId.None, PlayerId.Neutral, DeathCause.Defeat, -1, events);
                    break;
                case Building building when GarrisonSystem.Civilian(building):
                    GarrisonSystem.Abandon(world, building);
                    break;
                case Building building:
                    DeathSystem.Fall(world, building, DeathCause.Defeat, events);
                    break;
            }
        }

        for (var category = ProductionCategory.Infantry; category <= ProductionCategories.Last; category++)
        {
            state.QueueList(category).Clear();
        }
    }

    /// <summary>
    /// The player left a network match (docs/implementation/M9_network.md, 4.11, step 10): defeated as by <see cref="Defeat"/>, but what
    /// is theirs stays on the map, idle, as in RA2 — no ally takes it. Their units stop and forget their targets, their buildings their
    /// targets and repair, their queues are emptied; from then on nothing of theirs looks for a target or harvests
    /// (<see cref="GameWorld.IsDefeated"/>). The others may still destroy it.
    /// </summary>
    public static void Abandon(GameWorld world, PlayerId player, List<SimEvent>? events)
    {
        var state = world.Player(player);
        state.DefeatedTick = world.CurrentTick;
        events?.Add(new PlayerDefeated(world.CurrentTick, player));
        foreach (var entity in world.Entities)
        {
            if (!entity.IsAlive || entity.Owner != player)
            {
                continue;
            }

            switch (entity)
            {
                case Unit unit:
                    Movement.UnitOrders.Stop(world, unit);
                    Movement.UnitOrders.ForgetTarget(unit);
                    unit.Anchor = unit.Position;
                    break;
                case Building building:
                    building.Target = null;
                    building.TargetOrder = TargetOrder.None;
                    building.Repairing = false;
                    break;
            }
        }

        for (var category = ProductionCategory.Infantry; category <= ProductionCategories.Last; category++)
        {
            state.QueueList(category).Clear();
        }
    }

    /// <summary>Whether the player has nothing left that keeps them in the match.</summary>
    private static bool HasLost(GameWorld world, PlayerId player, bool shortGame)
    {
        foreach (var entity in world.Entities)
        {
            if (!entity.IsAlive || entity.Owner != player || entity is not (Unit or Building))
            {
                continue;
            }

            if (entity is Building building ? building.Def is { Wall: false } : !shortGame || world.Rules.IsMcv(((Unit)entity).Type))
            {
                return false;
            }
        }

        return true;
    }
}
