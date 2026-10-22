using System.Collections.Generic;
using YamlDotNet.RepresentationModel;

namespace Rts.Data.Yaml;

/// <summary>
/// One definition of a data file as it is written (docs/implementation/M3_base_economy.md, 4.1): its id, its file and its fields. Read
/// from the file, <see cref="Fields"/> are the fields the entry has itself; once inheritance is resolved they are the merged fields.
/// </summary>
public sealed class RawDefinition(string id, string fileName, YamlScalarNode idNode, SortedDictionary<string, Field> fields)
{
    public string Id { get; } = id;

    public string FileName { get; } = fileName;

    /// <summary>The key of the entry, where its id is written. Errors about the whole entry point here.</summary>
    public YamlScalarNode IdNode { get; } = idNode;

    /// <summary>The fields by key, in key order.</summary>
    public SortedDictionary<string, Field> Fields { get; } = fields;
}
