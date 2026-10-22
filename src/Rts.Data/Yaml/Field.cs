using YamlDotNet.RepresentationModel;

namespace Rts.Data.Yaml;

/// <summary>
/// A field of a definition as it is written: its key, its value and the file they come from. An inherited field keeps the file and
/// the place of its parent, so an error in it points at the line where the value is written.
/// </summary>
public sealed record Field(string FileName, YamlScalarNode Key, YamlNode Value);
