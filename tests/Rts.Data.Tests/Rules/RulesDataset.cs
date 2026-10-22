namespace Rts.Data.Tests.Rules;

/// <summary>The test dataset of the rules (docs/implementation/M3_base_economy.md, 6): the layout of game/data, with the definitions the tests need.</summary>
internal static class RulesDataset
{
    /// <summary>The folder of the dataset, found from the repository root, which is the first folder above the test binary with <c>RtsGame.sln</c>.</summary>
    public static string Folder => Path.Combine(RepoRoot(), "tests", "Rts.Data.Tests", "Rules", "Dataset");

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "RtsGame.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("No RtsGame.sln above the test binary.");
    }
}
