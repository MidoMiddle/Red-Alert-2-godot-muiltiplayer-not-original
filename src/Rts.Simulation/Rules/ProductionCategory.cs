namespace Rts.Simulation.Rules;

/// <summary>
/// What a factory or the construction yard builds (docs/implementation/M3_base_economy.md, 4.10). A unit is infantry, a vehicle or
/// a ship (the shipyard, stage 2a), a building is a building or a defense; the production queues of M3 step 8 use all of them. Values
/// are part of the data: never renumber.
/// </summary>
public enum ProductionCategory : byte
{
    Infantry = 1,
    Vehicle = 2,
    Building = 3,
    Defense = 4,
    Naval = 5,
}

/// <summary>The production categories as a range: the queues of a player, one for each.</summary>
public static class ProductionCategories
{
    public const ProductionCategory First = ProductionCategory.Infantry;
    public const ProductionCategory Last = ProductionCategory.Naval;
    public const int Count = (int)Last;
}
