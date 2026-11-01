using Rts.Client.Units;
using Rts.Data.Tests.Maps;

namespace Rts.Data.Tests.Matches;

/// <summary>
/// The table of the people's clips, game/data/visuals/human_clips.yaml (docs/art/mixamo.md): the clips the game needs are in it, the
/// clips only some people take name units of the game, the file of the clips is there; the reader refuses what it does not know.
/// </summary>
public sealed class HumanClipTableTests
{
    private static string Root => Path.Combine(SampleMaps.RepoRoot, "game");

    private static HumanClipTable Table() =>
        HumanClipTable.Read(File.ReadAllText(Path.Combine(Root, "data", "visuals", "human_clips.yaml")));

    [Fact]
    public void The_table_has_the_clips_the_people_play_and_their_file_is_there()
    {
        var table = Table();
        Assert.True(File.Exists(Path.Combine(Root, "assets", "models", "unit", "human_clips", "human_clips.glb")));
        foreach (var family in new[] { "idle", "walk", "run", "die" })
        {
            Assert.NotEmpty(table.Family(family, "civ_man"));
        }

        Assert.Contains("idle_0", table.Clips.Keys);
        foreach (var name in new[] { "crouch_down", "crouch", "crouch_up", "crouch_sprint" })
        {
            Assert.Contains(name, table.Clips.Keys);
        }

        // A walk or a run carries the person on: its stride matches the feet to the ground. A fall plays once.
        Assert.All(table.Family("walk", "civ_woman").Concat(table.Family("run", "civ_man")), c => Assert.True(c.Stride > 0f && c.Loop, c.Name));
        Assert.All(table.Family("die", "civ_man"), c => Assert.False(c.Loop, c.Name));
    }

    [Fact]
    public void The_catwalk_is_the_womens()
    {
        var table = Table();
        var database = GameDataFiles.Rules.Database;
        Assert.All(table.Only.Values.SelectMany(ids => ids), id => Assert.True(database.TryGetUnit(id, out _), $"{id} is no unit"));
        Assert.Contains(table.Family("walk", "civ_woman"), c => c.Name == "walk_woman_0");
        Assert.DoesNotContain(table.Family("walk", "civ_man"), c => c.Name == "walk_woman_0");
        // crouch_down is a clip of its own, not of a family "crouch".
        Assert.Empty(table.Family("crouch", "civ_man"));
    }

    [Theory]
    [InlineData("clips:\n  idle_0: { loop: true, length: 2, speed: 1 }\n", "unknown key 'speed'")]
    [InlineData("clips:\n  idle_0: { loop: maybe, length: 2 }\n", "loop is true or false")]
    [InlineData("clips:\n  idle_0: { loop: true, length: 0 }\n", "length is a number above 0")]
    [InlineData("clips:\n  idle_0: { loop: true, length: 2 }\nonly:\n  walk_9: [civ_woman]\n", "'walk_9' is no clip")]
    public void The_reader_refuses_what_it_does_not_know(string text, string message)
    {
        var error = Assert.Throws<HumanClipTableException>(() => HumanClipTable.Read(text));
        Assert.Contains(message, error.Message);
    }
}
