using System;
using System.Collections.Generic;
using System.Linq;
using Rts.Data.Maps;
using Rts.MapGen.Stages;
using Rts.Simulation.Geometry;
using Rts.Simulation.Math;

namespace Rts.MapGen;

/// <summary>A random map that did not pass <see cref="MapValidator"/> in <see cref="MapGenerator.Attempts"/> seeds in a row.</summary>
public sealed class MapGenException(string message, IReadOnlyList<MapProblem> problems) : Exception(message)
{
    public IReadOnlyList<MapProblem> Problems { get; } = problems;
}

/// <summary>
/// The random map generator (docs/implementation/M7_maps.md, 4.14). Integers and hashes only, so one seed and the same settings
/// give the same map on every system. Stages: heights (<see cref="Relief"/>), <see cref="Starts"/>, water
/// (<see cref="Water"/>), then smoothing — the whole map for hills, ramps, starts and river banks for mountains — lakes and
/// shores, <see cref="Bridges"/>, <see cref="Ore"/>, the drills of the fields (<see cref="Drills"/>, M8 step 10b), <see cref="Roads"/>, an oil derrick by the contested ore, rough patches,
/// the dressing of the climate (<see cref="Dressing"/>, with a harbour on the sea, <see cref="Harbour"/>), what stands in the sea
/// (<see cref="Offshore"/>), and the forest (<see cref="Features"/>). The map is symmetric by construction (<see cref="Symmetry"/>). A map with errors
/// of <see cref="MapValidator"/> is made again from seed + 1, at most <see cref="Attempts"/> times; the seed used is in the
/// map's id (<c>random-SEED</c>) and description.
/// </summary>
public static class MapGenerator
{
    public const int Attempts = 10;

    /// <summary>The map and the seed it was made from (the asked one, or a later one after a failed check).</summary>
    public static (MapDefinition Map, uint Seed) Generate(MapGenSettings settings) => Generate(settings, map => MapValidator.Check(map, settings.Types));

    /// <summary>As <see cref="Generate(MapGenSettings)"/> with another check, so tests can make the retries happen.</summary>
    internal static (MapDefinition Map, uint Seed) Generate(MapGenSettings settings, Func<MapDefinition, List<MapProblem>> check)
    {
        settings.Check();
        IReadOnlyList<MapProblem> problems = [];
        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            var seed = unchecked(settings.Seed + (uint)attempt);
            var map = Build(settings, seed);
            problems = check(map);
            if (!MapValidator.HasErrors(problems))
            {
                return (map, seed);
            }
        }

