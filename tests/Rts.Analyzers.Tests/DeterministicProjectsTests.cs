using System.Collections.Immutable;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Rts.Simulation;

namespace Rts.Analyzers.Tests;

/// <summary>
/// The projects under the determinism rules (docs/implementation/conventions.md, section 1): their project file turns the rules on, so
/// the build runs the banned API list and <see cref="DeterminismAnalyzer"/> over them, and they do not reference Godot. For the bots
/// (docs/implementation/M5_first_bot.md, 4.1, 6) also: their real sources pass the analyzer, and a <c>double</c> among them does not.
/// </summary>
public sealed class DeterministicProjectsTests
{
    private static readonly string Root = RepoRoot();

    [Theory]
    [InlineData("src/Rts.Simulation/Rts.Simulation.csproj")]
    [InlineData("src/Rts.Data/Rts.Data.csproj")]
    [InlineData("src/Rts.MapGen/Rts.MapGen.csproj")]
    [InlineData("src/Rts.AI/Rts.AI.csproj")]
    public void The_project_turns_the_determinism_rules_on_and_knows_no_godot(string path)
    {
        var project = XDocument.Load(Path.Combine(Root, path));

        Assert.Contains(project.Descendants("RtsDeterministic"), flag => flag.Value == "true");
        // Neither the SDK nor any reference: comments may name Godot.
        var godot = project.Descendants()
            .Where(e => e.Name.LocalName is "Project" or "PackageReference" or "ProjectReference" or "Reference")
            .SelectMany(e => e.Attributes())
            .Where(attribute => attribute.Name.LocalName is "Sdk" or "Include")
            .Where(attribute => attribute.Value.Contains("Godot", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(godot);
    }

    [Fact]
    public void The_sources_of_the_bots_pass_the_analyzer()
    {
        Assert.Empty(AnalyzeBots());
    }

    [Fact]
    public void A_double_among_the_sources_of_the_bots_is_reported()
    {
        var ids = AnalyzeBots("namespace Rts.AI; internal static class Guess { internal static int Half(int x) => (int)(x * 0.5); }");

        Assert.Contains(DeterminismAnalyzer.FloatingPointId, ids);
    }

    /// <summary>The diagnostics of <see cref="DeterminismAnalyzer"/> over every source of src/Rts.AI, with <paramref name="extra"/>.</summary>
    private static IReadOnlyList<string> AnalyzeBots(string? extra = null)
    {
        var sources = Directory.GetFiles(Path.Combine(Root, "src", "Rts.AI"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(path => File.ReadAllText(path))
            .Append(extra ?? "")
            .Select(text => CSharpSyntaxTree.ParseText(text))
            .ToList();
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path))
            .Append(MetadataReference.CreateFromFile(typeof(GameSimulation).Assembly.Location));
        var compilation = CSharpCompilation.Create("Rts.AI", sources, references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.True(errors.Count == 0, "The sources of the bots do not compile: " + string.Join("; ", errors));

        return compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new DeterminismAnalyzer()))
            .GetAnalyzerDiagnosticsAsync()
            .GetAwaiter()
            .GetResult()
            .Select(d => d.Id)
            .ToList();
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "RtsGame.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("RtsGame.sln is not above " + AppContext.BaseDirectory);
    }
}
