using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Rts.Simulation.Geometry;
using Rts.Simulation.World;

namespace Rts.Simulation.Map;

/// <summary>A unit placed by a test map.</summary>
public sealed record TestMapUnit(UnitType Type, CellPos Cell, PlayerId Owner);

/// <summary>A parsed test map: the grid and the units on it.</summary>
public sealed record TestMap(MapGrid Grid, IReadOnlyList<TestMapUnit> Units);

/// <summary>
/// Temporary ASCII map format for tests and the M2 sandbox, until the real map format of M7
/// (docs/implementation/M2_unit_control.md, 4.3). One row of cells per line, codes separated by spaces:
/// <code>
/// // comment
/// . , = b ~ W # i T
/// unit heavy_tank 3 5 player=1
/// </code>
/// Codes are the land types of docs/implementation/M7_maps.md, 4.3, plus <c>T</c> for a tree (clear land with an obstacle).
/// A <c>unit</c> line takes a unit id of <see cref="UnitArchetypes"/>, the cell x and y, and the owner.
/// Every error names the line and the column.
/// </summary>
public static class TestMapParser
{
    public const char TreeCode = 'T';

    public static TestMap Parse(string text)
    {
        var rows = new List<(int Line, LandType[] Land, bool[] Trees)>();
        var unitLines = new List<(int Line, string Text)>();

        var lines = text.Replace("\r\n", "\n").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var lineNumber = i + 1;
            var line = lines[i].Trim();
            if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            if (line.StartsWith("unit ", StringComparison.Ordinal))
            {
                unitLines.Add((lineNumber, lines[i]));
                continue;
            }

            rows.Add(ParseRow(lineNumber, lines[i]));
        }

        if (rows.Count == 0)
        {
            throw Error(0, 0, "the map has no rows of cells");
        }

        var width = rows[0].Land.Length;
        var grid = new MapGrid(width, rows.Count);
        for (var y = 0; y < rows.Count; y++)
        {
            var (line, land, trees) = rows[y];
            if (land.Length != width)
            {
                throw Error(line, 1, $"row has {land.Length} cells, the first row has {width}");
            }

            for (var x = 0; x < width; x++)
            {
                var cell = new CellPos(x, y);
                grid.SetLand(cell, land[x]);
                grid.SetBlocked(cell, trees[x]);
            }
        }

        var units = new List<TestMapUnit>();
        foreach (var (line, unitText) in unitLines)
        {
            units.Add(ParseUnit(line, unitText, grid));
        }

        return new TestMap(grid, units);
    }

    /// <summary>The map code of a land type, the inverse of parsing.</summary>
    public static char CodeOf(LandType land) => land switch
    {
        LandType.Clear => '.',
        LandType.Rough => ',',
        LandType.Road => '=',
        LandType.Beach => 'b',
        LandType.Shallow => '~',
        LandType.Deep => 'W',
        LandType.Rock => '#',
        LandType.Ice => 'i',
        _ => throw new ArgumentOutOfRangeException(nameof(land), land, "Unknown land type."),
    };

    private static (int, LandType[], bool[]) ParseRow(int lineNumber, string line)
    {
        var land = new List<LandType>();
        var trees = new List<bool>();
        for (var column = 0; column < line.Length; column++)
        {
            var code = line[column];
            if (code == ' ' || code == '\t')
            {
                continue;
            }

            if (column + 1 < line.Length && line[column + 1] != ' ' && line[column + 1] != '\t')
            {
                throw Error(lineNumber, column + 1, "cell codes must be single characters separated by spaces");
            }

            if (code == TreeCode)
            {
                land.Add(LandType.Clear);
                trees.Add(true);
                continue;
            }

            land.Add(code switch
            {
                '.' => LandType.Clear,
                ',' => LandType.Rough,
                '=' => LandType.Road,
                'b' => LandType.Beach,
                '~' => LandType.Shallow,
                'W' => LandType.Deep,
                '#' => LandType.Rock,
                'i' => LandType.Ice,
                _ => throw Error(lineNumber, column + 1, $"unknown cell code '{code}'"),
            });
            trees.Add(false);
        }

        return (lineNumber, land.ToArray(), trees.ToArray());
    }

    // unit <id> <x> <y> player=<n>
    private static TestMapUnit ParseUnit(int lineNumber, string line, MapGrid grid)
    {
        var parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 5 || !parts[4].StartsWith("player=", StringComparison.Ordinal))
        {
            throw Error(lineNumber, 1, "expected 'unit <id> <x> <y> player=<n>'");
        }

        if (!UnitArchetypes.TryGetById(parts[1], out var archetype))
        {
            throw Error(lineNumber, ColumnOf(line, parts[1]), $"unknown unit '{parts[1]}'");
        }

        var x = ParseNumber(lineNumber, line, parts[2]);
        var y = ParseNumber(lineNumber, line, parts[3]);
        var player = ParseNumber(lineNumber, line, parts[4]["player=".Length..]);
        if (player < 0 || player > PlayerId.MaxPlayers)
        {
            throw Error(lineNumber, ColumnOf(line, parts[4]), $"player must be 0…{PlayerId.MaxPlayers}, got {player}");
        }

        var cell = new CellPos(x, y);
        if (!grid.Contains(cell))
        {
            throw Error(lineNumber, ColumnOf(line, parts[2]), $"cell ({x}, {y}) is outside the {grid.Width}x{grid.Height} map");
        }

        if (!grid.CanEnter(cell, archetype.Locomotor))
        {
            throw Error(lineNumber, ColumnOf(line, parts[2]), $"{archetype.Id} ({archetype.Locomotor}) cannot stand on cell ({x}, {y})");
        }

        return new TestMapUnit(archetype.Type, cell, new PlayerId(player));
    }

    private static int ParseNumber(int lineNumber, string line, string text) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw Error(lineNumber, ColumnOf(line, text), $"'{text}' is not a number");

    private static int ColumnOf(string line, string part) => line.IndexOf(part, StringComparison.Ordinal) + 1;

    private static InvalidDataException Error(int line, int column, string message) =>
        new(line == 0 ? $"Test map: {message}." : $"Test map, line {line}, column {column}: {message}.");
}
