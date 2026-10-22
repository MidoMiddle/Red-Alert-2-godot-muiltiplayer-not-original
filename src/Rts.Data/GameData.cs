using System.Collections.Generic;
using Rts.Simulation.Rules;

namespace Rts.Data;

/// <summary>
/// The data files of the game, loaded for a match (docs/implementation/M3_base_economy.md, 4.1–4.5): the definitions, the rules the
/// simulation plays by and the hash that replays keep. On top of the checks of <see cref="RulesLoader"/>, the data must have what the
/// engine needs (<see cref="GameRules.Problems"/>): every unit type, locomotor and land type it knows, with the land codes of the map
/// format. Every mistake comes in one <see cref="RulesException"/>, at the place where it is written when it has one.
/// </summary>
public sealed record GameData(RulesDatabase Database, GameRules Rules, ulong Hash)
{
    /// <summary>The folder of the data below the repository root and below the Godot project: <c>game/data</c>, <c>res://data</c>.</summary>
    public const string Folder = "data";

    /// <exception cref="RulesException">The files have mistakes; all of them are listed.</exception>
    public static GameData Load(IEnumerable<(string FileName, string Text)> files)
    {
        var database = RulesLoader.Load(files, EngineCheck);
        return new GameData(database, GameRules.From(database), DataHasher.Compute(database));
    }

    /// <summary>Loads every <c>.yaml</c> file below <paramref name="root"/>, such as <c>game/data</c>, in the order of their paths.</summary>
    /// <exception cref="RulesException">The files have mistakes; all of them are listed.</exception>
    public static GameData LoadDirectory(string root) => Load(RulesLoader.FilesBelow(root));

    private static void EngineCheck(RulesDatabase database, SortedDictionary<string, Yaml.RawDefinition> sources, List<RulesError> errors)
    {
        foreach (var problem in GameRules.Problems(database))
        {
            errors.Add(RulesValidator.At(sources, problem.Id, problem.Key, problem.Reason));
        }
    }
}
