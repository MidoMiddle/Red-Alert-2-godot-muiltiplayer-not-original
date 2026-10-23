using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using Rts.AI;
using Rts.AI.Debug;
using Rts.Simulation.Matches;

namespace Rts.Client.Debug;

/// <summary>
/// The journals of the decisions of the bots (docs/implementation/M8_ai_difficulty.md, 4.9, step 12): in a developer build, or with
/// <c>--ai-debug</c>, every bot of a match gets an <see cref="AiDecisionLog"/> — the debug window shows its last decisions — and each
/// decision goes as a line into <c>user://logs/ai/&lt;time&gt;-&lt;seed&gt;-p&lt;N&gt;.log</c>. Off in a release build without the flag.
/// </summary>
public static class AiJournalFiles
{
    /// <summary>Whether the bots keep journals and the debug window and overlay open: a developer build, or <c>--ai-debug</c>.</summary>
    public static bool Enabled => OS.IsDebugBuild() || Array.IndexOf(OS.GetCmdlineUserArgs(), "--ai-debug") >= 0;

    /// <summary>Gives every bot of <paramref name="match"/> its journal and its file.</summary>
    public static void Attach(IReadOnlyList<AiPlayer> bots, MatchSetup match)
    {
        if (!Enabled || bots.Count == 0)
        {
            return;
        }

        var folder = ProjectSettings.GlobalizePath("user://logs/ai");
        Directory.CreateDirectory(folder);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
        foreach (var bot in bots)
        {
            var path = Path.Combine(folder, $"{stamp}-{match.Seed}-p{bot.Player.Value}.log");
            File.WriteAllText(path, $"{bot.Player} {bot.Profile ?? "bot"} on {match.MapId}, seed {match.Seed}\n");
            var log = new AiDecisionLog();
            log.Recorded += decision => File.AppendAllText(path, decision + "\n");
            bot.Log = log;
        }

        GD.Print($"[ai] journals of the bots in {folder}");
    }
}
