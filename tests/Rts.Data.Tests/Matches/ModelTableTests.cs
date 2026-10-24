using Rts.Client.Models;
using Rts.Data.Tests.Maps;
using Rts.Simulation.Map;
using Rts.Simulation.World;

namespace Rts.Data.Tests.Matches;

/// <summary>
/// The table of models, game/data/visuals/models.yaml (docs/art/model_standards.md): every file in it is there, every key names
/// something of the game; the reader refuses what it does not know.
/// </summary>
public sealed class ModelTableTests
{
    private static readonly string[] TreeShapes = ["pine", "broadleaf_small", "broadleaf_big", "palm", "cactus"];
    private static readonly string[] OreKinds = ["ore", "gems"];

    private static string Root => Path.Combine(SampleMaps.RepoRoot, "game");

    private static IReadOnlyDictionary<string, ModelEntry> Table() =>
        ModelTable.Read(File.ReadAllText(Path.Combine(Root, "data", "visuals", "models.yaml")));

    [Fact]
    public void Every_model_of_the_table_has_its_file_and_names_something_of_the_game()
    {
        var database = GameDataFiles.Rules.Database;
        var table = Table();
        Assert.NotEmpty(table);
        foreach (var (key, entry) in table)
        {
            Assert.True(File.Exists(Path.Combine(Root, "assets", "models", entry.File)), $"{key}: no file {entry.File}");
            var id = key[(key.IndexOf('/') + 1)..];
            var known = key[..key.IndexOf('/')] switch
            {
                "building" => database.TryGetBuilding(id, out _) || BuildingTypes.TryGetById(id, out _),
                "unit" => database.TryGetUnit(id, out _),
                "object" => MapObjectTypes.TryGetById(id, out _) && id != "pine_tree",
                "tree" => TreeShapes.Contains(id),
                "ore" => OreKinds.Contains(id),
                _ => false,
            };
            Assert.True(known, $"{key} names nothing of the game");
        }
    }

    [Fact]
    public void An_entry_takes_its_placement_and_its_climates()
    {
        var table = ModelTable.Read("""
            format: models/1
            models:
              tree/palm: { file: desert/palm/palm_01.glb, scale: 1.5, yaw: 90, offset: [0, -0.1, 0.25], climates: [desert] }
              building/reactor: { file: soviet/reactor/reactor.glb, height: 2.5 }
            """);

        var palm = table["tree/palm"];
        Assert.Equal(("desert/palm/palm_01.glb", 1.5f, 90f, (0f, -0.1f, 0.25f)), (palm.File, palm.Scale, palm.Yaw, palm.Offset));
        Assert.True(palm.DrawnIn("desert"));
        Assert.False(palm.DrawnIn("winter"));
        Assert.Equal((1f, 0f, 2.5f), (table["building/reactor"].Scale, table["building/reactor"].Yaw, table["building/reactor"].Height));
        Assert.True(table["building/reactor"].DrawnIn("winter"));
    }

    [Theory]
    [InlineData("format: models/2\nmodels:\n", "the format")]
    [InlineData("format: models/1\nmodels:\n  tank/heavy: { file: a/b.glb }\n", "is not a key")]
    [InlineData("format: models/1\nmodels:\n  unit/heavy_tank: { file: a/b.fbx }\n", "is not a .glb path")]
    [InlineData("format: models/1\nmodels:\n  unit/heavy_tank: { file: ../b.glb }\n", "is not a .glb path")]
    [InlineData("format: models/1\nmodels:\n  unit/heavy_tank: { file: a/b.glb, size: 2 }\n", "unknown key 'size'")]
    [InlineData("format: models/1\nmodels:\n  unit/heavy_tank: { file: a/b.glb, scale: 0 }\n", "scale must be")]
    [InlineData("format: models/1\nmodels:\n  unit/heavy_tank: { scale: 2 }\n", "has no file")]
    [InlineData("format: models/1\nmodels:\n  unit/heavy_tank: { file: a/b.glb, offset: [1, 2] }\n", "offset must be")]
    public void The_reader_refuses_what_it_does_not_know(string text, string message)
    {
        var error = Assert.Throws<ModelTableException>(() => ModelTable.Read(text.Replace("\\n", "\n")));
        Assert.Contains(message, error.Message);
    }
}
