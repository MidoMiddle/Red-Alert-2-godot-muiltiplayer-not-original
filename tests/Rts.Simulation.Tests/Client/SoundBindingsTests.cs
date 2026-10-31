using Rts.Client.Audio;
using Rts.Simulation.Map;
using YamlDotNet.RepresentationModel;

namespace Rts.Simulation.Tests.Client;

/// <summary>
/// <c>game/data/audio/bindings.yaml</c>: which sound plays for each weapon, building, bridge and flier. Every id it names is in the rules and
/// every sound it names is in <c>sounds.yaml</c>, so a typo does not leave a weapon silent; a mistake in a line leaves out only that line.
/// </summary>
public sealed class SoundBindingsTests
{
    private const string BindingsFile = "data/audio/bindings.yaml";

    [Fact]
    public void The_shipped_bindings_are_valid()
    {
        var errors = new List<string>();

        var bindings = SoundBindings.Parse(Shipped("bindings.yaml"), BindingsFile, errors);

        Assert.Empty(errors);
        Assert.NotEmpty(bindings.Weapons);
        Assert.NotEmpty(bindings.BuildingDefault);
    }

    [Fact]
    public void Every_sound_of_the_bindings_is_defined()
    {
        var sounds = SoundLibrary.Parse(Shipped("sounds.yaml"), "data/audio/sounds.yaml", []);
        var bindings = SoundBindings.Parse(Shipped("bindings.yaml"), BindingsFile, []);

        var named = bindings.Weapons.Values.Concat(bindings.Buildings.Values).Append(bindings.BuildingDefault)
            .Concat(bindings.Bridges.Values.SelectMany(bridge => new[] { bridge.Span, bridge.Whole })).Concat(bindings.Movement.Values)
            .Append(SoundBindings.ShotDefault);
        Assert.All(named, sound => Assert.Contains(sound, sounds.Keys));
    }

    [Fact]
    public void Every_id_of_the_bindings_is_in_the_rules()
    {
        var bindings = SoundBindings.Parse(Shipped("bindings.yaml"), BindingsFile, []);
        var kinds = KindsOfTheData();

        Assert.All(bindings.Weapons.Keys, id => Assert.True(kinds.TryGetValue(id, out var kind) && kind == "weapon", id));
        Assert.All(bindings.Buildings.Keys, id => Assert.True(kinds.TryGetValue(id, out var kind) && kind is "building" or "map_building", id));
        Assert.All(bindings.Bridges.Keys, id => Assert.True(BridgeTypes.TryGetKind(id, out _), id));
        Assert.All(bindings.Movement.Keys, id => Assert.True(kinds.TryGetValue(id, out var kind) && kind == "unit", id));
        Assert.NotEmpty(bindings.Movement);
    }

    [Fact]
    public void A_weapon_or_a_building_not_named_gets_the_default()
    {
        var bindings = SoundBindings.Parse(
            """
            weapons:
              some_rifle: snd_rifle
            buildings:
              default: snd_building_concrete
              some_house: snd_building_brick
            """,
            "test.yaml",
            []);

        Assert.Equal("snd_rifle", bindings.ShotOf("some_rifle"));
        Assert.Equal(SoundBindings.ShotDefault, bindings.ShotOf("other_gun"));
        Assert.Equal("snd_building_brick", bindings.FallOf("some_house"));
        Assert.Equal("snd_building_concrete", bindings.FallOf("other_building"));
        Assert.DoesNotContain(SoundBindings.DefaultKey, bindings.Buildings.Keys);
    }

    [Fact]
    public void A_wrong_line_is_reported_and_only_it_is_left_out()
    {
        var errors = new List<string>();

        var bindings = SoundBindings.Parse(
            """
            weapons:
              good_gun: snd_shot
              bad_gun: [snd_shot]
            bridges:
              good_bridge: {span: snd_a, whole: snd_b}
              bad_bridge: {span: snd_a}
            music: {}
            """,
            "test.yaml",
            errors);

        Assert.Contains("good_gun", bindings.Weapons.Keys);
        Assert.DoesNotContain("bad_gun", bindings.Weapons.Keys);
        Assert.Contains("good_bridge", bindings.Bridges.Keys);
        Assert.DoesNotContain("bad_bridge", bindings.Bridges.Keys);
        Assert.Contains(errors, error => error.Contains("weapons.bad_gun", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("bridges.bad_bridge", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("music is not a section", StringComparison.Ordinal));
    }

    /// <summary>The <c>kind</c> of every definition in the YAML files of <c>game/data</c>, by its id.</summary>
    private static Dictionary<string, string> KindsOfTheData()
    {
        var kinds = new Dictionary<string, string>();
        foreach (var path in Directory.EnumerateFiles(Path.Combine(ClientFiles.RepoRoot, "game", "data"), "*.yaml", SearchOption.AllDirectories))
        {
            var stream = new YamlStream();
            stream.Load(new StringReader(File.ReadAllText(path)));
            if (stream.Documents.Count == 0 || stream.Documents[0].RootNode is not YamlMappingNode root)
            {
                continue;
            }

            foreach (var pair in root.Children)
            {
                if (pair.Key is YamlScalarNode { Value: { } id } && pair.Value is YamlMappingNode fields
                    && fields.Children.TryGetValue(new YamlScalarNode("kind"), out var kind) && kind is YamlScalarNode { Value: { } text })
                {
                    kinds[id] = text;
                }
            }
        }

        return kinds;
    }

    private static string Shipped(string name) => File.ReadAllText(Path.Combine(ClientFiles.RepoRoot, "game", "data", "audio", name));
}
