using System;
using YamlDotNet.RepresentationModel;

namespace Rts.Data.Yaml;

/// <summary>
/// The fields of one definition, read against the schema of its kind (docs/implementation/M3_base_economy.md, 4.1). A key the kind does not
/// know is an error at that key, so a typo never disappears quietly. A missing required field is an error at the id, unless the definition
/// is a template (<c>abstract: true</c>): a template may leave fields out, because it only gives them to the definitions that inherit it.
/// </summary>
internal sealed class FieldReader
{
    /// <summary>The keys that every kind accepts; the loader reads them, not the kind.</summary>
    private static readonly string[] Common = ["kind", "inherits", "abstract"];

    private readonly RawDefinition _definition;
    private readonly bool _complete;

    public FieldReader(RawDefinition definition, bool complete, string[] allowed)
    {
        _definition = definition;
        _complete = complete;
        foreach (var (key, field) in definition.Fields)
        {
            if (Array.IndexOf(allowed, key) < 0 && Array.IndexOf(Common, key) < 0)
            {
                throw RulesFormatException.At(field.FileName, field.Key, $"unknown key '{key}' in '{definition.Id}'");
            }
        }
    }

    /// <summary>A required field. Missing in a complete definition is an error; in a template it gives the placeholder.</summary>
    public T Required<T>(string key, Func<string, YamlNode, T> convert, T placeholder)
    {
        if (_definition.Fields.TryGetValue(key, out var field))
        {
            return convert(field.FileName, field.Value);
        }

        if (!_complete)
        {
            return placeholder;
        }

        throw RulesFormatException.At(_definition.FileName, _definition.IdNode, $"'{_definition.Id}' has no '{key}'");
    }

    /// <summary>An optional field: its value, or the fallback when it is not written.</summary>
    public T Optional<T>(string key, Func<string, YamlNode, T> convert, T fallback) =>
        _definition.Fields.TryGetValue(key, out var field) ? convert(field.FileName, field.Value) : fallback;

    /// <summary>An optional field without a default: null when it is not written.</summary>
    public T? Maybe<T>(string key, Func<string, YamlNode, T> convert)
        where T : struct =>
        _definition.Fields.TryGetValue(key, out var field) ? convert(field.FileName, field.Value) : null;
}
