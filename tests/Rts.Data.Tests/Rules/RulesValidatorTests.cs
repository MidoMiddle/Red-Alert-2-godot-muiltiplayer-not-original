namespace Rts.Data.Tests.Rules;

/// <summary>
/// The checks of the meaning of the data and the collection of every error (docs/implementation/M3_base_economy.md, 4.3, 6): each mistake
/// is caught at the file and line where it is written, and all of them come together.
/// </summary>
public sealed class RulesValidatorTests
{
    private const string Buildings = "factions/union/buildings.yaml";
    private const string Units = "factions/union/units.yaml";
    private const string Factions = "factions/factions.yaml";
    private const string Terrain = "shared/terrain.yaml";
    private const string Templates = "shared/templates.yaml";
    private const string Locomotors = "shared/locomotors.yaml";

    /// <summary>The files of the test dataset as (name, text), in the order <see cref="RulesLoader.LoadDirectory"/> reads them.</summary>
    private static List<(string FileName, string Text)> Dataset() =>
        Directory.GetFiles(RulesDataset.Folder, "*.yaml", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(RulesDataset.Folder, path).Replace('\\', '/'))
            .OrderBy(name => name, StringComparer.Ordinal)
            .Select(name => (name, File.ReadAllText(Path.Combine(RulesDataset.Folder, name)).Replace("\r\n", "\n")))
            .ToList();

    /// <summary>The dataset with one text replaced in one file; the text must be there exactly once.</summary>
    private static List<(string FileName, string Text)> Edited(
        List<(string FileName, string Text)> files, string fileName, string text, string replacement)
    {
        var index = files.FindIndex(file => file.FileName == fileName);
        var original = files[index].Text;
        Assert.Equal(1, original.Split(text).Length - 1);
        var edited = new List<(string FileName, string Text)>(files);
        edited[index] = (fileName, original.Replace(text, replacement));
        return edited;
    }

    private static IReadOnlyList<RulesError> Errors(List<(string FileName, string Text)> files) =>
        Assert.Throws<RulesException>(() => RulesLoader.Load(files)).Errors;

    /// <summary>The one error that a single edit of the dataset gives.</summary>
    private static RulesError Fails(string fileName, string text, string replacement) =>
        Assert.Single(Errors(Edited(Dataset(), fileName, text, replacement)));

    [Fact]
    public void The_dataset_passes_every_check()
    {
        var rules = RulesLoader.Load(Dataset());

        Assert.Equal(4, rules.Buildings.Length);
    }

    [Fact]
    public void An_unknown_key_is_reported_at_the_key()
    {
        var error = Fails(Units, "  sight: 8\n", "  sigth: 8\n");

        Assert.Equal((Units, 37, 3), (error.FileName, error.Line, error.Column));
        Assert.Contains("unknown key 'sigth' in 'heavy_tank'", error.Reason);
    }

    [Fact]
    public void A_prerequisite_that_is_not_defined_is_reported_at_its_id_in_the_list()
    {
        var error = Fails(Buildings, "prerequisites: [reactor]", "prerequisites: [reactor, reacter]");

        Assert.Equal((Buildings, 35, 28), (error.FileName, error.Line, error.Column));
        Assert.Equal("'refinery' refers to building 'reacter', which is not defined", error.Reason);
    }

    [Fact]
    public void A_prerequisite_of_another_kind_is_named_with_its_kind()
    {
        var error = Fails(Units, "prerequisites: [war_factory]", "prerequisites: [heavy_tank_cannon]");

        Assert.Equal((Units, 38, 19), (error.FileName, error.Line, error.Column));
        Assert.Equal("'heavy_tank' refers to building 'heavy_tank_cannon', but 'heavy_tank_cannon' is weapon", error.Reason);
    }

    [Fact]
    public void A_weapon_that_is_not_defined_is_reported()
    {
        var error = Fails(Units, "weapons: [heavy_tank_cannon]", "weapons: [heavy_tank_gun]");

        Assert.Equal((Units, 39, 13), (error.FileName, error.Line, error.Column));
        Assert.Contains("weapon 'heavy_tank_gun', which is not defined", error.Reason);
    }

    [Fact]
    public void A_faction_that_is_not_defined_is_reported()
    {
        var error = Fails(Units, "  name: Conscript\n  faction: union\n", "  name: Conscript\n  faction: soviet\n");

        Assert.Equal((Units, 4, 12), (error.FileName, error.Line, error.Column));
        Assert.Equal("'conscript' refers to faction 'soviet', which is not defined", error.Reason);
    }

