using System;
using YamlDotNet.RepresentationModel;

namespace Rts.Data.Yaml;

/// <summary>
/// A data file, or a definition in it, that cannot be read. <see cref="FileName"/> names the file, <see cref="Line"/> and
/// <see cref="Column"/> are 1-based, and the message reads <c>file:line:column: reason</c>.
/// </summary>
public sealed class RulesFormatException(string fileName, int line, int column, string reason) : Exception($"{fileName}:{line}:{column}: {reason}")
{
    public string FileName { get; } = fileName;

    public int Line { get; } = line;

    public int Column { get; } = column;

    public string Reason { get; } = reason;

    /// <summary>The error at the start of a YAML node.</summary>
    public static RulesFormatException At(string fileName, YamlNode node, string reason) =>
        new(fileName, (int)node.Start.Line, (int)node.Start.Column, reason);
}
