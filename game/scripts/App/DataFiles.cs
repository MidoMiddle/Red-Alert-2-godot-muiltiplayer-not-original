using System.Collections.Generic;
using Godot;
using Rts.Data;

namespace Rts.Client.App;

/// <summary>
/// The data files of the game: <c>game/data</c>, <c>res://data</c> inside the project (docs/implementation/M3_base_economy.md, 4.1).
/// Read through Godot's file access, so they work from the exported game too, and loaded once, at the first use. A mistake in them
/// stops the game: every error goes to the Godot console with its file and line, then <see cref="RulesException"/> is thrown.
/// </summary>
public static class DataFiles
{
    public const string Root = "res://" + GameData.Folder;

    private static GameData? _current;

    /// <summary>The loaded data.</summary>
    /// <exception cref="RulesException">The files have mistakes; each is printed before.</exception>
    public static GameData Current => _current ??= Load();

    /// <summary>Loads the data if it is not loaded; false, after printing every error, when the files have mistakes.</summary>
    public static bool TryLoad()
    {
        try
        {
            _ = Current;
            return true;
        }
        catch (RulesException)
        {
            return false;
        }
    }

    private static GameData Load()
    {
        var files = new List<(string FileName, string Text)>();
        Collect(Root, "", files);
        files.Sort((a, b) => string.CompareOrdinal(a.FileName, b.FileName));
        try
        {
            var data = GameData.Load(files);
            GD.Print($"[data] {files.Count} files, {data.Database.Units.Length} units, {data.Database.Buildings.Length} buildings, hash {data.Hash:x16}");
            return data;
        }
        catch (RulesException e)
        {
            foreach (var error in e.Errors)
            {
                GD.PushError(error.FileName.Length == 0 ? $"[data] {error.Reason}" : $"[data] {Root}/{error}");
            }

            GD.PushError($"[data] {e.Errors.Count} errors in {Root}: the game cannot start");
            throw;
        }
    }

    /// <summary>Every <c>.yaml</c> file below <paramref name="folder"/>, named by its path relative to <see cref="Root"/>.</summary>
    private static void Collect(string folder, string prefix, List<(string FileName, string Text)> files)
    {
        foreach (var name in DirAccess.GetFilesAt(folder))
        {
            if (name.EndsWith(".yaml", System.StringComparison.Ordinal))
            {
                files.Add((prefix + name, Godot.FileAccess.GetFileAsString($"{folder}/{name}")));
            }
        }

        foreach (var name in DirAccess.GetDirectoriesAt(folder))
        {
            // The sounds and the climate looks are not rules (RulesLoader.NotRulesFolders).
            if (prefix.Length == 0 && System.Array.IndexOf(RulesLoader.NotRulesFolders, name) >= 0)
            {
                continue;
            }

            Collect($"{folder}/{name}", $"{prefix}{name}/", files);
        }
    }
}