        throw new MapGenException($"No valid map in {Attempts} seeds from {settings.Seed}: {problems[0].Message}", problems);
    }

    /// <summary>One attempt from exactly this seed, checked or not.</summary>
    internal static MapDefinition Build(MapGenSettings settings, uint seed)
    {
        var map = new GenMap(settings, seed);
        Relief.Build(map);
        Starts.Place(map);

        if (settings.Water == WaterKind.Islands)
        {
            Water.Islands(map);
        }
        else
        {
            var soften = settings.Relief == ReliefKind.Mountainous ? Relief.RampMask(map) : null;
            var riverMask = new bool[map.Levels.Length];
            if (settings.Water == WaterKind.Rivers)
            {
                Water.Rivers(map, riverMask);
            }
            else if (settings.Water == WaterKind.Sea)
            {
                Water.Sea(map, riverMask);
            }

            if (soften is not null)
            {
                // Around every start, and along a corridor from it to the centre: the terraces there get ramps, so no base is walled in.
                foreach (var start in map.Starts)
                {
                    Relief.MarkDisc(map, soften, start.Cell, Starts.FlatRadius + 5);
                    // In doubled coordinates (cell centres 2x + 1, the centre of the map N), where rounding is the same for every image.
                    var (sx, sy) = (2 * start.Cell.X + 1, 2 * start.Cell.Y + 1);
                    for (var step = 0; step <= 8; step++)
                    {
                        Relief.MarkDisc2(map, soften, sx + (map.N - sx) * step / 8, sy + (map.N - sy) * step / 8, 3);
                    }
                }

                for (var i = 0; i < soften.Length; i++)
                {
                    soften[i] |= riverMask[i];
                }
            }

            if (settings.Relief != ReliefKind.Flat)
            {
                Relief.Soften(map, soften);
            }

            if (settings.Water == WaterKind.Lakes)
            {
                Water.Lakes(map);
            }
        }

        Water.Shores(map, beachOnCliffs: settings.Water != WaterKind.Islands);
        if (settings.Water == WaterKind.Rivers)
        {
            Bridges.Place(map);
        }

        Ore.Place(map, out var contested);
        PlaceDrills(map);
        Roads.Place(map);
        Features.Buildings(map, contested);
        Bridges.Huts(map);
        Features.Town(map);
        Dressing.Place(map);
        Offshore.Place(map);
        Features.Rough(map);
        Features.Forest(map);
        Features.Ground(map);

        var name = $"Random {settings.Size} ({seed})";
        var description = $"Generated from seed {seed}: {settings.Players} players, {settings.Symmetry}, {settings.Relief}, " +
                          $"water {settings.Water}{(settings.SeaPercent > 0 ? $" {settings.SeaPercent}%" : "")}, " +
                          $"forest {settings.Forest}, resources {settings.Resources}.";
        // Sand only where a vehicle comes down to the water; a cliff straight into it (the owner, 2026-10-30).
        return Shorelines.Tidy(map.ToDefinition(name, Drills.WithDrills(description, settings.Drills)), map.Types);
    }

    /// <summary>
    /// The harbour and what stands in the sea (<see cref="Harbour"/>, <see cref="Offshore"/>) added to a map the generator made before
    /// with these settings, the rest of it as it is (the owner, 2026-11-01: the ready maps get them too). A town's old harbour by the
    /// water gives way to the quay's. The ground of a new kind gets a letter of the legend.
    /// </summary>
    public static MapDefinition AddSea(MapDefinition map, MapGenSettings settings)
    {
        settings.Check();
        var gen = GenMap.From(map, settings, settings.Seed);
        if (settings.Climate is ClimateGrounds.Urban or ClimateGrounds.Coast && settings.Water == WaterKind.Sea)
        {
            var old = gen.Structures.FindAll(s => Array.IndexOf(Dressing.HarbourBuildings, s.Type) >= 0);
            gen.Structures.RemoveAll(old.Contains);
            if (Harbour.Build(gen, clearing: true))
            {
                Dressing.PlaceDocks(gen);
            }
            else
            {
                gen.Structures.AddRange(old);
            }
        }

        Offshore.Place(gen);
        var legend = new List<TextureEntry>(map.TextureLegend);
        var texture = new char[map.Width * map.Height];
        for (var i = 0; i < texture.Length; i++)
        {
            var material = gen.Paint[i] ?? ClimateGrounds.MaterialOf(settings.Climate, gen.Land[i]);
            if (legend.Find(e => e.Material == material) is not { } entry)
            {
                entry = new TextureEntry("abcdefghijklmnopqrstuvwxyz0123456789".First(c => !legend.Exists(e => e.Code == c)), material);
                legend.Add(entry);
            }

            texture[i] = entry.Code;
        }

        var heights = new byte[gen.Levels.Length];
        for (var i = 0; i < heights.Length; i++)
        {
            heights[i] = (byte)gen.Levels[i];
        }

        var laid = new MapDefinition(
            map.Id, map.Name, map.Author, map.Description, map.Width, map.Height, map.Climate, map.Players, map.Starts, [.. legend],
            [.. heights], [.. gen.Land], [.. texture], [.. gen.Ore], [.. gen.Objects], [.. gen.Bridges], [.. gen.Structures],
            map.Lighting, map.Playable, map.Origin);

        // The shores tidied only about the quay: the map was tidied once, and a second time would take another strip at the cliffs' feet.
        var tidy = Shorelines.Tidy(laid, settings.Types);
        var (land, tidied) = (laid.Land.ToArray(), laid.Texture.ToArray());
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                if (NearChange(map, laid, x, y, 3))
                {
                    (land[y * map.Width + x], tidied[y * map.Width + x]) = (tidy.Land[y * map.Width + x], tidy.Texture[y * map.Width + x]);
                }
            }
        }

        return new MapDefinition(
            map.Id, map.Name, map.Author, map.Description, map.Width, map.Height, map.Climate, map.Players, map.Starts, tidy.TextureLegend,
            laid.Heights, [.. land], [.. tidied], laid.Ore, laid.Objects, laid.Bridges, laid.Structures, map.Lighting, map.Playable, map.Origin);
    }

    // Whether the land or the corners of a cell within reach of this one differ between the two maps.
    private static bool NearChange(MapDefinition before, MapDefinition after, int x, int y, int reach)
    {
        for (var ny = IntMath.Max(0, y - reach); ny <= IntMath.Min(before.Height - 1, y + reach); ny++)
        {
            for (var nx = IntMath.Max(0, x - reach); nx <= IntMath.Min(before.Width - 1, x + reach); nx++)
            {
                if (before.Land[ny * before.Width + nx] != after.Land[ny * before.Width + nx]
                    || before.CornerHeight(nx, ny) != after.CornerHeight(nx, ny) || before.CornerHeight(nx + 1, ny + 1) != after.CornerHeight(nx + 1, ny + 1))
                {
                    return true;
                }
            }
        }

        return false;
    }

    // The drills of the settings (M8 step 10b) before the roads, which go round them; the ore under them goes. A field with no cell
    // for one keeps none — the map stays fair, its images alike.
    private static void PlaceDrills(GenMap map)
    {
        var (placements, _) = Drills.For(map.ToDefinition("work", ""), map.Types, map.Symmetry, map.Settings.Drills);
        foreach (var placement in placements)
        {
            map.Objects.Add(placement);
            map.Ore[placement.Cell.Y * map.N + placement.Cell.X] = 0;
        }
    }

    /// <summary>The image <paramref name="k"/> of a cell under the symmetry of the settings, for tests and tools.</summary>
    public static CellPos Image(MapGenSettings settings, int k, CellPos cell) => new Symmetry(settings.Symmetry, settings.Size).Cell(k, cell);
}
