using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rts.Data.Yaml;
using Rts.Simulation.Rules;

namespace Rts.Data;

/// <summary>
/// Loads the rules of a match from the data files (docs/implementation/M3_base_economy.md, 4.1): reads every file, checks that the ids are
/// unique, resolves <c>inherits</c>, reads each definition by its kind and builds the <see cref="RulesDatabase"/>. The first error stops the
/// load and names its file, line and column; reporting every error at once is the validator of M3 step 3.
/// </summary>
public static class RulesLoader
{
    /// <summary>Loads files given as (name, text) pairs. The name is what the errors show.</summary>
    public static RulesDatabase Load(IEnumerable<(string FileName, string Text)> files)
    {
        var definitions = new List<RawDefinition>();
        var ids = new SortedDictionary<string, RawDefinition>(StringComparer.Ordinal);
        foreach (var (fileName, text) in files)
        {
            foreach (var definition in YamlFile.Read(fileName, text))
            {
                if (ids.TryGetValue(definition.Id, out var first))
                {
                    throw RulesFormatException.At(
                        definition.FileName, definition.IdNode, $"'{definition.Id}' is already defined in {first.FileName}");
                }

                ids.Add(definition.Id, definition);
                definitions.Add(definition);
            }
        }

        var builder = new RulesBuilder();
        foreach (var definition in Inheritance.Resolve(definitions))
        {
            Add(builder, definition);
        }

        return builder.Build();
    }

    /// <summary>
    /// Loads every <c>.yaml</c> file below <paramref name="root"/>. The files are read in the order of their relative paths, so the first
    /// error is the same on every system.
    /// </summary>
    public static RulesDatabase LoadDirectory(string root)
    {
        var names = Directory.GetFiles(root, "*.yaml", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
        return Load(names.Select(name => (name, File.ReadAllText(Path.Combine(root, name)))));
    }

    private static void Add(RulesBuilder builder, RawDefinition entry)
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
        }
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