    [Fact]
    public void A_locomotor_that_is_not_defined_is_reported()
    {
        var error = Fails(Units, "locomotor: foot", "locomotor: legs");

        Assert.Equal((Units, 7, 14), (error.FileName, error.Line, error.Column));
        Assert.Contains("locomotor 'legs', which is not defined", error.Reason);
    }

    [Fact]
    public void A_broken_reference_in_a_template_is_reported_once_in_the_template()
    {
        // Three units inherit the locomotor from the template; the error is where the value is written.
        var error = Fails(Templates, "locomotor: track", "locomotor: tracks");

        Assert.Equal((Templates, 7, 14), (error.FileName, error.Line, error.Column));
    }

    [Fact]
    public void A_speed_for_an_unknown_locomotor_is_reported_at_its_key()
    {
        var error = Fails(Terrain, "wheel: 120% }", "wheel: 120%, hover: 50% }");

        Assert.Equal((Terrain, 13, 50), (error.FileName, error.Line, error.Column));
        Assert.Equal("'road' refers to locomotor 'hover', which is not defined", error.Reason);
    }

    [Fact]
    public void A_land_type_needs_a_speed_for_every_locomotor()
    {
        var error = Fails(Terrain, "track: 100%, wheel: 120% }", "track: 100% }");

        Assert.Equal((Terrain, 13, 10), (error.FileName, error.Line, error.Column));
        Assert.Equal("'road' has no speed for locomotor 'wheel'", error.Reason);
    }

    [Fact]
    public void A_land_code_used_twice_is_reported_at_the_second_land_type()
    {
        var error = Fails(Terrain, "code: \"=\"", "code: \".\"");

        Assert.Equal((Terrain, 11, 9), (error.FileName, error.Line, error.Column));
        Assert.Equal("code '.' of 'road' is already used by 'clear'", error.Reason);
    }

    [Fact]
    public void An_inheritance_cycle_is_reported()
    {
        var files = Dataset();
        files.Add(("zz.yaml", "loop_a:\n  kind: weapon\n  inherits: loop_b\n  name: A\nloop_b:\n  kind: weapon\n  inherits: loop_a\n  name: B\n"));

        var error = Assert.Single(Errors(files));

        Assert.Equal(("zz.yaml", 7, 13), (error.FileName, error.Line, error.Column));
        Assert.Equal("inheritance cycle: loop_a -> loop_b -> loop_a", error.Reason);
    }

    [Fact]
    public void A_prerequisite_cycle_is_reported_once_with_the_cycle()
    {
        var error = Fails(Buildings, "  footprint: [3, 3]\n\nreactor:", "  footprint: [3, 3]\n  prerequisites: [refinery]\n\nreactor:");

        // Found from the construction yard, the first in id order; it closes at the reactor, one line lower after the edit.
        Assert.Equal((Buildings, 22, 19), (error.FileName, error.Line, error.Column));
        Assert.Equal("prerequisite cycle: construction_yard -> refinery -> reactor -> construction_yard", error.Reason);
    }

    [Fact]
    public void A_building_that_requires_itself_is_a_cycle()
    {
        var error = Fails(Buildings, "prerequisites: [refinery]", "prerequisites: [war_factory]");

        Assert.Equal("prerequisite cycle: war_factory -> war_factory", error.Reason);
    }

    [Fact]
    public void A_faction_without_its_harvester_is_reported()
    {
        var error = Fails(Factions, "harvester: harvester", "harvester: harvestr");

        Assert.Equal((Factions, 7, 14), (error.FileName, error.Line, error.Column));
        Assert.Equal("'union' refers to unit 'harvestr', which is not defined", error.Reason);
    }

    [Fact]
    public void A_faction_whose_refinery_is_a_unit_is_reported()
    {
        var error = Fails(Factions, "refinery: refinery", "refinery: harvester");

        Assert.Equal((Factions, 6, 13), (error.FileName, error.Line, error.Column));
        Assert.Equal("'union' refers to building 'harvester', but 'harvester' is unit", error.Reason);
    }

