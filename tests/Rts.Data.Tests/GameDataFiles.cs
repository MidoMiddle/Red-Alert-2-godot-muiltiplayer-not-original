using Rts.Simulation.Rules;

namespace Rts.Data.Tests;

/// <summary>
/// The data files of the game, <c>game/data</c>, loaded once for the tests that play maps by the real rules
/// (docs/implementation/M3_base_economy.md, 4.5). Linked into Rts.MapGen.Tests too.
/// </summary>
internal static class GameDataFiles
{
    /// <summary>The folder, found from the repository root: the first folder above the test binary with <c>RtsGame.sln</c>.</summary>
    public static string Folder { get; } = Path.Combine(RepoRoot(), "game", GameData.Folder);

    public static GameData Current { get; } = GameData.LoadDirectory(Folder);

    public static GameRules Rules => Current.Rules;

    /// <summary>The root of the repository: the first folder above the test binary with <c>RtsGame.sln</c>.</summary>
    public static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "RtsGame.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("No RtsGame.sln above the test binary.");
    }
}
