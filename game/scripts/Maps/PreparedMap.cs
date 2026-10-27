using System;
using Rts.Data.Maps;
using Rts.Simulation.Map;
using Rts.Simulation.Rules;

namespace Rts.Client.Maps;

/// <summary>
/// The map of the match that is about to start, read and parsed by the loading screen on a background thread
/// (docs/implementation/M6_menus_skirmish.md, 4.14). <c>SimulationHost</c> takes it instead of parsing the file again.
/// </summary>
public static class PreparedMap
{
    private static (string Path, TestMap Map, MapDefinition? Definition)? _prepared;

    /// <summary>
    /// Parses the text of the map at <paramref name="path"/>: an <c>.rtsmap</c> with its definition (its starts make a match),
    /// or an ASCII test map. Pure, so it can run on any thread.
    /// </summary>
    public static (TestMap Map, MapDefinition? Definition) Parse(string path, string text, GameRules rules)
    {
        if (!path.EndsWith(".rtsmap", StringComparison.Ordinal))
        {
            return (TestMapParser.Parse(text, rules), null);
        }

        // A map of the M7 format: with two starts or more it is played as a match; the debug squads at the starts are left for
        // a map with fewer.
        var definition = RtsMapReader.Read(text);
        var grid = definition.ToMapGrid(rules.MapTypes);
        return (new TestMap(grid, StartSquads.Place(definition, grid, rules)), definition);
    }

    /// <summary>Keeps the parsed map of <paramref name="path"/> for the match that starts next.</summary>
    public static void Store(string path, TestMap map, MapDefinition? definition) => _prepared = (path, map, definition);

    /// <summary>The parsed map of <paramref name="path"/> if it was stored; it is handed out once.</summary>
    public static bool TryTake(string path, out (TestMap Map, MapDefinition? Definition) map)
    {
        if (_prepared is { } prepared && prepared.Path == path)
        {
            _prepared = null;
            map = (prepared.Map, prepared.Definition);
            return true;
        }

        map = default;
        return false;
    }
}
