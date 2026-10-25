namespace Rts.Data.Tests;

/// <summary>
/// The rule of the asset registry (docs/assets_registry.md; docs/implementation/conventions.md, section 8; M6 step 14): every file of
/// <c>game/assets/</c> has its row in the registry — where it came from and under which licence — and every row a file. A file brought
/// in without a row fails CI. Godot's own <c>.import</c> and <c>.uid</c> files beside them are not assets.
/// </summary>
public sealed class AssetRegistryTests
{
    private const string AssetsFolder = "game/assets/";

    // The columns of a row: file, way, plan, date, prompt, reference, licence, source.
    private const int LicenceColumn = 6;

    private static readonly string Root = GameDataFiles.RepoRoot();

    private static readonly string[] NotAssets = [".import", ".uid", ".DS_Store"];

    private static List<string> Files() =>
        Directory.EnumerateFiles(Path.Combine(Root, AssetsFolder), "*", SearchOption.AllDirectories)
            .Where(file => !NotAssets.Any(file.EndsWith))
            .Select(file => Path.GetRelativePath(Root, file).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToList();

    /// <summary>The rows of the table: the path of the first cell and the cells.</summary>
    private static List<(string Path, string[] Cells)> Rows() =>
        File.ReadAllLines(Path.Combine(Root, "docs", "assets_registry.md"))
            .Where(line => line.StartsWith("| `" + AssetsFolder, StringComparison.Ordinal))
            .Select(line => line.Trim().Trim('|').Split('|').Select(cell => cell.Trim()).ToArray())
            .Select(cells => (cells[0].Trim('`'), cells))
            .ToList();

    [Fact]
    public void Every_file_of_the_assets_has_its_row()
    {
        var listed = Rows().Select(row => row.Path).ToHashSet(StringComparer.Ordinal);

        var missing = Files().Where(file => !listed.Contains(file)).ToList();

        Assert.True(missing.Count == 0, "Not in docs/assets_registry.md: " + string.Join(", ", missing));
    }

    [Fact]
    public void Every_row_is_of_a_file_that_is_there_once_with_its_licence()
    {
        var files = Files().ToHashSet(StringComparer.Ordinal);
        var rows = Rows();

        Assert.Empty(rows.Where(row => !files.Contains(row.Path)).Select(row => row.Path));
        Assert.Empty(rows.GroupBy(row => row.Path).Where(group => group.Count() > 1).Select(group => group.Key));
        Assert.Empty(rows.Where(row => row.Cells.Length <= LicenceColumn || row.Cells[LicenceColumn] is "" or "—").Select(row => row.Path));
    }
}
