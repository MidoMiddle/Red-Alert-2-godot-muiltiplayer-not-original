using System;
using System.Collections.Generic;
using System.Linq;
using Rts.Data.Yaml;

namespace Rts.Data;

/// <summary>
/// Resolves <c>inherits</c> (docs/implementation/M3_base_economy.md, 4.1). A definition takes every field of its parent, and its own fields
/// replace them: a list is replaced whole, and a field cannot be removed. <c>kind</c>, <c>inherits</c> and <c>abstract</c> are never inherited,
/// and the parent must have the same kind. A chain may be as long as the data needs; a cycle, or a parent that is not defined, is an error at
/// the <c>inherits</c> that causes it. A definition whose chain is broken is left out, and so is every definition that inherits it: the
/// error is reported once, where it is written.
/// </summary>
public static class Inheritance
{
    /// <summary>The keys a definition does not pass on to the definitions that inherit it.</summary>
    private static readonly string[] Own = ["kind", "inherits", "abstract"];

    /// <summary>
    /// The definitions with their fields merged, in the order they were given, without those whose chain is broken. The ids must be unique.
    /// Mistakes go to <paramref name="errors"/>.
    /// </summary>
    public static List<RawDefinition> Resolve(IReadOnlyList<RawDefinition> definitions, List<RulesError> errors)
    {
        var byId = new SortedDictionary<string, RawDefinition>(StringComparer.Ordinal);
        foreach (var definition in definitions)
        {
            byId.Add(definition.Id, definition);
        }

        // A null value marks a definition that cannot be resolved.
        var resolved = new SortedDictionary<string, RawDefinition?>(StringComparer.Ordinal);
        var path = new List<string>();
        var merged = new List<RawDefinition>(definitions.Count);
        foreach (var definition in definitions)
        {
            if (Resolve(definition.Id, byId, resolved, path, errors) is { } result)
            {
                merged.Add(result);
            }
        }

        return merged;
    }

    /// <param name="path">The ids whose resolution is under way, from the outermost; a parent found in it closes a cycle.</param>
    private static RawDefinition? Resolve(
        string id,
        SortedDictionary<string, RawDefinition> byId,
        SortedDictionary<string, RawDefinition?> resolved,
        List<string> path,
        List<RulesError> errors)
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

        try
        {
            var parentId = Scalars.Id(inherits.FileName, inherits.Value);
            if (!byId.ContainsKey(parentId))
            {
                throw RulesFormatException.At(inherits.FileName, inherits.Value, $"'{id}' inherits '{parentId}', which is not defined");
            }

            // The parent is under way, or is the definition itself: the chain closes here.
            var cycleStart = parentId == id ? path.Count : path.IndexOf(parentId);
            if (cycleStart >= 0)
            {
                var cycle = path.Skip(cycleStart).Append(id).Append(parentId);
                throw RulesFormatException.At(inherits.FileName, inherits.Value, $"inheritance cycle: {string.Join(" -> ", cycle)}");
            }

            path.Add(id);
            var parent = Resolve(parentId, byId, resolved, path, errors);
            path.RemoveAt(path.Count - 1);
            if (parent is null)
            {
                // The parent's own error is already reported.
                resolved.Add(id, null);
                return null;
            }

            var kind = DefinitionReader.KindOf(own);
            var parentKind = DefinitionReader.KindOf(parent);
            if (kind != parentKind)
            {
                var field = own.Fields["kind"];
                throw RulesFormatException.At(
                    field.FileName, field.Value, $"'{id}' is {kind}, but it inherits '{parentId}', which is {parentKind}");
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
        catch (RulesFormatException e)
        {
            errors.Add(e.Error);
            resolved.Add(id, null);
            return null;
        }
    }
}
