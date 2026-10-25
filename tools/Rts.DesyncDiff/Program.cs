using Rts.Net.Desync;
using Rts.Simulation.Replays;

// Rts.DesyncDiff (docs/implementation/M9_network.md, 4.10, step 6).
//   diff <a_state.txt> <b_state.txt>                        the fields two dumps of one tick disagree on, the first one first
//   replay <match.rtsreplay> <state.txt>… [--data <dir>]   computes the replay up to the tick of the dumps headless and says which
//                                                           dump agrees with it — whose computer went wrong
if (args.Length == 3 && args[0] == "diff")
{
    var (first, second) = (File.ReadAllText(args[1]), File.ReadAllText(args[2]));
    if (DumpDiff.TickOf(first) != DumpDiff.TickOf(second))
    {
        Console.WriteLine($"Warning: the dumps are of ticks {DumpDiff.TickOf(first)} and {DumpDiff.TickOf(second)}.");
    }

    var differences = DumpDiff.Compare(first, second);
    Console.WriteLine(DumpDiff.Report(differences));
    return differences.IsEmpty ? 0 : 1;
}

if (args.Length >= 3 && args[0] == "replay")
{
    var dataIndex = Array.IndexOf(args, "--data");
    var data = dataIndex > 0 && dataIndex + 1 < args.Length ? args[dataIndex + 1] : Path.Combine("game", "data");
    var dumps = args.Skip(2).Where((_, i) => dataIndex < 0 || (i + 2 != dataIndex && i + 2 != dataIndex + 1)).ToList();
    var replay = ReplayFormat.Read(File.ReadAllBytes(args[1]));
    var game = Rts.Data.GameData.LoadDirectory(data);
    if (ReplayPlayer.DataWarning(replay, game.Hash) is { } warning)
    {
        Console.WriteLine("Warning: " + warning);
    }

    var verdicts = DesyncReplay.Check(replay, game.Rules, dumps.Select(path => (path, File.ReadAllText(path))).ToList());
    foreach (var verdict in verdicts)
    {
        Console.WriteLine(verdict);
    }

    return verdicts.All(verdict => verdict.Agrees) ? 0 : 1;
}

Console.Error.WriteLine("usage: diff <a_state.txt> <b_state.txt> | replay <match.rtsreplay> <state.txt>… [--data <game/data>]");
return 2;
