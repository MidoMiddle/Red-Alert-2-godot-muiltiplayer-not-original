namespace Rts.Simulation.Rules;

/// <summary>
/// What a factory or the construction yard builds (docs/implementation/M3_base_economy.md, 4.10). A unit is infantry or vehicle, a
/// building is a building or a defense; the production queues of M3 step 8 use all four. Values are part of the data: never renumber.
/// </summary>
public enum ProductionCategory : byte
{
    Infantry = 1,
    Vehicle = 2,
    Building = 3,
    Defense = 4,
}
