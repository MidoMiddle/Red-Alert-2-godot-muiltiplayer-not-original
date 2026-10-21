using Rts.Data.Maps;
using Rts.Simulation.Map;

// dotnet run --project tools/Rts.MapTool -- check <map.rtsmap>               read, print a summary and the hash
// dotnet run --project tools/Rts.MapTool -- canon <map.rtsmap>               rewrite the file in canonical form
// dotnet run --project tools/Rts.MapTool -- convert <test.txt> <out.rtsmap>  convert an ASCII test map of M2
if (args.Length == 2 && args[0] == "check")
{
    return Run(args[1], map =>
    {
        Console.WriteLine($"{map.Id} \"{map.Name}\" {map.Width}x{map.Height}, climate {map.Climate}, {map.Players} players, {map.Starts.Length} starts");
        Console.WriteLine($"objects {map.Objects.Length} · bridges {map.Bridges.Length} · structures {map.Structures.Length} · " +
                          $"highest level {map.Heights.Max()} · ore cells {map.Ore.Count(o => o > 0)}");
        Console.WriteLine($"hash {MapHasher.Hash(map)}");
        return 0;
    });
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

Console.Error.WriteLine("usage: check <map.rtsmap> | canon <map.rtsmap> | convert <test.txt> <out.rtsmap>");
return 2;

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
