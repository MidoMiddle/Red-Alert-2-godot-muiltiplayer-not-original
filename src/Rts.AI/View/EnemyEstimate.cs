using System.Collections.Generic;
using System.Collections.Immutable;
using Rts.Simulation.Math;
using Rts.Simulation.Rules;
using Rts.Simulation.World;

namespace Rts.AI.View;

/// <summary>
/// What a bot thinks one enemy has (docs/implementation/M8_ai_difficulty.md, 4.6, step 6). The counts are weighted by how fresh the
/// sighting is and given in hundredths of a unit — 250 is two and a half — so that old knowledge fades rather than drops.
/// </summary>
/// <param name="Units">The units but the harvesters, weighted, in hundredths.</param>
/// <param name="Infantry">Of them infantry, in hundredths.</param>
/// <param name="Vehicles">Of them vehicles, in hundredths.</param>
/// <param name="Harvesters">The harvesters, weighted, in hundredths.</param>
/// <param name="ArmyStrength">The strength of the armed units — cost × the share of hit points left — weighted.</param>
/// <param name="Refineries">The refineries the bot remembers.</param>
/// <param name="TechLevel">How deep in the prerequisites the deepest building the bot remembers stands: 0 — a yard or none.</param>
/// <param name="Buildings">The buildings the bot remembers.</param>
/// <param name="LastSeenTick">The last tick anything of the enemy was seen.</param>
/// <param name="Composition">The units by type, weighted, in hundredths.</param>
public sealed record EnemyPicture(
    PlayerId Player, int Units, int Infantry, int Vehicles, int Harvesters, int ArmyStrength, int Refineries, int TechLevel, int Buildings,
    int LastSeenTick, ImmutableSortedDictionary<UnitType, int> Composition)
{
    /// <summary>The weight of <paramref name="type"/> in the army, in hundredths.</summary>
    public int Of(UnitType type) => Composition.TryGetValue(type, out var weight) ? weight : 0;
}

/// <summary>
/// What a bot knows of every enemy (docs/implementation/M8_ai_difficulty.md, 4.6, step 6), from what it has seen only — through its
/// view, so late by its perception, and all of the map for «Brutal», whose player sees everything:
/// <list type="bullet">
/// <item>the units, each by its last sighting: whole for <see cref="FreshTicks"/>, then fading to nothing at <see cref="ForgetTicks"/>,
/// when it is forgotten — the bot does not see units die, so old knowledge only fades; seen again, it is fresh;</item>
/// <item>the buildings the <see cref="AiMemory"/> remembers: their count, the refineries among them — a building that is the refinery of
/// a faction of the data — and the level of technology, the deepest of them in the prerequisites;</item>
/// <item>when anything of the enemy was seen last.</item>
/// </list>
/// Kept by the memory and brought up to date with it on every turn of the bot. The counters of step 7 read the composition.
/// </summary>
public sealed class EnemyEstimate
{
    /// <summary>A sighting this recent counts whole: 60 s.</summary>
    public const int FreshTicks = 900;

    /// <summary>A sighting this old counts nothing and is forgotten: 3 min.</summary>
    public const int ForgetTicks = 2700;

    private readonly SortedDictionary<int, (PlayerId Owner, UnitType Type, bool Infantry, bool Harvester, bool Armed, int Strength, int SeenTick)> _units = new();
    private readonly SortedDictionary<int, EnemyPicture> _players = new();
    private readonly List<int> _forgotten = new();
    private int[]? _depths;
    private bool[]? _refineries;

    /// <summary>The enemies the bot knows anything of, by player.</summary>
    public IReadOnlyCollection<EnemyPicture> Players => _players.Values;

    /// <summary>Every enemy together; player 0 — neutral, for none in particular.</summary>
    public EnemyPicture Total { get; private set; } = Empty(PlayerId.Neutral);

    /// <summary>What the bot knows of <paramref name="player"/>: nothing at all for an enemy it has not seen.</summary>
    public EnemyPicture Of(PlayerId player) => _players.TryGetValue(player.Value, out var picture) ? picture : Empty(player);

    /// <summary>How much a sighting <paramref name="age"/> ticks old counts, in percent.</summary>
    public static int Weight(int age) =>
        age <= FreshTicks ? 100 : age >= ForgetTicks ? 0 : 100 * (ForgetTicks - age) / (ForgetTicks - FreshTicks);

