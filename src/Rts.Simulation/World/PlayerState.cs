using System.Collections.Generic;
using Rts.Simulation.Rules;

namespace Rts.Simulation.World;

/// <summary>
/// The economy of one player (docs/implementation/M3_base_economy.md, 4.7): credits, production queues, power and what the player may
/// build. <see cref="Credits"/> and the queues are state; the rest follows from the player's buildings, worked out again by
/// <see cref="Systems.PowerSystem"/> and <see cref="Systems.TechTree"/> when they change, so it is not hashed. Every player 1…8 has one
/// from the start of the match; the neutral player has no economy.
/// </summary>
public sealed class PlayerState
{
    private readonly bool[] _units;
    private readonly bool[] _buildings;

    // One queue per ProductionCategory, in its order (infantry, vehicle, building, defense).
    private readonly List<ProductionItem>[] _queues = [[], [], [], []];

    internal PlayerState(PlayerId id, FactionDef faction, int credits, RulesDatabase rules)
    {
        Id = id;
        Faction = faction;
        Credits = credits;
        _units = new bool[rules.Units.Length];
        _buildings = new bool[rules.Buildings.Length];
    }

    public PlayerId Id { get; }

    /// <summary>The faction of the player: what it builds. The first faction of the data until the skirmish setup chooses (M6).</summary>
    public FactionDef Faction { get; }

    public int Credits { get; internal set; }

    /// <summary>The player's buildings that stand, ready or growing, in id order.</summary>
    public IReadOnlyList<EntityId> Buildings => BuildingList;

    /// <summary>Power the ready buildings produce: the sum of their positive <c>power</c>.</summary>
    public int PowerOutput { get; internal set; }

    /// <summary>Power the ready buildings drain: the sum of their negative <c>power</c>, as a positive number.</summary>
    public int PowerDrain { get; internal set; }

    /// <summary>The player drains more than it produces (4.11).</summary>
    public bool LowPower { get; internal set; }

    internal List<EntityId> BuildingList { get; } = [];

    /// <summary>
    /// The production queue of a category (4.10): the first item is in work, the rest wait. Buildings and defenses hold one item,
    /// infantry and vehicles up to <see cref="ProductionRules.MaxUnitQueue"/>.
    /// </summary>
    public IReadOnlyList<ProductionItem> Queue(ProductionCategory category) => QueueList(category);

    internal List<ProductionItem> QueueList(ProductionCategory category) => _queues[(int)category - 1];

    /// <summary>Whether the unit at <paramref name="index"/> of <see cref="RulesDatabase.Units"/> may be built now.</summary>
    public bool CanBuildUnit(int index) => _units[index];

    /// <summary>Whether the building at <paramref name="index"/> of <see cref="RulesDatabase.Buildings"/> may be built now.</summary>
    public bool CanBuildBuilding(int index) => _buildings[index];

    /// <summary>How many unit and building types may be built now.</summary>
    public (int Units, int Buildings) AvailableCount => (Count(_units), Count(_buildings));

    internal void SetAvailable(bool[] units, bool[] buildings)
    {
        units.CopyTo(_units, 0);
        buildings.CopyTo(_buildings, 0);
    }

    private static int Count(bool[] flags)
    {
        var count = 0;
        foreach (var flag in flags)
        {
            count += flag ? 1 : 0;
        }

        return count;
    }
}
