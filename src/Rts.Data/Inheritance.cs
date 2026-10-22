using System;
using System.Collections.Generic;
using System.Linq;
using Rts.Data.Yaml;

namespace Rts.Data;

/// <summary>
/// Resolves <c>inherits</c> (docs/implementation/M3_base_economy.md, 4.1). A definition takes every field of its parent, and its own fields
/// replace them: a list is replaced whole, and a field cannot be removed. <c>kind</c>, <c>inherits</c> and <c>abstract</c> are never inherited,
/// and the parent must have the same kind. A chain may be as long as the data needs; a cycle, or a parent that is not defined, is an error at
/// the <c>inherits</c> that causes it.
/// </summary>
public static class Inheritance
{
    /// <summary>The keys a definition does not pass on to the definitions that inherit it.</summary>
    private static readonly string[] Own = ["kind", "inherits", "abstract"];

    /// <summary>The definitions with their fields merged, in the order they were given. The ids must be unique.</summary>
    public static List<RawDefinition> Resolve(IReadOnlyList<RawDefinition> definitions)
    {
        var byId = new SortedDictionary<string, RawDefinition>(StringComparer.Ordinal);
        foreach (var definition in definitions)
        {
            byId.Add(definition.Id, definition);
        }

        var resolved = new SortedDictionary<string, RawDefinition>(StringComparer.Ordinal);
        var path = new List<string>();
        var merged = new List<RawDefinition>(definitions.Count);
        foreach (var definition in definitions)
        {
            merged.Add(Resolve(definition.Id, byId, resolved, path));
        }

        return merged;
    }

    /// <param name="path">The ids whose resolution is under way, from the outermost; a parent found in it closes a cycle.</param>
    private static RawDefinition Resolve(
        string id,
        SortedDictionary<string, RawDefinition> byId,
        SortedDictionary<string, RawDefinition> resolved,
        List<string> path)
    {
        if (resolved.TryGetValue(id, out var done))
        {
            return done;
        }

        var own = byId[id];
        if (!own.Fields.TryGetValue("inherits", out var inherits))
        {
            resolved.Add(id, own);
            return own;
        }

        var parentId = Scalars.Id(inherits.FileName, inherits.Value);
        if (!byId.ContainsKey(parentId))
        {
            throw RulesFormatException.At(inherits.FileName, inherits.Value, $"'{id}' inherits '{parentId}', which is not defined");
        }

        path.Add(id);
        if (path.Contains(parentId))
        {
            var cycle = path.Skip(path.IndexOf(parentId)).Append(parentId);
            throw RulesFormatException.At(inherits.FileName, inherits.Value, $"inheritance cycle: {string.Join(" -> ", cycle)}");
        }

        var parent = Resolve(parentId, byId, resolved, path);
        path.RemoveAt(path.Count - 1);

        var kind = DefinitionReader.KindOf(own);
        var parentKind = DefinitionReader.KindOf(parent);
        if (kind != parentKind)
        {
            var field = own.Fields["kind"];
            throw RulesFormatException.At(field.FileName, field.Value, $"'{id}' is {kind}, but it inherits '{parentId}', which is {parentKind}");
        }

        var fields = new SortedDictionary<string, Field>(StringComparer.Ordinal);
        foreach (var (key, field) in parent.Fields)
        {
            if (Array.IndexOf(Own, key) < 0)
            {
                fields.Add(key, field);
            }
        }

        foreach (var (key, field) in own.Fields)
        {
            fields[key] = field;
        }

        var result = new RawDefinition(id, own.FileName, own.IdNode, fields);
        resolved.Add(id, result);
        return result;
    }
}
