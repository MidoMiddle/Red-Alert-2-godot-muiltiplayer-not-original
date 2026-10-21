namespace Rts.Simulation.Tests.Client;

/// <summary>
/// Finds the Godot client's files from the test binary, so the checks work from any build folder.
/// The client itself is not loaded: these tests only read its text files.
/// </summary>
internal static class ClientFiles
{
    private static readonly Lazy<string> RootFolder = new(FindRepoRoot);

    /// <summary>The repository root: the first folder above the test binary that contains <c>RtsGame.sln</c>.</summary>
    public static string RepoRoot => RootFolder.Value;

    /// <summary>All files matching <paramref name="pattern"/> under <c>game/&lt;folder&gt;</c>, skipping Godot's cache.</summary>
    public static IEnumerable<string> FilesUnder(string folder, string pattern)
    {
        var path = Path.Combine(RepoRoot, "game", folder);
        if (!Directory.Exists(path))
        {
            return [];
        }

        var cache = Path.DirectorySeparatorChar + ".godot" + Path.DirectorySeparatorChar;
        return Directory.EnumerateFiles(path, pattern, SearchOption.AllDirectories)
            .Where(file => !file.Contains(cache, StringComparison.Ordinal));
    }

    /// <summary>A path relative to the repository root, with forward slashes, for readable messages.</summary>
    public static string ToRepoPath(string absolutePath) =>
        Path.GetRelativePath(RepoRoot, absolutePath).Replace(Path.DirectorySeparatorChar, '/');

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "RtsGame.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("RtsGame.sln was not found above the test binary.");
    }
}
