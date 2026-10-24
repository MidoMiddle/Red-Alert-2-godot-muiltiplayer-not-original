using System;
using System.Collections.Immutable;

namespace Rts.Data.Maps;

/// <summary>
/// The time of day of a map (docs/map_elements/18_lighting/README.md, 3–4): a preset of the climate's light, chosen in the
/// map header. Visual only: the simulation never reads it.
/// </summary>
public static class MapLighting
{
    public const string Default = "day";

    /// <summary>The presets in the order of a day, as the editor offers them.</summary>
    public static readonly ImmutableArray<string> Ids = ["dawn", "day", "dusk", "night"];

    public static bool IsKnown(string id) => Ids.IndexOf(id, 0, Ids.Length, StringComparer.Ordinal) >= 0;
}
