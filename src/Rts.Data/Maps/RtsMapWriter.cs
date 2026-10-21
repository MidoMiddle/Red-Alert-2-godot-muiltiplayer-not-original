using System.Globalization;
using System.Text;
using Rts.Simulation.Geometry;

namespace Rts.Data.Maps;

/// <summary>
/// Writes a map in the canonical <c>.rtsmap</c> form (docs/implementation/M7_maps.md, 4.10): fixed key order, sorted lists,
/// no comments, LF line ends, every layer written out. Written by hand, not by a YAML serializer, so the exact text never
/// depends on a library version; the reader reads it back to an equal map.
/// </summary>
public static class RtsMapWriter
{
    public static string Write(MapDefinition map)
    {
        var text = new StringBuilder();
        Line(text, $"format: {MapDefinition.FormatVersion}");
        Line(text, $"id: {map.Id}");
        Line(text, $"name: {Quote(map.Name)}");
        Line(text, $"author: {Quote(map.Author)}");
        Line(text, $"description: {Quote(map.Description)}");
        Line(text, $"size: [{map.Width}, {map.Height}]");
        Line(text, $"climate: {map.Climate}");
        Line(text, $"players: {map.Players}");

        List(text, "starts", map.Starts, s => $"{{ slot: {s.Slot}, cell: {Cell(s.Cell)} }}");

        Line(text, "legend:");
        Line(text, "  texture:");
        foreach (var entry in map.TextureLegend)
        {
            Line(text, $"    {entry.Code}: {entry.Material}");
        }

        Line(text, "layers:");
        Layer(text, "height", map.Width + 1, map.Height + 1, i => MapNames.HeightCodes[map.Heights[i]]);
        Layer(text, "land", map.Width, map.Height, i => MapNames.LandCode(map.Land[i]));
        Layer(text, "texture", map.Width, map.Height, i => map.Texture[i]);
        Layer(text, "ore", map.Width, map.Height, i => MapNames.OreCodes[map.Ore[i]]);

        List(text, "objects", map.Objects, o => $"{{ type: {o.Type}, cell: {Cell(o.Cell)}, variant: {Number(o.Variant)} }}");
        List(text, "bridges", map.Bridges, b => $"{{ type: {b.Type}, from: {Cell(b.From)}, to: {Cell(b.To)} }}");
        List(text, "structures", map.Structures,
            s => $"{{ type: {s.Type}, cell: {Cell(s.Cell)}, owner: {(s.Owner == 0 ? "neutral" : Number(s.Owner))}, facing: {Number(s.Facing)} }}");
        return text.ToString();
    }

    private static void List<T>(StringBuilder text, string key, System.Collections.Immutable.ImmutableArray<T> items, System.Func<T, string> item)
    {
        if (items.IsEmpty)
        {
            Line(text, $"{key}: []");
            return;
        }

        Line(text, $"{key}:");
        foreach (var entry in items)
        {
            Line(text, $"  - {item(entry)}");
        }
    }

    private static void Layer(StringBuilder text, string key, int width, int height, System.Func<int, char> code)
    {
        Line(text, $"  {key}: |");
        for (var y = 0; y < height; y++)
        {
            text.Append("    ");
            for (var x = 0; x < width; x++)
            {
                text.Append(code(y * width + x));
            }

            text.Append('\n');
        }
    }

    private static string Cell(CellPos cell) => $"[{Number(cell.X)}, {Number(cell.Y)}]";

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>A double-quoted YAML string; only the backslash and the quote need escaping in a single line without controls.</summary>
    private static string Quote(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    private static void Line(StringBuilder text, string line) => text.Append(line).Append('\n');
}
