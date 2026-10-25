using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.World;

namespace Rts.Net.Desync;

/// <summary>
/// The whole state of a simulation as text, for finding a desync (docs/implementation/M9_network.md, 4.10): one line a field,
/// <c>path = value</c>, in an order that is the same on every computer — the world's own fields, the players one by one, the entities by
/// id, then the layers of the map row by row. Two dumps of the same tick from two computers differ exactly where their states do;
/// <c>tools/Rts.DesyncDiff</c> finds the first such line.
/// <para>
/// Read by reflection — the public and internal properties of the simulation's objects, sorted by name — so the simulation keeps no
/// code for it and nothing of its state is left out by a forgotten line. The rules (archetypes, definitions) are named, not written:
/// they come from the data, which the handshake has checked already. Another entity is written as its id.
/// </para>
/// </summary>
public static class StateDump
{
    private const int MaxDepth = 6;

    // Not state: the rules, the index of places (derived from the positions), the world again from inside it.
    private static readonly HashSet<string> Skipped = ["Rules", "Spatial", "World"];

    // The rules an object is made of: named by their id.
    private static readonly HashSet<string> Named = ["Archetype", "Def", "Type"];

    public static string Write(GameWorld world)
    {
        var text = new StringBuilder();
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        Line(text, "tick", world.CurrentTick);
        WriteObject(text, "world", world, visited, 0, skip: ["Map", "Players", "Entities"]);

        var players = world.Players.ToList();
        for (var i = 0; i < players.Count; i++)
        {
            WriteValue(text, $"player[{i + 1}]", players[i], visited, 0);
        }

        foreach (var entity in world.Entities.OrderBy(entity => entity.Id.Value))
        {
            Line(text, $"entity[{entity.Id.Value}].kind", entity.GetType().Name);
            WriteObject(text, $"entity[{entity.Id.Value}]", entity, visited, 0, skip: []);
        }

        WriteMap(text, world.Map, visited);
        return text.ToString();
    }

    private static void WriteMap(StringBuilder text, MapGrid map, HashSet<object> visited)
    {
        WriteObject(text, "map", map, visited, 0, skip: ["Heights"]);
        for (var y = 0; y < map.Height; y++)
        {
            var land = new StringBuilder(map.Width);
            var ore = new StringBuilder(map.Width * 2);
            for (var x = 0; x < map.Width; x++)
            {
                var cell = new CellPos(x, y);
                land.Append((char)('0' + (int)map.LandAt(cell)));
                ore.Append(map.OreAt(cell).Packed.ToString("x2", CultureInfo.InvariantCulture));
            }

            Line(text, $"map.land[{y}]", land.ToString());
            Line(text, $"map.ore[{y}]", ore.ToString());
        }

        for (var y = 0; y <= map.Height; y++)
        {
            var row = new StringBuilder();
            for (var x = 0; x <= map.Width; x++)
            {
                row.Append(map.Heights.CornerLevel(x, y).ToString(CultureInfo.InvariantCulture)).Append(' ');
            }

            Line(text, $"map.heights[{y}]", row.ToString().TrimEnd());
        }
    }

    private static void WriteObject(StringBuilder text, string path, object value, HashSet<object> visited, int depth, string[] skip)
    {
        if (!visited.Add(value))
        {
            Line(text, path, "(seen)");
            return;
        }

        foreach (var property in Properties(value.GetType()))
        {
            if (Skipped.Contains(property.Name) || skip.Contains(property.Name))
            {
                continue;
            }

            if (Named.Contains(property.Name))
            {
                Line(text, $"{path}.{property.Name}", Identity(Get(property, value)));
                continue;
            }

            WriteValue(text, $"{path}.{property.Name}", Get(property, value), visited, depth + 1);
        }
    }

    private static void WriteValue(StringBuilder text, string path, object? value, HashSet<object> visited, int depth)
    {
        switch (value)
        {
            case null:
                Line(text, path, "null");
                return;
            case string or bool or char or Enum or decimal:
            case IFormattable when value.GetType().IsPrimitive:
                Line(text, path, Scalar(value));
                return;
            case Entity entity:
                Line(text, path, "#" + entity.Id.Value.ToString(CultureInfo.InvariantCulture));
                return;
        }

        var type = value.GetType();
        if (depth > MaxDepth || !(type.Namespace?.StartsWith("Rts.Simulation", StringComparison.Ordinal) ?? false) && value is not IEnumerable)
        {
            Line(text, path, Scalar(value));
            return;
        }

        if (value is IEnumerable items)
        {
            var i = 0;
            foreach (var item in items)
            {
                WriteValue(text, $"{path}[{i}]", item, visited, depth + 1);
                i++;
            }

            Line(text, $"{path}.count", i);
            return;
        }

        // A small value of the simulation — a position, an id, a fixed number — reads best as itself.
        if (type.IsValueType && Properties(type).All(property => property.PropertyType.IsPrimitive || property.PropertyType.IsEnum))
        {
            Line(text, path, Scalar(value));
            return;
        }

        WriteObject(text, path, value, visited, depth, skip: []);
    }

    private static IEnumerable<PropertyInfo> Properties(Type type) =>
        type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(property => property.GetIndexParameters().Length == 0 && !property.PropertyType.IsByRefLike && !property.PropertyType.IsPointer
                               && property.GetMethod is { } getter && (getter.IsPublic || getter.IsAssembly))
            .OrderBy(property => property.Name, StringComparer.Ordinal);

    private static object? Get(PropertyInfo property, object owner)
    {
        try
        {
            return property.GetValue(owner);
        }
        catch (TargetInvocationException error)
        {
            return $"(throws {error.InnerException?.GetType().Name})";
        }
    }

    // What a piece of the rules is: its id when it has one.
    private static string Identity(object? rules) =>
        rules is null ? "null"
        : rules.GetType().GetProperty("Id") is { } id ? Scalar(id.GetValue(rules) ?? "null")
        : Scalar(rules);

    private static string Scalar(object value) =>
        value is IFormattable formattable ? formattable.ToString(null, CultureInfo.InvariantCulture) : value.ToString() ?? "";

    private static void Line(StringBuilder text, string path, object value) =>
        text.Append(path).Append(" = ").Append(value is string s ? s : Scalar(value)).Append('\n');
}