    internal void Update(AiWorldView view, AiMemory memory)
    {
        var tick = view.Tick;
        foreach (var enemy in view.VisibleEnemies)
        {
            if (enemy is Unit unit)
            {
                _units[unit.Id.Value] = (unit.Owner, unit.Type, unit.Archetype.Category == UnitCategory.Infantry, unit.Harvester is not null,
                    unit.Archetype.Armed, memory.StrengthOf(unit), tick);
            }
        }

        _forgotten.Clear();
        foreach (var (id, sighting) in _units)
        {
            if (tick - sighting.SeenTick >= ForgetTicks)
            {
                _forgotten.Add(id);
            }
        }

        foreach (var id in _forgotten)
        {
            _units.Remove(id);
        }

        Pictures(view.Rules.Database, memory, tick);
    }

    private void Pictures(RulesDatabase rules, AiMemory memory, int tick)
    {
        var tallies = new SortedDictionary<int, Tally>();
        Tally Of(PlayerId player)
        {
            if (!tallies.TryGetValue(player.Value, out var tally))
            {
                tally = new Tally();
                tallies[player.Value] = tally;
            }

            return tally;
        }

        var total = new Tally();
        foreach (var sighting in _units.Values)
        {
            var weight = Weight(tick - sighting.SeenTick);
            foreach (var tally in new[] { Of(sighting.Owner), total })
            {
                tally.LastSeen = IntMath.Max(tally.LastSeen, sighting.SeenTick);
                if (sighting.Harvester)
                {
                    tally.Harvesters += weight;
                    continue;
                }

                tally.Units += weight;
                tally.Infantry += sighting.Infantry ? weight : 0;
                tally.Vehicles += sighting.Infantry ? 0 : weight;
                tally.Strength += sighting.Armed ? sighting.Strength * weight / 100 : 0;
                tally.Composition[sighting.Type] = tally.Composition.GetValueOrDefault(sighting.Type) + weight;
            }
        }

        foreach (var building in memory.Buildings)
        {
            foreach (var tally in new[] { Of(building.Owner), total })
            {
                tally.LastSeen = IntMath.Max(tally.LastSeen, building.SeenTick);
                tally.Buildings++;
                tally.Refineries += IsRefinery(rules, building.DefIndex) ? 1 : 0;
                tally.Tech = IntMath.Max(tally.Tech, Depth(rules, building.DefIndex));
            }
        }

        _players.Clear();
        foreach (var (player, tally) in tallies)
        {
            _players[player] = tally.Picture(new PlayerId(player));
        }

        Total = total.Picture(PlayerId.Neutral);
    }

    private bool IsRefinery(RulesDatabase rules, int building)
    {
        if (_refineries is null)
        {
            _refineries = new bool[rules.Buildings.Length];
            foreach (var faction in rules.Factions)
            {
                var index = rules.IndexOfBuilding(faction.Refinery);
                if (index >= 0)
                {
                    _refineries[index] = true;
                }
            }
        }

        return _refineries[building];
    }

    /// <summary>How deep <paramref name="building"/> stands in the prerequisites: 0 without any, else one deeper than its deepest.</summary>
    private int Depth(RulesDatabase rules, int building)
    {
        _depths ??= new int[rules.Buildings.Length];
        if (_depths[building] > 0)
        {
            return _depths[building] - 1;
        }

        // Marked while it is worked out, so that a cycle — the data forbids them — cannot loop.
        _depths[building] = 1;
        var depth = 0;
        foreach (var id in rules.Buildings[building].Prerequisites)
        {
            var index = rules.IndexOfBuilding(id);
            if (index >= 0)
            {
                depth = IntMath.Max(depth, Depth(rules, index) + 1);
            }
        }

        _depths[building] = depth + 1;
        return depth;
    }

    private static EnemyPicture Empty(PlayerId player) => new(player, 0, 0, 0, 0, 0, 0, 0, 0, -1, ImmutableSortedDictionary<UnitType, int>.Empty);

    private sealed class Tally
    {
        public int Units;
        public int Infantry;
        public int Vehicles;
        public int Harvesters;
        public int Strength;
        public int Refineries;
        public int Tech;
        public int Buildings;
        public int LastSeen = -1;
        public readonly SortedDictionary<UnitType, int> Composition = new();

        public EnemyPicture Picture(PlayerId player) => new(
            player, Units, Infantry, Vehicles, Harvesters, Strength, Refineries, Tech, Buildings, LastSeen, Composition.ToImmutableSortedDictionary());
    }
}
