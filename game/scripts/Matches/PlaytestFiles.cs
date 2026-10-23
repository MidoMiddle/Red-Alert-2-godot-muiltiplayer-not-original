using System;
using System.IO;
using System.Linq;
using Godot;
using Rts.AI;
using Rts.Client.App;

namespace Rts.Client.Matches;

/// <summary>
/// The files of the tests by people (docs/implementation/M8_ai_difficulty.md, step 14) in <see cref="Folder"/>: a record of every match
/// surveyed (<see cref="PlaytestRecord"/>, JSON), its replay beside it, and <c>protocol.md</c> (<see cref="PlaytestReport"/>) made again
/// of all the records after each one.
/// </summary>
public static class PlaytestFiles
{
    public const string Folder = "user://playtests";
    public const string ProtocolFile = Folder + "/protocol.md";

    /// <summary>
    /// Writes the record of the match of <paramref name="host"/> with <paramref name="answers"/>, its replay and the protocol; returns
    /// the path of the record, or null outside a match.
    /// </summary>
    public static string? Save(SimulationHost host, PlaytestAnswers answers)
    {
        var world = host.Simulation.World;
        if (world.Match is not { } match
            || MatchSummary.Of(world, SimulationHost.LocalPlayer, host.Simulation.Settings.TicksPerSecond) is not { } summary)
        {
            return null;
        }

        var folder = ProjectSettings.GlobalizePath(Folder);
        Directory.CreateDirectory(folder);
        var now = DateTime.Now;
        var stem = $"{now.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture)}-{match.MapId}";
        var replay = host.SaveReplay(Folder, stem) ?? "";
        var journals = Debug.AiJournalFiles.Enabled ? $"user://logs/ai/*-{match.Seed}-p*.log" : "";
        var database = DataFiles.Current.Rules.Database;
        var record = PlaytestRecord.Of(
            summary, match, SimulationHost.LocalPlayer, player => MatchBots.PersonalityOf(match, player, database), answers, now, replay, journals);
        File.WriteAllText(Path.Combine(folder, stem + ".json"), record.ToJson());

        var records = Directory.GetFiles(folder, "*.json")
            .Select(file => PlaytestRecord.FromJson(File.ReadAllText(file)))
            .OfType<PlaytestRecord>();
        File.WriteAllText(ProjectSettings.GlobalizePath(ProtocolFile), PlaytestReport.Markdown(records));
        GD.Print($"[playtest] saved {Folder}/{stem}.json, protocol {ProtocolFile}");
        return $"{Folder}/{stem}.json";
    }
}
