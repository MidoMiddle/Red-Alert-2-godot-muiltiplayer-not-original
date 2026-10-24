using Rts.Simulation.Rules;

namespace Rts.Simulation.World;

/// <summary>What a production item makes: a unit or a building from the data. Values go into commands: never renumber.</summary>
public enum ProductionKind : byte
{
    Unit = 1,
    Building = 2,
}

/// <summary>
/// One item of a production queue (docs/implementation/M3_base_economy.md, 4.10): the definition it makes, how far it is and what has
/// been paid for it. State: hashed.
/// </summary>
public sealed class ProductionItem
{
    internal ProductionItem(ProductionKind kind, int defIndex)
    {
        Kind = kind;
        DefIndex = defIndex;
    }

    public ProductionKind Kind { get; }

    /// <summary>The index of the definition in <see cref="RulesDatabase.Units"/> or <see cref="RulesDatabase.Buildings"/>.</summary>
    public int DefIndex { get; }

    /// <summary>Progress in ticks × 1000 (per mille of a tick), from 0 to the build time × 1000.</summary>
    public int Progress { get; internal set; }

    /// <summary>Credits paid so far: the cost times the share of progress, rounded down.</summary>
    public int Paid { get; internal set; }

    /// <summary>The player stopped it: it neither goes on nor costs anything.</summary>
    public bool Paused { get; internal set; }

    /// <summary>It stands still because the player cannot pay the next tick; it goes on by itself when the credits are there.</summary>
    public bool WaitingForFunds { get; internal set; }

    /// <summary>Finished: a building waits to be placed, a unit waits for the exit of its factory to be free.</summary>
    public bool Ready { get; internal set; }
}

/// <summary>The rules of production that follow from the data: build time, cost and category of an item.</summary>
public static class ProductionRules
{
    /// <summary>The most units a queue of infantry or vehicles holds (decision 1 of the M3 plan).</summary>
    public const int MaxUnitQueue = 15;

    /// <summary>The most items one command orders at once (Shift + click in RA2).</summary>
    public const int MaxOrder = 5;

    public static ProductionCategory CategoryOf(RulesDatabase rules, ProductionKind kind, int defIndex) => kind == ProductionKind.Unit
        ? rules.Units[defIndex].Category switch
        {
            UnitCategory.Infantry => ProductionCategory.Infantry,
            UnitCategory.Naval => ProductionCategory.Naval,
            UnitCategory.Aircraft => ProductionCategory.Aircraft,
            _ => ProductionCategory.Vehicle,
        }
        : rules.Buildings[defIndex].Category;

    /// <summary>The build time in ticks × 1000, the scale of <see cref="ProductionItem.Progress"/>.</summary>
    public static int TotalProgress(RulesDatabase rules, ProductionKind kind, int defIndex) =>
        (kind == ProductionKind.Unit ? rules.Units[defIndex].BuildTime : rules.Buildings[defIndex].BuildTime) * 1000;

    public static int CostOf(RulesDatabase rules, ProductionKind kind, int defIndex) =>
        kind == ProductionKind.Unit ? rules.Units[defIndex].Cost : rules.Buildings[defIndex].Cost;

    /// <summary>
    /// What <paramref name="player"/> pays for the item: its cost, less the player's discount for a vehicle, a ship or an aircraft
    /// (stage 2c), rounded down to a credit.
    /// </summary>
    public static int CostOf(RulesDatabase rules, PlayerState player, ProductionKind kind, int defIndex)
    {
        var cost = CostOf(rules, kind, defIndex);
        var discounted = kind == ProductionKind.Unit && rules.Units[defIndex].Category != UnitCategory.Infantry && player.VehicleDiscountPercent > 0;
        return discounted ? (int)((long)cost * (100 - player.VehicleDiscountPercent) / 100) : cost;
    }

    /// <summary>The id of the definition, such as <c>reactor</c>.</summary>
    public static string IdOf(RulesDatabase rules, ProductionKind kind, int defIndex) =>
        kind == ProductionKind.Unit ? rules.Units[defIndex].Id : rules.Buildings[defIndex].Id;

    /// <summary>True when the player has a ready building that produces <paramref name="category"/>.</summary>
    public static bool HasFactory(GameWorld world, PlayerId player, ProductionCategory category)
    {
        foreach (var entity in world.Entities)
        {
            if (entity is Building { IsAlive: true, IsReady: true, Def: { } def } building && building.Owner == player && def.Produces.Contains(category))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>True when the kind and index name a definition of the rules.</summary>
    public static bool IsKnown(RulesDatabase rules, ProductionKind kind, int defIndex) => kind switch
    {
        ProductionKind.Unit => defIndex >= 0 && defIndex < rules.Units.Length,
        ProductionKind.Building => defIndex >= 0 && defIndex < rules.Buildings.Length,
        _ => false,
    };
}
