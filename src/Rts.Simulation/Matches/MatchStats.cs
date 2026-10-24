using System.Collections.Generic;
using Rts.Simulation.Events;
using Rts.Simulation.World;

namespace Rts.Simulation.Matches;

/// <summary>A point of a player's series: on the tick, the credits, the strength of the army and the buildings (walls aside).</summary>
public readonly record struct StatsSample(int Tick, int Credits, int ArmyStrength, int Buildings);

/// <summary>What one player did in the match (docs/implementation/M5_first_bot.md, 4.9).</summary>
public sealed class PlayerStats
{
    private readonly List<StatsSample> _samples = new();

    /// <summary>Units out of their factories; the units a match starts with do not count.</summary>
    public int UnitsBuilt { get; internal set; }

    /// <summary>The player's units destroyed, by anybody and anything — a defeat too.</summary>
    public int UnitsLost { get; internal set; }

    /// <summary>Units of other players the player's units and defenses destroyed.</summary>
    public int UnitsDestroyed { get; internal set; }

    /// <summary>Buildings placed — walls too, and yards deployed from an MCV: a deploy is a <see cref="BuildingPlaced"/> as well.</summary>
    public int BuildingsBuilt { get; internal set; }

    /// <summary>The player's buildings destroyed; a sold one is not lost.</summary>
    public int BuildingsLost { get; internal set; }

    /// <summary>Buildings of other players the player destroyed.</summary>
    public int BuildingsDestroyed { get; internal set; }

    /// <summary>Credits the harvesters brought in.</summary>
    public int CreditsHarvested { get; internal set; }

    /// <summary>Credits paid for production, repairs and walls, less what cancelled production gave back.</summary>
    public int CreditsSpent { get; internal set; }

    /// <summary>Credits sold buildings gave back. With the rest: credits now = at the start + harvested − spent + from sales.</summary>
    public int CreditsFromSales { get; internal set; }

    /// <summary>The series, one point every <see cref="MatchStats.SampleTicks"/> from tick 0, oldest first.</summary>
    public IReadOnlyList<StatsSample> Samples => _samples;

    internal void Sample(StatsSample sample) => _samples.Add(sample);
}

/// <summary>
/// The statistics of a match (docs/implementation/M5_first_bot.md, 4.9, step 11), of every player 1…8, in the sandbox too. Worked out
/// in the simulation, so they are the same on every machine and in a replay; not hashed — the game does not depend on them:
/// <list type="bullet">
/// <item>at the end of every tick, before the fallen leave the world (<see cref="Record"/>), from its events: <see cref="UnitProduced"/>,
/// <see cref="BuildingPlaced"/> (a deploy too), <see cref="EntityDestroyed"/> — lost by the owner, destroyed by
/// <see cref="EntityDestroyed.KillerOwner"/> when that is another player — <see cref="ProductionCancelled"/> and
/// <see cref="BuildingSold"/>;</item>
/// <item>the credits no event carries — the ore unloaded and the payments of production, repairs and walls — told by the systems that
/// move them (<see cref="Harvested"/>, <see cref="Spent"/>);</item>
/// <item>the series every <see cref="SampleTicks"/> ticks: credits, the strength of the army (cost × the share of hit points left of the
/// armed units, harvesters aside, as the bots count it), the buildings without walls.</item>
/// </list>
/// </summary>
public sealed class MatchStats
{
    /// <summary>30 seconds at 15 ticks a second.</summary>
    public const int SampleTicks = 450;

    private readonly PlayerStats[] _players = new PlayerStats[PlayerId.MaxPlayers];
    private int[]? _costs;

    internal MatchStats()
    {
        for (var i = 0; i < _players.Length; i++)
        {
            _players[i] = new PlayerStats();
        }
    }

    /// <summary>The ticks the match has been played, or lasted once it ended (<see cref="GameWorld.Result"/>).</summary>
    public int Duration { get; private set; }

    /// <summary>The statistics of a player 1…8.</summary>
    public PlayerStats Of(PlayerId player) => _players[player.Value - 1];

    /// <summary>A harvester of <paramref name="player"/> unloaded <paramref name="credits"/>.</summary>
    internal void Harvested(PlayerId player, int credits) => Of(player).CreditsHarvested += credits;

    /// <summary><paramref name="player"/> paid <paramref name="credits"/> for production, a repair or a wall.</summary>
    internal void Spent(PlayerId player, int credits) => Of(player).CreditsSpent += credits;

    /// <summary>The events of the tick just played, and a point of the series on its turn.</summary>
    internal void Record(GameWorld world, List<SimEvent> events)
    {
        foreach (var simEvent in events)
        {
            switch (simEvent)
            {
                case UnitProduced produced:
                    Of(produced.Player).UnitsBuilt++;
                    break;
                case BuildingPlaced placed:
                    Of(placed.Player).BuildingsBuilt++;
                    break;
                case EntityDestroyed destroyed when world.TryGet(destroyed.Entity, out var dead) && IsPlayer(dead.Owner):
                    Destroyed(dead, destroyed.KillerOwner);
                    break;
                case ProductionCancelled cancelled:
                    Of(cancelled.Player).CreditsSpent -= cancelled.Refund;
                    break;
                case BuildingSold sold:
                    Of(sold.Player).CreditsFromSales += sold.Refund;
                    break;
            }
        }

        var tick = world.CurrentTick;
        Duration = world.Result?.Tick ?? tick + 1;
        if (tick % SampleTicks == 0)
        {
            Sample(world, tick);
        }
    }

    private void Destroyed(Entity dead, PlayerId killer)
    {
        var lost = Of(dead.Owner);
        var other = IsPlayer(killer) && killer != dead.Owner ? Of(killer) : null;
        if (dead is Building)
        {
            lost.BuildingsLost++;
            if (other is not null)
            {
                other.BuildingsDestroyed++;
            }
        }
        else
        {
            lost.UnitsLost++;
            if (other is not null)
            {
                other.UnitsDestroyed++;
            }
        }
    }

    private void Sample(GameWorld world, int tick)
    {
        _costs ??= Costs(world);
        var army = new int[_players.Length];
        var buildings = new int[_players.Length];
        foreach (var entity in world.Entities)
        {
            if (!entity.IsAlive || !IsPlayer(entity.Owner))
            {
                continue;
            }

            var index = entity.Owner.Value - 1;
            switch (entity)
            {
                case Unit { Archetype.Armed: true, Harvester: null } unit when unit.MaxHp > 0:
                    army[index] += _costs[(int)unit.Type] * unit.Hp / unit.MaxHp;
                    break;
                case Building building when building.Def is { Wall: false }:
                    buildings[index]++;
                    break;
            }
        }

        for (var i = 0; i < _players.Length; i++)
        {
            _players[i].Sample(new StatsSample(tick, world.Player(new PlayerId(i + 1)).Credits, army[i], buildings[i]));
        }
    }

    /// <summary>The cost of every unit type, by its value.</summary>
    private static int[] Costs(GameWorld world)
    {
        var costs = new int[UnitTypes.All.Count + 1];
        foreach (var type in UnitTypes.All)
        {
            costs[(int)type] = world.Rules.Database.TryGetUnit(UnitTypes.IdOf(type), out var unit) ? unit.Cost : 0;
        }

        return costs;
    }

    private static bool IsPlayer(PlayerId player) => player.Value is >= 1 and <= PlayerId.MaxPlayers;
}
