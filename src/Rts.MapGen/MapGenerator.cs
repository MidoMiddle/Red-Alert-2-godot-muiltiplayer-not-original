using System;
using System.Collections.Generic;
using Rts.Data.Maps;
using Rts.MapGen.Stages;
using Rts.Simulation.Geometry;

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
/// shores, <see cref="Bridges"/>, <see cref="Ore"/>, the drills of the fields (<see cref="Drills"/>, M8 step 10b), <see cref="Roads"/>, an oil derrick by the contested ore, rough patches
/// and the forest (<see cref="Features"/>). The map is symmetric by construction (<see cref="Symmetry"/>). A map with errors
/// of <see cref="MapValidator"/> is made again from seed + 1, at most <see cref="Attempts"/> times; the seed used is in the
/// map's id (<c>random-SEED</c>) and description.
/// </summary>
public static class MapGenerator
{
    public const int Attempts = 10;

    /// <summary>The map and the seed it was made from (the asked one, or a later one after a failed check).</summary>
    public static (MapDefinition Map, uint Seed) Generate(MapGenSettings settings) => Generate(settings, MapValidator.Check);

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

        var soften = settings.Relief == ReliefKind.Mountainous ? Relief.RampMask(map) : null;
        var riverMask = new bool[map.Levels.Length];
        if (settings.Water == WaterKind.Rivers)
        {
            Water.Rivers(map, riverMask);
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

        Water.Shores(map);
        if (settings.Water == WaterKind.Rivers)
        {
            Bridges.Place(map);
        }

        Ore.Place(map, out var contested);
        PlaceDrills(map);
        Roads.Place(map);
        Features.Buildings(map, contested);
        Features.Rough(map);
        Features.Forest(map);
        Features.Ground(map);

        var name = $"Random {settings.Size} ({seed})";
        var description = $"Generated from seed {seed}: {settings.Players} players, {settings.Symmetry}, {settings.Relief}, water {settings.Water}, " +
                          $"forest {settings.Forest}, resources {settings.Resources}.";
        return map.ToDefinition(name, Drills.WithDrills(description, settings.Drills));
    }

    // The drills of the settings (M8 step 10b) before the roads, which go round them; the ore under them goes. A field with no cell
    // for one keeps none — the map stays fair, its images alike.
    private static void PlaceDrills(GenMap map)
    {
        var (placements, _) = Drills.For(map.ToDefinition("work", ""), map.Symmetry, map.Settings.Drills);
        foreach (var placement in placements)
        {
            map.Objects.Add(placement);
            map.Ore[placement.Cell.Y * map.N + placement.Cell.X] = 0;
        }
    }

    /// <summary>The image <paramref name="k"/> of a cell under the symmetry of the settings, for tests and tools.</summary>
    public static CellPos Image(MapGenSettings settings, int k, CellPos cell) => new Symmetry(settings.Symmetry, settings.Size).Cell(k, cell);
}
