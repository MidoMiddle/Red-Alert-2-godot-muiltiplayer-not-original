using Rts.Data.Maps;
using Rts.Simulation.Map;

// dotnet run --project tools/Rts.MapTool -- check <map.rtsmap>               read, print a summary and the hash
// dotnet run --project tools/Rts.MapTool -- canon <map.rtsmap>               rewrite the file in canonical form
// dotnet run --project tools/Rts.MapTool -- convert <test.txt> <out.rtsmap>  convert an ASCII test map of M2
// dotnet run --project tools/Rts.MapTool -- shapes <map.rtsmap>              cell shapes and cell levels as text
// dotnet run --project tools/Rts.MapTool -- relief-test <out.rtsmap>         write the relief test map of step 2
if (args.Length == 2 && args[0] == "check")
{
    return Run(args[1], map =>
    {
        Console.WriteLine($"{map.Id} \"{map.Name}\" {map.Width}x{map.Height}, climate {map.Climate}, {map.Players} players, {map.Starts.Length} starts");
        Console.WriteLine($"objects {map.Objects.Length} · bridges {map.Bridges.Length} · structures {map.Structures.Length} · " +
                          $"highest level {map.Heights.Max()} · ore cells {map.Ore.Count(o => o > 0)}");
        var grid = map.ToMapGrid([TestMapConverter.PineTree]);
        var shapes = Cells(map).Select(cell => grid.Heights.ShapeAt(cell)).ToList();
        Console.WriteLine($"cells: flat {shapes.Count(s => s == CellShape.Flat)} · slope {shapes.Count(s => s == CellShape.Slope)} · cliff {shapes.Count(s => s == CellShape.Cliff)}");
        Console.WriteLine($"hash {MapHasher.Hash(map)}");
        return 0;
    });
}

if (args.Length == 2 && args[0] == "shapes")
{
    return Run(args[1], map =>
    {
        var heights = map.ToMapGrid([]).Heights;
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
    Console.WriteLine($"wrote {args[2]} · {map.Objects.Length} objects · hash {MapHasher.Hash(map)}");
    return 0;
}

Console.Error.WriteLine("usage: check <map.rtsmap> | canon <map.rtsmap> | shapes <map.rtsmap> | convert <test.txt> <out.rtsmap> | relief-test <out.rtsmap>");
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
