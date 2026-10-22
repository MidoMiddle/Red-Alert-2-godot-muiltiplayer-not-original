using System;
using YamlDotNet.RepresentationModel;

namespace Rts.Data.Yaml;

/// <summary>
/// A data file, or a definition in it, that cannot be read: the first mistake found in it. <see cref="FileName"/> names the file,
/// <see cref="Line"/> and <see cref="Column"/> are 1-based, and the message reads <c>file:line:column: reason</c>. The loader collects these
/// as <see cref="RulesError"/> and reports them together in a <see cref="RulesException"/>.
/// </summary>
public sealed class RulesFormatException(string fileName, int line, int column, string reason) : Exception($"{fileName}:{line}:{column}: {reason}")
{
    public string FileName { get; } = fileName;

    public int Line { get; } = line;

    public int Column { get; } = column;

    public string Reason { get; } = reason;

    /// <summary>The same mistake as an entry of the list that the loader collects.</summary>
    public RulesError Error => new(FileName, Line, Column, Reason);

    /// <summary>The error at the start of a YAML node.</summary>
    public static RulesFormatException At(string fileName, YamlNode node, string reason) =>
        new(fileName, (int)node.Start.Line, (int)node.Start.Column, reason);
}
