using System.Reflection;
using Rts.Simulation;

namespace Rts.Simulation.Tests;

/// <summary>
/// Guards the project structure: the simulation must stay a pure .NET library.
/// </summary>
public sealed class ProjectSetupTests
{
    private static readonly Assembly SimulationAssembly = typeof(SimulationVersion).Assembly;

    [Fact]
    public void Simulation_does_not_reference_Godot()
    {
        var godotReferences = SimulationAssembly.GetReferencedAssemblies()
            .Where(name => name.Name!.StartsWith("Godot", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Empty(godotReferences);
    }

    [Fact]
    public void Simulation_references_only_base_class_library()
    {
        var foreignReferences = SimulationAssembly.GetReferencedAssemblies()
            .Select(name => name.Name!)
            .Where(name => !name.StartsWith("System", StringComparison.Ordinal)
                && name is not "netstandard" and not "mscorlib")
            .ToList();

        Assert.Empty(foreignReferences);
    }
}
