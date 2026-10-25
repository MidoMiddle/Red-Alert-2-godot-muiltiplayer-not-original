using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Rts.Simulation;
using Rts.Simulation.Hashing;
using Rts.Simulation.Replays;
using Rts.Simulation.World;

namespace Rts.Net.Desync;

/// <summary>
/// What a computer keeps of a desync (docs/implementation/M9_network.md, 4.10), in a folder of its choice — the game's is
/// <c>user://desync/&lt;date&gt;_&lt;tick&gt;/</c>. The files carry the player's number, so the folders of two computers can be put
/// together: <c>p2_state.txt</c> (<see cref="StateDump"/> on the tick everyone stopped on), <c>p2_desync.txt</c> (the tick, the
/// hashes, where it stopped) and <c>p2.rtsreplay</c> (the match up to there, with this computer's checksums) — the replay lets
/// <c>tools/Rts.DesyncDiff</c> compute the match again and tell whose state went wrong.
/// </summary>
public static class DesyncDump
{
    /// <summary>Writes the three files; returns their paths.</summary>
    public static IReadOnlyList<string> Save(
        string directory, Desync desync, PlayerId player, GameSimulation simulation, IReadOnlyList<StateChecksum> checksums, ulong dataHash)
    {
        Directory.CreateDirectory(directory);
        var prefix = Path.Combine(directory, "p" + player.Value.ToString(CultureInfo.InvariantCulture));
        var state = prefix + "_state.txt";
        var info = prefix + "_desync.txt";
        var replay = prefix + ".rtsreplay";
        File.WriteAllText(state, StateDump.Write(simulation.World));
        File.WriteAllText(info, string.Join('\n', new[]
        {
            $"player = {player.Value}",
            $"desync_tick = {desync.Tick}",
            $"dump_tick = {simulation.CurrentTick}",
            $"simulation_version = {SimulationVersion.Current}",
        }.Concat(desync.Hashes.Select(pair => $"hash[{pair.Key}] = {pair.Value:x16}"))) + "\n");
        File.WriteAllBytes(replay, ReplayFormat.Write(new Replay(
            SimulationVersion.Current, dataHash, simulation.Settings, simulation.CurrentTick, simulation.ExecutedCommands.ToList(), checksums.ToList())));
        return [state, info, replay];
    }
}
