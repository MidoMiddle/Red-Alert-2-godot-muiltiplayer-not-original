using System.Globalization;
using Rts.Data;
using Rts.Data.Maps;
using Rts.Simulation.Replays;
using Rts.Simulation.World;

namespace Rts.Arena;

/// <summary>
/// The command line of the Arena (docs/implementation/M8_ai_difficulty.md, 4.10, step 4): <c>run</c> plays matches and writes their results,
/// <c>replay</c> checks one. Code 0 — all well; 1 — a replay does not match; 2 — the command, a profile, the map or the data is wrong.
/// </summary>
public static class ArenaCommand
{
    private const string Usage =
        "usage: rts-arena run --config <matrix.yaml> [--out folder] [--jobs N] [--data folder]\n" +
        "       rts-arena run --a <profile> --b <profile> --map <id> [--games N] [--minutes M] [--seed S] [--out folder] [--jobs N] [--data folder]\n" +
        "       rts-arena replay <file.rtsreplay> [--data folder]\n" +
        "a profile is level[:personality[:faction]]: easy, medium, hard or brutal; balanced and union by default";

    /// <summary>The columns of <c>results.csv</c>, the same on every run of the same matches.</summary>
    public static readonly string[] ResultColumns =
    [
        "game", "map", "seed", "a", "b", "a_start", "b_start", "winner", "end_tick", "end_time",
        "a_units_built", "a_units_lost", "a_buildings_built", "a_buildings_lost", "a_harvested", "a_spent", "a_first_attack", "a_units", "a_harvesters_killed",
        "b_units_built", "b_units_lost", "b_buildings_built", "b_buildings_lost", "b_harvested", "b_spent", "b_first_attack", "b_units", "b_harvesters_killed",
        "replay",
    ];

    public static int Execute(string[] args, TextWriter output, TextWriter error)
    {
        try
        {
            return args.Length > 0 && args[0] == "run" ? Run(Options(args, 1), output)
                : args.Length > 1 && args[0] == "replay" ? Replay(args[1], Options(args, 2), output)
                : throw new ArenaException(args.Length == 0 ? "no command" : $"unknown command '{args[0]}'");
        }
        catch (ArenaException e)
        {
            error.WriteLine($"rts-arena: {e.Message}");
            error.WriteLine(Usage);
            return 2;
        }
        catch (RulesException e)
        {
            foreach (var problem in e.Errors)
            {
                error.WriteLine($"{problem.FileName}:{problem.Line}:{problem.Column}: {problem.Reason}");
            }

            error.WriteLine($"rts-arena: the data has {e.Errors.Count} errors");
            return 2;
        }
    }

    private static int Run(Dictionary<string, string> options, TextWriter output)
    {
        var data = Data(options);
        ArenaConfig config;
        if (options.TryGetValue("config", out var file))
        {
            if (!File.Exists(file))
            {
                throw new ArenaException($"no matrix '{file}'");
            }

            config = ArenaConfig.Read(file, File.ReadAllText(file), data.Database, DataFolder(options));
        }
        else
        {
            // One pair on one map, every replay kept and checked: the minimal Arena of step 4.
            var a = BotProfile.Parse(Required(options, "a"), data.Database);
            var b = BotProfile.Parse(Required(options, "b"), data.Database);
            var id = Required(options, "map");
            var map = new MapSpec(id, Map(options, id), null);
            config = new ArenaConfig(
                Number(options, "games", 1), true, Number(options, "minutes", 60) * ArenaMatch.TicksPerMinute, (ulong)Number(options, "seed", 1000),
                [map], [new ArenaPair(a, b)], ReplaySelection.Every);
        }

        var folder = options.GetValueOrDefault("out") ?? Path.Combine("arena-results", DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
        return ArenaRunner.Run(config, data, folder, Number(options, "jobs", Environment.ProcessorCount), output);
    }

    private static int Replay(string file, Dictionary<string, string> options, TextWriter output)
    {
        var data = Data(options);
        if (options.ContainsKey("a") || options.ContainsKey("b"))
        {
            throw new ArenaException("--a and --b are not needed: the replay keeps the level and the personality of every bot");
        }

        if (!File.Exists(file))
        {
            throw new ArenaException($"no replay '{file}'");
        }

        Replay replay;
        try
        {
            replay = ReplayFormat.Read(File.ReadAllBytes(file));
        }
        catch (InvalidDataException e)
        {
            throw new ArenaException($"'{file}' is not a replay: {e.Message}");
        }

        var check = ArenaMatch.Check(replay, data.Rules);
        output.WriteLine($"{file}: {check}");
        return check == "match" ? 0 : 1;
    }

    /// <summary>The <c>--key value</c> pairs after <paramref name="from"/>.</summary>
    private static Dictionary<string, string> Options(string[] args, int from)
    {
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = from; i < args.Length; i += 2)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal) || i + 1 >= args.Length)
            {
                throw new ArenaException($"'{args[i]}' needs to be --key value");
            }

            options[args[i][2..]] = args[i + 1];
        }

        return options;
    }

    private static string Required(Dictionary<string, string> options, string key) =>
        options.GetValueOrDefault(key) ?? throw new ArenaException($"--{key} is missing");

    private static int Number(Dictionary<string, string> options, string key, int fallback)
    {
        if (!options.TryGetValue(key, out var text))
        {
            return fallback;
        }

        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value > 0
            ? value
            : throw new ArenaException($"--{key} must be a whole number above zero, not '{text}'");
    }

    /// <summary>The folder of the data: <c>--data</c>, else <c>game/data</c> here or in a folder above.</summary>
    private static string DataFolder(Dictionary<string, string> options)
    {
        if (options.TryGetValue("data", out var given))
        {
            return Directory.Exists(given) ? given : throw new ArenaException($"no data folder '{given}'");
        }

        for (var folder = new DirectoryInfo(Directory.GetCurrentDirectory()); folder is not null; folder = folder.Parent)
        {
            var data = Path.Combine(folder.FullName, "game", GameData.Folder);
            if (Directory.Exists(Path.Combine(data, "maps")))
            {
                return data;
            }
        }

        throw new ArenaException("no game/data here or above: give --data");
    }

    private static GameData Data(Dictionary<string, string> options) => GameData.LoadDirectory(DataFolder(options));

    private static MapDefinition Map(Dictionary<string, string> options, string id)
    {
        var maps = Path.Combine(DataFolder(options), "maps");
        var file = Path.Combine(maps, id + ".rtsmap");
        if (!File.Exists(file))
        {
            var known = Directory.GetFiles(maps, "*.rtsmap").Select(Path.GetFileNameWithoutExtension).Order(StringComparer.Ordinal);
            throw new ArenaException($"no map '{id}' in {maps}: one of {string.Join(", ", known)}");
        }

        try
        {
            return RtsMapReader.Read(File.ReadAllText(file));
        }
        catch (RtsMapFormatException e)
        {
            throw new ArenaException($"the map '{id}' cannot be read: {e.Message}");
        }
    }
}
