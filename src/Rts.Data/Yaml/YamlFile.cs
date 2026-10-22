using System;
using System.Collections.Generic;
using System.IO;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.RepresentationModel;

namespace Rts.Data.Yaml;

/// <summary>
/// Reads one data file into its definitions and keeps every position (docs/implementation/M3_base_economy.md, 4.1). The approach is the
/// one of the map reader (<c>RtsMapReader</c>, M7): YAML through the representation model, keys checked by hand, every error with its
/// file, line and column. The two copies are recorded in the M3 journal; they are merged after M7 closes.
/// </summary>
public static class YamlFile
{
    /// <summary>
    /// The definitions of a file in the order they are written; an empty file has none. Mistakes go to <paramref name="errors"/>: a file
    /// that is not valid YAML gives one error and no definitions, an entry that cannot be read gives one error and is left out.
    /// </summary>
    public static List<RawDefinition> Read(string fileName, string text, List<RulesError> errors)
    {
        var definitions = new List<RawDefinition>();
        try
        {
            ReadInto(definitions, fileName, text, errors);
        }
        catch (RulesFormatException e)
        {
            errors.Add(e.Error);
            definitions.Clear();
        }

        return definitions;
    }

    private static void ReadInto(List<RawDefinition> definitions, string fileName, string text, List<RulesError> errors)
    {
        text = text.Replace("\r\n", "\n");
        var stream = new YamlStream();
        try
        {
            stream.Load(new StringReader(text));
        }
        catch (YamlException e)
        {
            throw new RulesFormatException(fileName, (int)e.Start.Line, (int)e.Start.Column, $"not valid YAML: {e.Message}");
        }
        catch (InvalidOperationException e)
        {
            // YamlDotNet's scanner gives up this way on some broken input (an unclosed bracket) without a position: report
            // where the last token it could read ended, as RtsMapReader does.
            var (line, column) = LastReadablePosition(text);
            throw new RulesFormatException(fileName, line, column, $"not valid YAML: {e.Message}");
        }

        RejectAnchors(fileName, text);
        if (stream.Documents.Count > 1)
        {
            throw RulesFormatException.At(fileName, stream.Documents[1].RootNode, "a data file holds one YAML document");
        }

        if (stream.Documents.Count == 0 || stream.Documents[0].RootNode is YamlScalarNode { Value: "" })
        {
            return;
        }

        if (stream.Documents[0].RootNode is not YamlMappingNode root)
        {
            throw RulesFormatException.At(fileName, stream.Documents[0].RootNode, "a data file is a mapping from ids to definitions");
        }

        foreach (var entry in root)
        {
            try
            {
                definitions.Add(Definition(fileName, entry));
            }
            catch (RulesFormatException e)
            {
                errors.Add(e.Error);
            }
        }
    }

    /// <summary>
    /// Anchors are errors: a definition inherits through <c>inherits</c>, never through YAML references (4.1). An alias needs an anchor
    /// before it, so the anchor is the error that is reported.
    /// </summary>
    private static void RejectAnchors(string fileName, string text)
    {
        var parser = new Parser(new StringReader(text));
        while (parser.MoveNext())
        {
            if (parser.Current is NodeEvent { Anchor.IsEmpty: false } anchored)
            {
                throw new RulesFormatException(
                    fileName, (int)anchored.Start.Line, (int)anchored.Start.Column, $"anchor '&{anchored.Anchor}' is not used: inherit with 'inherits'");
            }
        }
    }

    private static RawDefinition Definition(string fileName, KeyValuePair<YamlNode, YamlNode> entry)
    {
        if (entry.Key is not YamlScalarNode idNode)
        {
            throw RulesFormatException.At(fileName, entry.Key, "an entry starts with its id");
        }

        var id = Scalars.Id(fileName, idNode);
        if (entry.Value is not YamlMappingNode body)
        {
            throw RulesFormatException.At(fileName, entry.Value, $"'{id}' must be a mapping of fields");
        }

        var fields = new SortedDictionary<string, Field>(StringComparer.Ordinal);
        foreach (var item in body)
        {
            if (item.Key is not YamlScalarNode { Value: { } key } keyNode)
            {
                throw RulesFormatException.At(fileName, item.Key, $"a key of '{id}' must be a single word");
            }

            if (!fields.TryAdd(key, new Field(fileName, keyNode, item.Value)))
            {
                throw RulesFormatException.At(fileName, keyNode, $"key '{key}' repeats in '{id}'");
            }
        }

        if (!fields.ContainsKey("kind"))
        {
            throw RulesFormatException.At(fileName, idNode, $"'{id}' has no 'kind'");
        }

        return new RawDefinition(id, fileName, idNode, fields);
    }

    /// <summary>The position where the scanner's last readable token ends; the start if it reads nothing.</summary>
    private static (int Line, int Column) LastReadablePosition(string text)
    {
        var scanner = new Scanner(new StringReader(text));
        var line = 1;
        var column = 1;
        try
        {
            while (scanner.MoveNext())
            {
                line = (int)scanner.Current!.End.Line;
                column = (int)scanner.Current.End.Column;
            }
        }
        catch (Exception e) when (e is YamlException or InvalidOperationException)
        {
            // The position of the last token read stays.
        }

        return (line, column);
    }
}
