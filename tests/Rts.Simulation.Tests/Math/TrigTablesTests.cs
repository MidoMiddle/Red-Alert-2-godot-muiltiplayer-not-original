using Rts.Simulation.Math;
using Rts.Simulation.Tests.Client;
using Rts.TrigGen;

namespace Rts.Simulation.Tests.Math;

/// <summary>
/// The committed tables are exactly what tools/Rts.TrigGen produces, so nobody edits them by hand.
/// After a deliberate generator change: run <c>dotnet run --project tools/Rts.TrigGen</c> and commit the file.
/// </summary>
public sealed class TrigTablesTests
{
    [Fact]
    public void Committed_file_matches_the_generator()
    {
        var path = Path.Combine(ClientFiles.RepoRoot, "src", "Rts.Simulation", "Math", "TrigTables.cs");
        // Git may check text out with CRLF on Windows; the content is what matters.
        var committed = File.ReadAllText(path).ReplaceLineEndings("\n");

        Assert.Equal(TrigTableGenerator.Render(), committed);
    }

    [Fact]
    public void Compiled_tables_match_the_generator()
    {
        Assert.Equal(TrigTableGenerator.SinQuarter(), TrigTables.SinQuarter);
        Assert.Equal(TrigTableGenerator.AtanTable(), TrigTables.Atan);
    }

    [Fact]
    public void Tables_have_the_planned_size_and_ends()
    {
        Assert.Equal(4097, TrigTables.SinQuarter.Length);
        Assert.Equal(0, TrigTables.SinQuarter[0]);
        Assert.Equal(65536, TrigTables.SinQuarter[^1]);

        Assert.Equal(1025, TrigTables.Atan.Length);
        Assert.Equal(0, TrigTables.Atan[0]);
        Assert.Equal(8192, TrigTables.Atan[^1]);
    }

    [Fact]
    public void Tables_grow_monotonically()
    {
        for (var i = 1; i < TrigTables.SinQuarter.Length; i++)
        {
            Assert.True(TrigTables.SinQuarter[i] >= TrigTables.SinQuarter[i - 1]);
        }

        for (var i = 1; i < TrigTables.Atan.Length; i++)
        {
            Assert.True(TrigTables.Atan[i] >= TrigTables.Atan[i - 1]);
        }
    }
}
