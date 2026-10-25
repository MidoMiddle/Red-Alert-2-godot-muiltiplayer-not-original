using Rts.Data.Maps;
using Rts.Simulation.Map;

// dotnet run --project tools/Rts.MapTool -- check <map.rtsmap>               read, print a summary and the hash
// dotnet run --project tools/Rts.MapTool -- canon <map.rtsmap>               rewrite the file in canonical form
// dotnet run --project tools/Rts.MapTool -- convert <test.txt> <out.rtsmap>  convert an ASCII test map of M2
// dotnet run --project tools/Rts.MapTool -- shapes <map.rtsmap>              cell shapes and cell levels as text
// dotnet run --project tools/Rts.MapTool -- passability <map.rtsmap> <type>  where a locomotor goes and how fast
// dotnet run --project tools/Rts.MapTool -- relief-test <out.rtsmap>         write the relief test map of step 2
// dotnet run --project tools/Rts.MapTool -- generate <out.rtsmap> [--seed N] [--size 64|96|128|160] [--players 2|4]
//     [--symmetry mirror|rotate180|rotate90] [--relief flat|hilly|mountainous] [--water none|rivers|lakes]
//     [--forest low|medium|high] [--resources low|medium|high]       a random map (step 13)
// dotnet run --project tools/Rts.MapTool -- ready-maps game/data/maps        write the four ready maps of step 14
// dotnet run --project tools/Rts.MapTool -- economy [map] [minutes]               the end-to-end run of M3 step 14 on game/data: MCV, base,
//     ten tanks; prints the timeline, credits and income by the minute
// dotnet run --project tools/Rts.MapTool -- balance [credits]                   balance v0 of M4 step 14: every ground fighter of the Union
//     against every other on equal credits (2700 without a number); prints the winner and its value left
// dotnet run --project tools/Rts.MapTool -- rules [folder]                   load the data files below a folder: counts and data hash, or every error;
//     without a folder game/data, with what the engine needs from it
if (args.Length == 2 && args[0] == "check")
{
    return Run(args[1], map =>
    {
        Console.WriteLine($"{map.Id} \"{map.Name}\" {map.Width}x{map.Height}, climate {map.Climate}, lighting {map.Lighting}, {map.Players} players, {map.Starts.Length} starts");
        Console.WriteLine($"objects {map.Objects.Length} · bridges {map.Bridges.Length} · structures {map.Structures.Length} · " +
                          $"highest level {map.Heights.Max()} · ore cells {map.Ore.Count(o => o > 0)}");
        var grid = map.ToMapGrid();
        var shapes = Cells(map).Select(cell => grid.Heights.ShapeAt(cell)).ToList();
        Console.WriteLine($"cells: flat {shapes.Count(s => s == CellShape.Flat)} · slope {shapes.Count(s => s == CellShape.Slope)} · cliff {shapes.Count(s => s == CellShape.Cliff)}");
        Console.WriteLine($"hash {MapHasher.Hash(map)}");
        foreach (var problem in MapValidator.Check(map))
        {
            Console.WriteLine($"{problem.Level}: {problem.Message}" + (problem.Cell is { } at ? $" at {at.X},{at.Y}" : ""));
        }

        return 0;
    });
}

if (args.Length is 1 or 2 && args[0] == "rules")
{
    var folder = args.Length == 2 ? args[1] : Path.Combine("game", Rts.Data.GameData.Folder);
    try
    {
        var rules = args.Length == 2 ? Rts.Data.RulesLoader.LoadDirectory(folder) : Rts.Data.GameData.LoadDirectory(folder).Database;
        Console.WriteLine($"units {rules.Units.Length} · buildings {rules.Buildings.Length} · weapons {rules.Weapons.Length} · " +
                          $"locomotors {rules.Locomotors.Length} · terrains {rules.Terrains.Length} · factions {rules.Factions.Length} · " +
                          $"armor {rules.Armors.Length} · warheads {rules.Warheads.Length} · projectiles {rules.Projectiles.Length} · " +
                          $"build orders {rules.BuildOrders.Length}");
        Console.WriteLine($"data hash {Rts.Simulation.Rules.DataHasher.Compute(rules):x16}");
        return 0;
    }
    catch (Rts.Data.RulesException e)
    {
        foreach (var error in e.Errors)
        {
            Console.Error.WriteLine(error.FileName.Length == 0 ? error.Reason : $"{Path.Combine(folder, error.FileName)}:{error.Line}:{error.Column}: {error.Reason}");
        }

        Console.Error.WriteLine($"{e.Errors.Count} errors");
        return 1;
    }
}

