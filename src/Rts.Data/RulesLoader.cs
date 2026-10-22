using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rts.Data.Yaml;
using Rts.Simulation.Rules;

namespace Rts.Data;

/// <summary>
/// Loads the rules of a match from the data files (docs/implementation/M3_base_economy.md, 4.1, 4.3): reads every file, checks that the ids
/// are unique, resolves <c>inherits</c>, reads each definition by its kind, builds the <see cref="RulesDatabase"/> and checks it with
/// <see cref="RulesValidator"/>. A mistake does not stop the load: the rest is read on, and every mistake found is reported together in a
/// <see cref="RulesException"/>. The meaning of the rules is checked only when every definition could be read, so that a definition left
/// out does not show up again as a broken reference.
/// </summary>
public static class RulesLoader
{
    /// <summary>Loads files given as (name, text) pairs. The name is what the errors show.</summary>
    /// <exception cref="RulesException">The files have mistakes; all of them are listed.</exception>
    public static RulesDatabase Load(IEnumerable<(string FileName, string Text)> files) => Load(files, check: null);

    /// <summary>
    /// <see cref="Load(IEnumerable{ValueTuple{string, string}})"/> with one more check of the meaning, which runs after the validator
    /// when it finds nothing: <see cref="GameData"/> adds what the engine needs. It gets the rules, the resolved entry of each by id
    /// and the list of errors to add to.
    /// </summary>
    internal static RulesDatabase Load(
        IEnumerable<(string FileName, string Text)> files,
        Action<RulesDatabase, SortedDictionary<string, RawDefinition>, List<RulesError>>? check)
    {
        var errors = new List<RulesError>();
        var fileNames = new List<string>();
        var (rules, sources) = Read(files, fileNames, errors);
        if (errors.Count == 0)
        {
            RulesValidator.Check(rules, sources, errors);
            if (errors.Count == 0)
            {
                check?.Invoke(rules, sources, errors);
            }

            if (errors.Count == 0)
            {
                return rules;
            }
        }

        throw new RulesException(InFileOrder(errors, fileNames));
    }

    /// <summary>
    /// Reads the files into rules without checking their meaning: the part of <see cref="Load"/> that tests of the schema and of
    /// inheritance use on their own. Every mistake goes to <paramref name="errors"/>, every file name to <paramref name="fileNames"/>.
    /// </summary>
    /// <returns>The rules that could be read, and the resolved entry of each of them by id.</returns>
    internal static (RulesDatabase Rules, SortedDictionary<string, RawDefinition> Sources) Read(
        IEnumerable<(string FileName, string Text)> files,
        List<string> fileNames,
        List<RulesError> errors)
    {
        var definitions = new List<RawDefinition>();
        var ids = new SortedDictionary<string, RawDefinition>(StringComparer.Ordinal);
        foreach (var (fileName, text) in files)
        {
            fileNames.Add(fileName);
            foreach (var definition in YamlFile.Read(fileName, text, errors))
            {
                if (ids.TryGetValue(definition.Id, out var first))
                {
                    var (line, column) = ((int)definition.IdNode.Start.Line, (int)definition.IdNode.Start.Column);
                    errors.Add(new RulesError(definition.FileName, line, column, $"'{definition.Id}' is already defined in {first.FileName}"));
                    continue;
                }

                ids.Add(definition.Id, definition);
                definitions.Add(definition);
            }
        }

        var builder = new RulesBuilder();
        var sources = new SortedDictionary<string, RawDefinition>(StringComparer.Ordinal);
        foreach (var definition in Inheritance.Resolve(definitions, errors))
        {
            try
            {
                if (Add(builder, definition))
                {
                    sources.Add(definition.Id, definition);
                }
            }
            catch (RulesFormatException e)
            {
                errors.Add(e.Error);
            }
        }

        return (builder.Build(), sources);
    }

    /// <summary>
    /// Loads every <c>.yaml</c> file below <paramref name="root"/>. The files are read in the order of their relative paths, so the errors
    /// come in the same order on every system.
    /// </summary>
    public static RulesDatabase LoadDirectory(string root) => Load(FilesBelow(root));

    /// <summary>Every <c>.yaml</c> file below <paramref name="root"/> as (relative path with '/', text), in path order.</summary>
    internal static List<(string FileName, string Text)> FilesBelow(string root) =>
        Directory.GetFiles(root, "*.yaml", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .OrderBy(name => name, StringComparer.Ordinal)
            .Select(name => (name, File.ReadAllText(Path.Combine(root, name))))
            .ToList();

    /// <summary>Reads the entry by its kind and adds it to the rules. False for a template, which adds nothing.</summary>
    private static bool Add(RulesBuilder builder, RawDefinition entry)
    {
        var template = DefinitionReader.IsTemplate(entry);
        switch (DefinitionReader.KindOf(entry))
        {
            case "unit":
                AddUnlessTemplate(template, DefinitionReader.Unit(entry, !template), definition => builder.Add(definition));
                break;
            case "building":
                AddUnlessTemplate(template, DefinitionReader.Building(entry, !template), definition => builder.Add(definition));
                break;
            case "weapon":
                AddUnlessTemplate(template, DefinitionReader.Weapon(entry, !template), definition => builder.Add(definition));
                break;
            case "locomotor":
                AddUnlessTemplate(template, DefinitionReader.Locomotor(entry, !template), definition => builder.Add(definition));
                break;
            case "terrain":
                AddUnlessTemplate(template, DefinitionReader.Terrain(entry, !template), definition => builder.Add(definition));
                break;
            case "faction":
                AddUnlessTemplate(template, DefinitionReader.Faction(entry, !template), definition => builder.Add(definition));
                break;
            case "economy":
                AddUnlessTemplate(template, DefinitionReader.Economy(entry, !template), definition => builder.Add(definition));
                break;
        }

        return !template;
    }

    /// <summary>
    /// The errors in the order of the files as they were given, then by line and column, one per place. A field of a template is
    /// inherited by several definitions, and a wrong value in it would be reported for each of them; the first report is kept.
    /// </summary>
    private static List<RulesError> InFileOrder(List<RulesError> errors, List<string> fileNames)
    {
        var unique = new List<RulesError>(errors.Count);
        foreach (var error in errors)
        {
            if (!unique.Exists(other => (other.FileName, other.Line, other.Column) == (error.FileName, error.Line, error.Column)))
            {
                unique.Add(error);
            }
        }

        return unique
            .OrderBy(error => fileNames.IndexOf(error.FileName))
            .ThenBy(error => error.Line)
            .ThenBy(error => error.Column)
            .ToList();
    }

    /// <summary>A template is read for its errors and its fields, but it creates no definition.</summary>
    private static void AddUnlessTemplate<T>(bool template, T definition, Action<T> add)
    {
        if (!template)
        {
            add(definition);
        }
    }
}
