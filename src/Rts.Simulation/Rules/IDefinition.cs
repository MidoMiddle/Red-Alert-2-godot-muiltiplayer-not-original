namespace Rts.Simulation.Rules;

/// <summary>A rule with a data id: the name that other definitions and map files use to refer to it (docs/implementation/M3_base_economy.md, 4.1).</summary>
public interface IDefinition
{
    string Id { get; }
}
