using System.Text.RegularExpressions;

namespace Rts.Simulation.Tests.Client;

/// <summary>
/// Scenes and <c>project.godot</c> are hand-written text, and CI never loads them in Godot.
/// This check catches a broken <c>res://</c> path (scene, script, theme or autoload) without the engine.
/// </summary>
public sealed class ClientResourcesTests
{
    private const string ResPrefix = "res://";

    private static readonly Regex ResPath = new(@"res://[^""\s\)\]]+", RegexOptions.CultureInvariant);

    [Fact]
    public void Resource_paths_in_project_and_scenes_point_to_existing_files()
    {
        var sources = new List<string> { Path.Combine(ClientFiles.RepoRoot, "game", "project.godot") };
        sources.AddRange(ClientFiles.FilesUnder(string.Empty, "*.tscn"));

        var checkedPaths = 0;
        var missing = new List<string>();
        foreach (var source in sources)
        {
            foreach (Match match in ResPath.Matches(File.ReadAllText(source)))
            {
                checkedPaths++;
                var target = Path.Combine(ClientFiles.RepoRoot, "game", match.Value[ResPrefix.Length..]);
                if (!File.Exists(target))
                {
                    missing.Add($"{ClientFiles.ToRepoPath(source)}: {match.Value}");
                }
            }
        }

        Assert.True(checkedPaths > 0, "no res:// paths were found, so the check would pass vacuously");
        Assert.Empty(missing);
    }
}
