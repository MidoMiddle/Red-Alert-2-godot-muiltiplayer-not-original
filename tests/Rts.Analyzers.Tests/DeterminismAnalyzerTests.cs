using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Rts.Analyzers;

namespace Rts.Analyzers.Tests;

public sealed class DeterminismAnalyzerTests
{
    [Theory]
    [InlineData("float x = 0;")]
    [InlineData("double x = 0;")]
    [InlineData("decimal x = 0;")]
    [InlineData("var x = 0.5;")]
    [InlineData("var x = 1f;")]
    [InlineData("var x = 2m;")]
    [InlineData("var x = (int)(object)1.0;")]
    public void Floating_point_is_reported(string statement)
    {
        var ids = Analyze(WrapInMethod(statement));

        Assert.Contains(DeterminismAnalyzer.FloatingPointId, ids);
    }

    [Fact]
    public void Floating_point_in_signatures_is_reported()
    {
        var ids = Analyze("""
            class C
            {
                double _field;
                float Method(decimal value) => 0;
            }
            """);

        // double field, float return type, decimal parameter.
        Assert.Equal(3, ids.Count(id => id == DeterminismAnalyzer.FloatingPointId));
    }

    [Theory]
    [InlineData("var d = new Dictionary<int, int>(); foreach (var kv in d) { }")]
    [InlineData("var d = new Dictionary<int, int>(); foreach (var k in d.Keys) { }")]
    [InlineData("var d = new Dictionary<int, int>(); foreach (var v in d.Values) { }")]
    [InlineData("var s = new HashSet<int>(); foreach (var x in s) { }")]
    [InlineData("var s = new HashSet<int>(); var l = s.Select(x => x).ToList();")]
    [InlineData("var s = new HashSet<int>(); var l = new List<int>(s);")]
    public void Unordered_enumeration_is_reported(string statement)
    {
        var ids = Analyze(WrapInMethod(statement));

        Assert.Contains(DeterminismAnalyzer.UnorderedEnumerationId, ids);
    }

    [Theory]
    [InlineData("int x = 5 / 2; long y = x * 3L;")]
    [InlineData("var d = new Dictionary<int, int>(); d[1] = 2; var ok = d.TryGetValue(1, out var v) && d.ContainsKey(1);")]
    [InlineData("var s = new HashSet<int>(); var has = s.Contains(1) && s.Count > 0;")]
    [InlineData("var l = new List<int>(); foreach (var x in l) { }")]
    [InlineData("var d = new SortedDictionary<int, int>(); foreach (var kv in d) { }")]
    public void Deterministic_code_is_not_reported(string statement)
    {
        var ids = Analyze(WrapInMethod(statement));

        Assert.Empty(ids);
    }

    private static string WrapInMethod(string statement) => $$"""
        using System.Collections.Generic;
        using System.Linq;

        class C
        {
            void M()
            {
                {{statement}}
            }
        }
        """;

    private static IReadOnlyList<string> Analyze(string source)
    {
        var compilation = CSharpCompilation.Create(
            "AnalyzerTest",
            [CSharpSyntaxTree.ParseText(source)],
            PlatformReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var compileErrors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.True(compileErrors.Count == 0, "Test source does not compile: " + string.Join("; ", compileErrors));

        var diagnostics = compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new DeterminismAnalyzer()))
            .GetAnalyzerDiagnosticsAsync()
            .GetAwaiter()
            .GetResult();

        return diagnostics.Select(d => d.Id).ToList();
    }

    private static IEnumerable<MetadataReference> PlatformReferences()
    {
        var paths = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);
        return paths.Select(path => MetadataReference.CreateFromFile(path));
    }
}