if (args.Length is 1 or 2 && args[0] == "balance")
{
    var credits = args.Length == 2 ? int.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture) : 2700;
    var data = Rts.Data.GameData.LoadDirectory(Path.Combine("game", Rts.Data.GameData.Folder));
    Console.Write(Rts.MapTool.Skirmish.Matrix(data.Rules, credits));
    return 0;
}

if (args.Length is >= 1 and <= 3 && args[0] == "economy")
{
    var mapFile = args.Length >= 2 ? args[1] : "frozen-crossing";
    var minutes = args.Length == 3 ? int.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture) : 20;
    var data = Rts.Data.GameData.LoadDirectory(Path.Combine("game", Rts.Data.GameData.Folder));
    return Run(Path.Combine("game", Rts.Data.GameData.Folder, "maps", mapFile + ".rtsmap"), map =>
    {
        Console.Write(Rts.MapTool.EconomyRun.Format(Rts.MapTool.EconomyRun.Play(map, data.Rules, maxMinutes: minutes)));
        return 0;
    });
}

if (args.Length == 2 && args[0] == "shapes")
{
    return Run(args[1], map =>
    {
        var heights = map.ToMapGrid().Heights;
        Console.WriteLine("shape: . flat  / slope  # cliff          level of each cell (its lowest corner, 0-9 a-e)");
        for (var y = 0; y < map.Height; y++)
        {
            var shapes = new char[map.Width];
            var levels = new char[map.Width];
            for (var x = 0; x < map.Width; x++)
            {
                var cell = new Rts.Simulation.Geometry.CellPos(x, y);
                shapes[x] = heights.ShapeAt(cell) switch { CellShape.Flat => '.', CellShape.Slope => '/', _ => '#' };
                levels[x] = MapNames.HeightCodes[heights.CellLevel(cell)];
            }

            Console.WriteLine($"{new string(shapes)}   {new string(levels)}");
        }

        return 0;
    });
}

if (args.Length == 3 && args[0] == "passability")
{
    if (!Enum.TryParse<Locomotor>(args[2], ignoreCase: true, out var locomotor) || !LandSpeeds.IsKnown(locomotor))
    {
        Console.Error.WriteLine($"unknown type '{args[2]}': foot, track, wheel, amphibious, naval or hover");
        return 2;
    }

    return Run(args[1], map =>
    {
        var grid = map.ToMapGrid();
        Console.WriteLine($"{locomotor}: # impassable  . 100 %  digit = tens of percent when slower  + faster");
        for (var y = 0; y < map.Height; y++)
        {
            var line = new char[map.Width];
            for (var x = 0; x < map.Width; x++)
            {
                var percent = grid.SpeedPercent(new Rts.Simulation.Geometry.CellPos(x, y), locomotor);
                line[x] = percent == 0 ? '#' : percent == 100 ? '.' : percent > 100 ? '+' : (char)('0' + percent / 10);
            }

            Console.WriteLine(new string(line));
        }

        return 0;
    });
}

if (args.Length == 2 && args[0] == "relief-test")
{
    var relief = Rts.MapTool.ReliefTestMap.Build();
    File.WriteAllText(args[1], RtsMapWriter.Write(relief));
    Console.WriteLine($"wrote {args[1]} · hash {MapHasher.Hash(relief)}");
    return 0;
}

if (args.Length == 2 && args[0] == "canon")
{
    return Run(args[1], map =>
    {
        File.WriteAllText(args[1], RtsMapWriter.Write(map));
        Console.WriteLine($"wrote {args[1]} · hash {MapHasher.Hash(map)}");
        return 0;
    });
}

if (args.Length == 3 && args[0] == "convert")
{
    var id = Path.GetFileNameWithoutExtension(args[2]);
    var map = TestMapConverter.Convert(TestMapParser.Parse(File.ReadAllText(args[1])), id, char.ToUpperInvariant(id[0]) + id[1..]);
    File.WriteAllText(args[2], RtsMapWriter.Write(map));
    Console.WriteLine($"wrote {args[2]} · {map.Objects.Length} objects · {map.Bridges.Length} bridges · {map.Structures.Length} buildings · hash {MapHasher.Hash(map)}");
    return 0;
}

