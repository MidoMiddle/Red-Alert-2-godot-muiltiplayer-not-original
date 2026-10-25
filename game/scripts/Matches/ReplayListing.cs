using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Rts.Simulation.Matches;
using Rts.Simulation.Replays;

namespace Rts.Client.Matches;

/// <summary>
/// The texts of a row of the list of replays (docs/menu/README.md, 3.8; M6 step 15): who played — teams apart by «vs», the people by the
/// names of a network match or as «You», the bots by level and personality — how long, and with which rules. Pure: the screen gives the
/// words of the language.
/// </summary>
public static class ReplayListing
{
    /// <summary>The length of <paramref name="ticks"/> at <paramref name="ticksPerSecond"/>: m:ss, or h:mm:ss from an hour; «—» unknown.</summary>
    public static string Length(int ticks, int ticksPerSecond)
    {
        if (ticks < 0)
        {
            return "—";
        }

        var seconds = ticksPerSecond > 0 ? Math.Max(0, ticks) / ticksPerSecond : 0;
        var time = TimeSpan.FromSeconds(seconds);
        return time.TotalHours >= 1
            ? string.Create(CultureInfo.InvariantCulture, $"{(int)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}")
            : string.Create(CultureInfo.InvariantCulture, $"{time.Minutes}:{time.Seconds:00}");
    }

    /// <summary>
    /// The players of <paramref name="setup"/>, a team after another: «You, Hard bot vs Easy bot». A person is named by the network block
    /// when there is one, else <paramref name="person"/> says it; a bot by <paramref name="bot"/>. Empty without a setup.
    /// </summary>
    public static string Players(MatchSetup? setup, ReplayNetwork? network, Func<MatchPlayer, string> person, Func<MatchPlayer, string> bot)
    {
        if (setup is null)
        {
            return "";
        }

        string Name(MatchPlayer player) =>
            player.Kind == PlayerKind.Ai ? bot(player)
            : network is not null && network.Names.TryGetValue(player.Slot, out var name) ? name
            : person(player);

        var teams = new List<List<string>>();
        var order = new List<int>();
        foreach (var player in setup.Players)
        {
            var team = order.IndexOf(player.Team);
            if (team < 0)
            {
                order.Add(player.Team);
                teams.Add([]);
                team = teams.Count - 1;
            }

            teams[team].Add(Name(player));
        }

        return string.Join(" vs ", teams.Select(names => string.Join(", ", names)));
    }

    /// <summary>The rules a replay was played with: «v72», and <paramref name="old"/> after it when this build cannot play it.</summary>
    public static string Version(ReplayHeader header, string old) =>
        header.Playable ? $"v{header.SimVersion}" : $"v{header.SimVersion} {old}";
}