    [Fact]
    public void A_faction_with_the_construction_yard_of_another_faction_is_reported()
    {
        var files = Dataset();
        files.Add((
            "zz.yaml",
            "soviet:\n  kind: faction\n  name: Soviet\n  mcv: mcv\n  construction_yard: construction_yard\n  refinery: refinery\n" +
            "  harvester: harvester\n"));

        var errors = Errors(files);

        Assert.Equal(
            [
                "'soviet' refers to unit 'mcv', but it belongs to 'union'",
                "'soviet' refers to building 'construction_yard', but it belongs to 'union'",
                "'soviet' refers to building 'refinery', but it belongs to 'union'",
                "'soviet' refers to unit 'harvester', but it belongs to 'union'",
            ],
            errors.Select(error => error.Reason));
    }

    [Theory]
    [InlineData(Buildings, "cost: 600", "cost: 0", "'cost' of 'reactor' must be above zero")]
    [InlineData(Units, "hp: 150", "hp: -1", "'hp' of 'conscript' must be above zero")]
    [InlineData(Units, "speed: 40", "speed: -40", "'speed' of 'heavy_tank' must not be negative")]
    [InlineData(Buildings, "footprint: [2, 2]", "footprint: [7, 1]", "the footprint of 'reactor' must be 1 to 6 cells a side, not [7, 1]")]
    [InlineData(Buildings, "footprint: [2, 2]", "footprint: [0, 2]", "the footprint of 'reactor' must be 1 to 6 cells a side, not [0, 2]")]
    [InlineData(Buildings, "exit_cell: [1, 3]", "exit_cell: [1, 6]", "the exit cell [1, 6] of 'war_factory' must be in its footprint or next to it")]
    [InlineData(Locomotors, "slope: 65%", "slope: 120%", "'slope' of 'wheel' must be 0% to 100%")]
    [InlineData(Buildings, "  power: 150\n", "  power: 150\n  adjacent: 11\n", "'adjacent' of 'reactor' must be 0 to 10 cells")]
    [InlineData("factions/union/weapons.yaml", "damage: 120", "damage: -5", "'damage' of 'heavy_tank_cannon' must not be negative")]
    public void A_number_out_of_its_range_is_reported_at_the_number(string fileName, string text, string replacement, string reason)
    {
        var error = Fails(fileName, text, replacement);

        Assert.Equal(fileName, error.FileName);
        Assert.Equal(reason, error.Reason);
    }

    [Fact]
    public void Mistakes_in_several_files_come_together_in_file_and_line_order()
    {
        var files = Dataset();
        files = Edited(files, Terrain, "wheel: 120% }", "wheel: 120%, hover: 50% }");
        files = Edited(files, Buildings, "prerequisites: [reactor]", "prerequisites: [reacter]");
        files = Edited(files, Buildings, "cost: 600", "cost: 0");
        files = Edited(files, Factions, "harvester: harvester", "harvester: harvestr");

        var errors = Assert.Throws<RulesException>(() => RulesLoader.Load(files));

        Assert.Equal(
            [(Factions, 7), (Buildings, 16), (Buildings, 35), (Terrain, 13)],
            errors.Errors.Select(error => (error.FileName, error.Line)));
        Assert.Equal(4, errors.Message.Split('\n').Length);
        Assert.StartsWith($"{Factions}:7:14: ", errors.Message);
    }

    [Fact]
    public void Reading_goes_on_after_a_broken_file_and_a_broken_entry()
    {
        var files = Dataset();
        files.Add(("a.yaml", "broken: [\n"));
        files.Add(("b.yaml", "first:\n  kind: weapon\n  name: First\n  colour: red\nsecond:\n  kind: weapon\n  damage: 5\n"));

        var errors = Errors(files);

        Assert.Equal(3, errors.Count);
        Assert.Equal("a.yaml", errors[0].FileName);
        Assert.Contains("not valid YAML", errors[0].Reason);
        Assert.Equal(("b.yaml", 4, "unknown key 'colour' in 'first'"), (errors[1].FileName, errors[1].Line, errors[1].Reason));
        Assert.Equal(("b.yaml", 5, "'second' has no 'name'"), (errors[2].FileName, errors[2].Line, errors[2].Reason));
    }

    [Fact]
    public void The_meaning_is_not_checked_while_some_entry_cannot_be_read()
    {
        // The broken reactor is left out; the refinery's prerequisite would be a second, misleading error.
        var error = Fails(Buildings, "  power: 150\n", "  power: lots\n");

        Assert.Contains("'lots' must be a whole number", error.Reason);
    }
}
