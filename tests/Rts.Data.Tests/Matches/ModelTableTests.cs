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
            Assert.All(entry.Files, file => Assert.True(File.Exists(Path.Combine(Root, "assets", "models", file)), $"{key}: no file {file}"));
            Assert.All(new[] { entry.Damaged, entry.Occupied, entry.Ruins }.OfType<string>(),
                file => Assert.True(File.Exists(Path.Combine(Root, "assets", "models", file)), $"{key}: no file {file}"));
            var id = key[(key.IndexOf('/') + 1)..];
            var known = key[..key.IndexOf('/')] switch
            {
                "building" => database.TryGetBuilding(id, out _) || BuildingTypes.TryGetById(id, out _),
                "unit" => database.TryGetUnit(id, out _),
                "object" => MapObjectTypes.TryGetById(id, out _) && id != "pine_tree",
                "tree" => TreeShapes.Contains(id),
                "ore" => OreKinds.Contains(id),
                "crate" => id == "supply",
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

    [Fact]
    public void A_building_names_its_files_in_its_states()
    {
        var house = ModelTable.Read("""
            format: models/1
            models:
              building/neutral_house_a: { file: map/house.glb, height: 2.5, damaged: map/house_damaged.glb, occupied: map/house_occupied.glb, ruins: map/house_ruins.glb }
            """)["building/neutral_house_a"];
        Assert.Equal(("map/house.glb", "map/house_damaged.glb", "map/house_occupied.glb", "map/house_ruins.glb"),
            (house.File, house.Damaged, house.Occupied, house.Ruins));

        var shed = ModelTable.Read("format: models/1\nmodels:\n  building/neutral_shed: { file: map/shed.glb }\n")["building/neutral_shed"];
        Assert.Null(shed.Damaged);
        Assert.Null(shed.Occupied);
        Assert.Null(shed.Ruins);
    }

    [Fact]
    public void Several_kinds_of_a_model_go_by_the_cell_evenly_and_neighbours_differ()
    {
        var cacti = ModelTable.Read("""
            format: models/1
            models:
              tree/cactus: { files: [d/c1.glb, d/c2.glb, d/c3.glb, d/c4.glb, d/c5.glb], scale: 1.2, jitter: 0.15, spin: true }
            """)["tree/cactus"];
        Assert.Equal(["d/c1.glb", "d/c2.glb", "d/c3.glb", "d/c4.glb", "d/c5.glb"], cacti.Files.ToArray());
        Assert.Equal("d/c1.glb", cacti.File);

        var counts = new int[5];
        var sameAsLeft = 0;
        for (var y = 0; y < 40; y++)
        {
            for (var x = 0; x < 40; x++)
            {
                var pick = cacti.Pick(x, y, 5);
                // The same cell, the same cactus, every time.
                Assert.Equal(pick, cacti.Pick(x, y, 5));
                counts[pick.Index]++;
                sameAsLeft += x > 0 && cacti.Pick(x - 1, y, 5).Index == pick.Index ? 1 : 0;
                Assert.InRange(pick.Scale, 1.2f * 0.85f - 0.001f, 1.2f * 1.15f + 0.001f);
                Assert.InRange(pick.Yaw, 0f, 360f);
            }
        }

        // 1 600 cells: each kind about a fifth, a neighbour the same about a fifth of the time.
        Assert.All(counts, count => Assert.InRange(count, 260, 380));
        Assert.InRange(sameAsLeft, 200, 420);
    }

    [Fact]
    public void One_file_without_jitter_or_spin_stands_as_the_table_says()
    {
        var crate = ModelTable.Read("format: models/1\nmodels:\n  object/boulder: { file: m/b.glb, scale: 0.8, yaw: 30 }\n")["object/boulder"];
        Assert.Equal(new ModelPick(0, 0.8f, 30f), crate.Pick(17, -4, 1));
        Assert.Equal(new ModelPick(0, 0.8f, 30f), crate.Pick(3, 9, 1));
    }

    [Theory]
    [InlineData("format: models/1\nmodels:\n  tree/palm: { file: a/b.glb, files: [a/c.glb] }\n", "one of the two")]
    [InlineData("format: models/1\nmodels:\n  tree/palm: { files: [] }\n", "files must be a list")]
    [InlineData("format: models/1\nmodels:\n  tree/palm: { files: [a/b.glb, a/c.obj] }\n", "is not a .glb path")]
    [InlineData("format: models/1\nmodels:\n  tree/palm: { file: a/b.glb, jitter: 0.6 }\n", "jitter must be")]
    [InlineData("format: models/1\nmodels:\n  tree/palm: { file: a/b.glb, spin: yes }\n", "spin must be")]
    [InlineData("format: models/2\nmodels:\n", "the format")]
    [InlineData("format: models/1\nmodels:\n  tank/heavy: { file: a/b.glb }\n", "is not a key")]
    [InlineData("format: models/1\nmodels:\n  unit/heavy_tank: { file: a/b.fbx }\n", "is not a .glb path")]
    [InlineData("format: models/1\nmodels:\n  unit/heavy_tank: { file: ../b.glb }\n", "is not a .glb path")]
    [InlineData("format: models/1\nmodels:\n  building/neutral_house_a: { file: a/b.glb, damaged: a/b.fbx }\n", "is not a .glb path")]
    [InlineData("format: models/1\nmodels:\n  building/neutral_house_a: { file: a/b.glb, ruins: a/b.obj }\n", "is not a .glb path")]
    [InlineData("format: models/1\nmodels:\n  unit/heavy_tank: { file: a/b.glb, size: 2 }\n", "unknown key 'size'")]
    [InlineData("format: models/1\nmodels:\n  unit/heavy_tank: { file: a/b.glb, scale: 0 }\n", "scale must be")]
    [InlineData("format: models/1\nmodels:\n  unit/heavy_tank: { scale: 2 }\n", "one of the two")]
    [InlineData("format: models/1\nmodels:\n  unit/heavy_tank: { file: a/b.glb, offset: [1, 2] }\n", "offset must be")]
    public void The_reader_refuses_what_it_does_not_know(string text, string message)
    {
        var error = Assert.Throws<ModelTableException>(() => ModelTable.Read(text.Replace("\\n", "\n")));
        Assert.Contains(message, error.Message);
    }
}
