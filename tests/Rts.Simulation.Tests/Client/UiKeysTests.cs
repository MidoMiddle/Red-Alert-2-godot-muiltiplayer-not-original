namespace Rts.Simulation.Tests.Client;

/// <summary>
/// Player-visible text in the client goes through localization keys (docs/implementation/M6_menus_skirmish.md, 4.13).
/// The debug overlay and the main scene are excluded on purpose: they are for developers, not for players.
/// </summary>
public sealed class UiKeysTests
{
    private static readonly string[] ExcludedFiles = ["scripts/DebugOverlay.cs", "scenes/Main.tscn"];

    [Fact]
    public void Visible_text_in_scenes_and_scripts_comes_from_keys()
    {
        var violations = new List<string>();

        foreach (var file in SceneFiles())
        {
            violations.AddRange(UiTextScanner.ScanScene(ClientFiles.ToRepoPath(file), File.ReadAllText(file)));
        }

        foreach (var file in CodeFiles())
        {
            violations.AddRange(UiTextScanner.ScanCode(ClientFiles.ToRepoPath(file), File.ReadAllText(file)));
        }

        Assert.Empty(violations);
    }

    [Fact]
    public void Every_key_used_in_scenes_and_scripts_exists_in_en_csv()
    {
        var used = UsedKeys();
        Assert.NotEmpty(used);

        var known = EnCsvKeys();
        var missing = used.Except(known).Order().ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public void En_csv_keys_are_unique_and_upper_snake_case()
    {
        var keys = EnCsvKeys();
        Assert.NotEmpty(keys);

        var duplicates = keys.GroupBy(key => key).Where(group => group.Count() > 1).Select(group => group.Key).ToList();
        var malformed = keys.Where(key => !UiTextScanner.IsKey(key)).ToList();

        Assert.Empty(duplicates);
        Assert.Empty(malformed);
    }

    [Fact]
    public void Scanner_reports_literal_text_in_code()
    {
        Assert.Single(UiTextScanner.ScanCode("fixture.cs", "label.Text = \"Back\";"));
        Assert.Single(UiTextScanner.ScanCode("fixture.cs", "option.AddItem(\"First\");"));
        Assert.Empty(UiTextScanner.ScanCode("fixture.cs", "label.Text = Tr(\"UI_BACK\");"));
        Assert.Empty(UiTextScanner.ScanCode("fixture.cs", "option.AddItem(Tr(\"UI_FIRST\"));"));
        Assert.Empty(UiTextScanner.ScanCode("fixture.cs", "label.Text = \"\";"));
    }

    [Fact]
    public void Scanner_reports_literal_text_in_scenes()
    {
        Assert.Single(UiTextScanner.ScanScene("fixture.tscn", "text = \"Back\""));
        Assert.Empty(UiTextScanner.ScanScene("fixture.tscn", "text = \"UI_BACK\""));
        Assert.Empty(UiTextScanner.ScanScene("fixture.tscn", "text = \"\""));
        Assert.Empty(UiTextScanner.ScanScene("fixture.tscn", "name = \"Back\""));
    }

    private static IEnumerable<string> SceneFiles() =>
        ClientFiles.FilesUnder("scenes", "*.tscn").Where(IsIncluded);

    private static IEnumerable<string> CodeFiles() =>
        ClientFiles.FilesUnder("scripts", "*.cs").Where(IsIncluded);

    private static bool IsIncluded(string file)
    {
        var path = ClientFiles.ToRepoPath(file);
        return !ExcludedFiles.Any(excluded => path.EndsWith("game/" + excluded, StringComparison.Ordinal));
    }

    private static List<string> UsedKeys()
    {
        var keys = new List<string>();
        foreach (var file in SceneFiles())
        {
            keys.AddRange(UiTextScanner.KeysInScene(File.ReadAllText(file)));
        }

        foreach (var file in CodeFiles())
        {
            keys.AddRange(UiTextScanner.KeysInCode(File.ReadAllText(file)));
        }

        return keys.Distinct().ToList();
    }

    /// <summary>The first column of <c>game/localization/en.csv</c>, without the header row.</summary>
    private static List<string> EnCsvKeys()
    {
        var path = Path.Combine(ClientFiles.RepoRoot, "game", "localization", "en.csv");
        return File.ReadAllLines(path)
            .Skip(1)
            .Where(line => line.Contains(','))
            .Select(line => line[..line.IndexOf(',')])
            .ToList();
    }
}