if (args.Length == 2 && args[0] == "ready-maps")
{
    foreach (var file in Rts.MapTool.ReadyMaps.Files)
    {
        var map = Rts.MapTool.ReadyMaps.Build(file);
        var path = Path.Combine(args[1], file + ".rtsmap");
        File.WriteAllText(path, RtsMapWriter.Write(map));
        Console.WriteLine($"wrote {path} · {map.Width}x{map.Height} · {map.Players} players · {map.Objects.Length} objects · {map.Bridges.Length} bridges · " +
                          $"{map.Structures.Length} buildings · {MapValidator.Check(map).Count} warnings · hash {MapHasher.Hash(map)}");
    }

    return 0;
}

// M8 step 10b: the drills of the fields on maps the generator made before it had them; the symmetry comes from the description.
if (args.Length >= 2 && args[0] == "add-drills")
{
    var kindIndex = Array.IndexOf(args, "--drills");
    var kind = kindIndex >= 0 && kindIndex + 1 < args.Length && args[kindIndex + 1] == "ore" ? Rts.MapGen.DrillKind.Ore : Rts.MapGen.DrillKind.OreAndGems;
    var failed = 0;
    var files = args.Skip(1).Where((_, i) => i + 1 != kindIndex && i + 1 != kindIndex + 1);
    foreach (var path in files)
    {
        try
        {
            var map = RtsMapReader.Read(File.ReadAllText(path));
            var symmetry = System.Text.RegularExpressions.Regex.Match(map.Description, @"^Generated from seed \d+: \d players, (\w+),");
            if (!symmetry.Success || !Enum.TryParse<Rts.MapGen.MapSymmetry>(symmetry.Groups[1].Value, out var kindOfSymmetry))
            {
                throw new ArgumentException("its description does not say the symmetry: it was not made by the generator");
            }

            var drilled = Rts.MapGen.Drills.AddTo(map, kindOfSymmetry, kind);
            var errors = MapValidator.Check(drilled).Where(problem => problem.Level == ProblemLevel.Error).ToList();
            if (errors.Count > 0)
            {
                throw new ArgumentException(string.Join("; ", errors.Select(problem => $"{problem.Message} [{problem.Cell}]")));
            }

            File.WriteAllText(path, RtsMapWriter.Write(drilled));
            var drills = drilled.Objects.Where(o => o.Type is Rts.MapGen.Drills.OreSource or Rts.MapGen.Drills.GemSource);
            Console.WriteLine($"{path}: {string.Join(", ", drills.Select(d => $"{d.Type} {d.Cell.X},{d.Cell.Y}"))} · {MapValidator.Check(drilled).Count} warnings");
        }
        catch (Exception e) when (e is ArgumentException or InvalidDataException or IOException)
        {
            Console.Error.WriteLine($"{path}: not changed — {e.Message}");
            failed++;
        }
    }

    return failed == 0 ? 0 : 1;
}

// Loose ends, stage 2: the bridge repair huts on maps the generator made before it had them; the symmetry comes from the description.
if (args.Length >= 2 && args[0] == "add-huts")
{
    var failed = 0;
    foreach (var path in args.Skip(1))
    {
        try
        {
            var map = RtsMapReader.Read(File.ReadAllText(path));
            var symmetry = System.Text.RegularExpressions.Regex.Match(map.Description, @"^Generated from seed \d+: \d players, (\w+),");
            if (!symmetry.Success || !Enum.TryParse<Rts.MapGen.MapSymmetry>(symmetry.Groups[1].Value, out var kindOfSymmetry))
            {
                throw new ArgumentException("its description does not say the symmetry: it was not made by the generator");
            }

            var hutted = BridgeHuts.AddTo(map, new Rts.MapGen.Symmetry(kindOfSymmetry, map.Width).Images);
            var errors = MapValidator.Check(hutted).Where(problem => problem.Level == ProblemLevel.Error).ToList();
            if (errors.Count > 0)
            {
                throw new ArgumentException(string.Join("; ", errors.Select(problem => $"{problem.Message} [{problem.Cell}]")));
            }

            File.WriteAllText(path, RtsMapWriter.Write(hutted));
            var huts = hutted.Structures.Where(s => s.Type == BridgeHuts.Hut);
            Console.WriteLine($"{path}: {map.Bridges.Length} bridges, huts {string.Join(", ", huts.Select(h => $"{h.Cell.X},{h.Cell.Y}"))}");
        }
        catch (Exception e) when (e is ArgumentException or InvalidDataException or IOException)
        {
            Console.Error.WriteLine($"{path}: not changed — {e.Message}");
            failed++;
        }
    }

    return failed == 0 ? 0 : 1;
}

