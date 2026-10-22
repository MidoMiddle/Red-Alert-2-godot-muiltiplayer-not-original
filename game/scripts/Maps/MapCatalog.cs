using System;
using System.Collections.Generic;
using Rts.Data.Maps;

namespace Rts.Client.Maps;

/// <summary>A playable map of the catalog: the file it came from, the map, and its hash (the cache key of its preview).</summary>
public sealed record MapEntry(string File, MapDefinition Map, string Hash);

/// <summary>
/// The maps the skirmish screen and the editor offer (docs/implementation/M7_maps.md, steps 10–11): every <c>.rtsmap</c> of
/// <see cref="Directory"/> and of the player's <see cref="UserDirectory"/> that reads and plays (<see cref="MapDefinition.ToMapGrid"/>), sorted by name. A broken file is left
/// out with an error naming it and, for a format error, the line and column. Pure: the screen reads the files. Tested in
/// Rts.Data.Tests.
/// </summary>
public static class MapCatalog
{
    public const string Directory = "res://data/maps";

    /// <summary>The player's own maps, where the map editor saves (<c>UserMapPaths.UserDirectory</c>).</summary>
    public const string UserDirectory = "user://maps";

    public static (List<MapEntry> Maps, List<string> Errors) Load(IEnumerable<(string File, string Text)> files)
    {
        var maps = new List<MapEntry>();
        var errors = new List<string>();
        foreach (var (file, text) in files)
        {
            try
            {
                var map = RtsMapReader.Read(text);
                map.ToMapGrid();
                maps.Add(new MapEntry(file, map, MapHasher.Hash(map)));
            }
            catch (RtsMapFormatException error)
            {
                errors.Add($"{file}:{error.Line}:{error.Column}: {error.Reason}");
            }
            catch (ArgumentException error)
            {
                errors.Add($"{file}: {error.Message}");
            }
        }

        maps.Sort((a, b) => string.Compare(a.Map.Name, b.Map.Name, StringComparison.Ordinal) is var byName and not 0
            ? byName
            : string.Compare(a.File, b.File, StringComparison.Ordinal));
        return (maps, errors);
    }
}