if (args.Length >= 2 && args[0] == "generate")
{
    string Option(string name, string fallback)
    {
        var index = Array.IndexOf(args, "--" + name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
    }

    T Choice<T>(string name, T fallback)
        where T : struct, Enum => Enum.TryParse<T>(Option(name, fallback.ToString()), ignoreCase: true, out var value)
            ? value
            : throw new ArgumentException($"--{name} {Option(name, "")} is not one of {string.Join(", ", Enum.GetNames<T>()).ToLowerInvariant()}");

    var symmetry = Choice("symmetry", Rts.MapGen.MapSymmetry.Mirror);
    var settings = new Rts.MapGen.MapGenSettings
    {
        Seed = uint.Parse(Option("seed", "1"), System.Globalization.CultureInfo.InvariantCulture),
        Size = int.Parse(Option("size", "64"), System.Globalization.CultureInfo.InvariantCulture),
        Symmetry = symmetry,
        Players = int.Parse(Option("players", symmetry == Rts.MapGen.MapSymmetry.Rotate90 ? "4" : "2"), System.Globalization.CultureInfo.InvariantCulture),
        Relief = Choice("relief", Rts.MapGen.ReliefKind.Hilly),
        Water = Choice("water", Rts.MapGen.WaterKind.Rivers),
        SeaPercent = int.Parse(Option("sea-percent", "0"), System.Globalization.CultureInfo.InvariantCulture),
        Forest = Choice("forest", Rts.MapGen.Amount.Medium),
        Resources = Choice("resources", Rts.MapGen.Amount.Medium),
        Drills = Option("drills", "gems") switch
        {
            "none" => Rts.MapGen.DrillKind.None,
            "ore" => Rts.MapGen.DrillKind.Ore,
            "gems" => Rts.MapGen.DrillKind.OreAndGems,
            var other => throw new ArgumentException($"--drills {other} is not one of none, ore, gems"),
        },
        Lighting = Option("lighting", MapLighting.Default),
        Climate = Option("climate", ClimateGrounds.Winter),
    };
    var (map, seed) = Rts.MapGen.MapGenerator.Generate(settings);

    // A ready map of the generator gets an id and a name of its own (--id, --name); the description keeps the settings.
    if (Option("id", "") is { Length: > 0 } id)
    {
        map = new MapDefinition(
            id, Option("name", id), map.Author, map.Description, map.Width, map.Height, map.Climate, map.Players, map.Starts, map.TextureLegend,
            map.Heights, map.Land, map.Texture, map.Ore, map.Objects, map.Bridges, map.Structures, map.Lighting, map.Playable, map.Origin);
    }

    File.WriteAllText(args[1], RtsMapWriter.Write(map));
    var warnings = MapValidator.Check(map);
    Console.WriteLine($"wrote {args[1]} · seed {seed} · {map.Width}x{map.Height} · {map.Objects.Length} objects · {map.Bridges.Length} bridges · " +
                      $"{map.Structures.Length} buildings · {warnings.Count} warnings · hash {MapHasher.Hash(map)}");
    return 0;
}

Console.Error.WriteLine("usage: check <map.rtsmap> | canon <map.rtsmap> | shapes <map.rtsmap> | passability <map.rtsmap> <type> | convert <test.txt> <out.rtsmap> | relief-test <out.rtsmap> | generate <out.rtsmap> [options] | add-drills <map.rtsmap>… [--drills ore|gems] | ready-maps <dir> | rules [folder] | economy [map] [minutes]");
return 2;

static IEnumerable<Rts.Simulation.Geometry.CellPos> Cells(MapDefinition map) =>
    Enumerable.Range(0, map.Height).SelectMany(y => Enumerable.Range(0, map.Width).Select(x => new Rts.Simulation.Geometry.CellPos(x, y)));

static int Run(string path, Func<MapDefinition, int> action)
{
    try
    {
        return action(RtsMapReader.Read(File.ReadAllText(path)));
    }
    catch (RtsMapFormatException e)
    {
        Console.Error.WriteLine($"{path}:{e.Line}:{e.Column}: {e.Reason}");
        return 1;
    }
}
