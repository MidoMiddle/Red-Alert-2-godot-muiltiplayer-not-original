using Rts.Simulation.Geometry;
using Rts.Simulation.Math;
using Rts.Simulation.Rules;
using Rts.Simulation.World;

namespace Rts.Data.Tests.Rules;

/// <summary>
/// The hash of the data (docs/implementation/M3_base_economy.md, 4.4, 6): the way the files are written does not change it, any value a
/// match uses does, and the names, models and icons do not.
/// </summary>
public sealed class DataHasherTests
{
    private static List<(string FileName, string Text)> Files() =>
        Directory.GetFiles(RulesDataset.Folder, "*.yaml", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(RulesDataset.Folder, path).Replace('\\', '/'))
            .OrderBy(name => name, StringComparer.Ordinal)
            .Select(name => (name, File.ReadAllText(Path.Combine(RulesDataset.Folder, name)).Replace("\r\n", "\n")))
            .ToList();

    private static ulong Hash(IEnumerable<(string FileName, string Text)> files) => DataHasher.Compute(RulesLoader.Load(files));

    /// <summary>The rules with every definition passed through <paramref name="change"/>.</summary>
    private static RulesDatabase Changed(RulesDatabase rules, Func<IDefinition, IDefinition> change)
    {
        var builder = new RulesBuilder();
        foreach (var definition in rules.Locomotors)
        {
            builder.Add((LocomotorDef)change(definition));
        }

        foreach (var definition in rules.Terrains)
        {
            builder.Add((TerrainDef)change(definition));
        }

        foreach (var definition in rules.Weapons)
        {
            builder.Add((WeaponDef)change(definition));
        }

        foreach (var definition in rules.Units)
        {
            builder.Add((UnitDef)change(definition));
        }

        foreach (var definition in rules.Buildings)
        {
            builder.Add((BuildingDef)change(definition));
        }

        foreach (var definition in rules.Factions)
        {
            builder.Add((FactionDef)change(definition));
        }

        return builder.Build();
    }

    private static Func<IDefinition, IDefinition> Unit(Func<UnitDef, UnitDef> change) =>
        definition => definition is UnitDef { Id: "heavy_tank" } unit ? change(unit) : definition;

    private static Func<IDefinition, IDefinition> Building(Func<BuildingDef, BuildingDef> change) =>
        definition => definition is BuildingDef { Id: "war_factory" } building ? change(building) : definition;

    /// <summary>One change of every value that a match uses, by name.</summary>
    private static readonly (string Name, Func<IDefinition, IDefinition> Change)[] Changes =
    [
        ("locomotor slope", d => d is LocomotorDef { Id: "track" } l ? l with { Slope = l.Slope + Fixed.Epsilon } : d),
        ("terrain code", d => d is TerrainDef { Id: "road" } t ? t with { Code = '+' } : d),
        ("terrain buildable", d => d is TerrainDef { Id: "road" } t ? t with { Buildable = false } : d),
        ("terrain speed", d => d is TerrainDef { Id: "road" } t
            ? t with { Speed = t.Speed.SetItem(2, t.Speed[2] with { Multiplier = t.Speed[2].Multiplier + Fixed.Epsilon }) }
            : d),
        ("weapon damage", d => d is WeaponDef w ? w with { Damage = w.Damage + 1 } : d),
        ("weapon range", d => d is WeaponDef w ? w with { Range = w.Range + Fixed.Epsilon } : d),
        ("weapon reload", d => d is WeaponDef w ? w with { Reload = w.Reload + 1 } : d),
        ("unit faction", Unit(u => u with { Faction = "union2" })),
        ("unit category", Unit(u => u with { Category = UnitCategory.Infantry })),
        ("unit cost", Unit(u => u with { Cost = u.Cost + 1 })),
        ("unit build time", Unit(u => u with { BuildTime = u.BuildTime + 1 })),
        ("unit hp", Unit(u => u with { Hp = u.Hp + 1 })),
        ("unit armor", Unit(u => u with { Armor = "light" })),
        ("unit speed", Unit(u => u with { Speed = u.Speed + 1 })),
        ("unit turn rate", Unit(u => u with { TurnRate = u.TurnRate + 1 })),
        ("unit sight", Unit(u => u with { Sight = u.Sight + 1 })),
        ("unit locomotor", Unit(u => u with { Locomotor = "wheel" })),
        ("unit placement", Unit(u => u with { Placement = UnitPlacement.Subcell })),
        ("unit turret", Unit(u => u with { Turret = false })),
        ("unit prerequisites", Unit(u => u with { Prerequisites = ["war_factory", "reactor"] })),
        ("unit weapons", Unit(u => u with { Weapons = [] })),
        ("building faction", Building(b => b with { Faction = "union2" })),
        ("building category", Building(b => b with { Category = ProductionCategory.Defense })),
        ("building cost", Building(b => b with { Cost = b.Cost + 1 })),
        ("building build time", Building(b => b with { BuildTime = b.BuildTime + 1 })),
        ("building hp", Building(b => b with { Hp = b.Hp + 1 })),
        ("building width", Building(b => b with { Footprint = b.Footprint with { Width = b.Footprint.Width + 1 } })),
        ("building height", Building(b => b with { Footprint = b.Footprint with { Height = b.Footprint.Height + 1 } })),
        ("building power", Building(b => b with { Power = b.Power + 1 })),
        ("building build-up", Building(b => b with { BuildUp = b.BuildUp + 1 })),
        ("building adjacent", Building(b => b with { Adjacent = b.Adjacent + 1 })),
        ("building extends base", Building(b => b with { ExtendsBase = false })),
        ("building produces", Building(b => b with { Produces = [ProductionCategory.Infantry] })),
        ("building exit x", Building(b => b with { ExitCell = new CellPos(2, 3) })),
        ("building exit y", Building(b => b with { ExitCell = new CellPos(1, 4) })),
        ("building no exit", Building(b => b with { ExitCell = null })),
        ("building prerequisites", Building(b => b with { Prerequisites = [] })),
        ("faction mcv", d => d is FactionDef f ? f with { Mcv = "heavy_tank" } : d),
        ("faction construction yard", d => d is FactionDef f ? f with { ConstructionYard = "reactor" } : d),
        ("faction refinery", d => d is FactionDef f ? f with { Refinery = "reactor" } : d),
        ("faction harvester", d => d is FactionDef f ? f with { Harvester = "mcv" } : d),
    ];

    [Fact]
    public void The_hash_of_the_dataset_is_pinned()
    {
        // Changes only when the dataset or the encoding of the hash changes; then the replays of earlier data stop matching it.
        // M3 step 6: the adjacent and extends_base of every building joined the encoding; step 8: produces and the economy records.
        Assert.Equal(0xd4678379802e8592UL, Hash(Files()));
    }

    [Fact]
    public void The_order_of_files_and_entries_does_not_change_the_hash()
    {
        var files = Files();
        var reordered = files
            .Select(file => (file.FileName, string.Join("\n\n", Enumerable.Reverse(file.Text.TrimEnd('\n').Split("\n\n"))) + "\n"))
            .Reverse();

        Assert.Equal(Hash(files), Hash(reordered));
    }

    [Fact]
    public void Spaces_comments_and_quotes_do_not_change_the_hash()
    {
        var files = Files();
        var reformatted = files.Select(file => (
            file.FileName,
            "# A comment at the top.\n\n" + file.Text.Replace("  name: ", "  name:     ").Replace("cost: 2000", "cost: \"2000\"")
                .Replace("\n\n", "\n\n\n# Between entries.\n")));

        Assert.Equal(Hash(files), Hash(reformatted));
    }

    [Fact]
    public void Every_value_a_match_uses_changes_the_hash()
    {
        var rules = RulesLoader.Load(Files());
        var original = DataHasher.Compute(rules);
        var hashes = new List<ulong> { original };
        foreach (var (name, change) in Changes)
        {
            var hash = DataHasher.Compute(Changed(rules, change));
            Assert.False(hashes.Contains(hash), $"'{name}' gives a hash that is already taken");
            hashes.Add(hash);
        }
    }

    [Fact]
    public void A_changed_number_in_a_file_changes_the_hash()
    {
        var files = Files();
        var index = files.FindIndex(file => file.FileName.EndsWith("weapons.yaml", StringComparison.Ordinal));
        var edited = new List<(string FileName, string Text)>(files);
        edited[index] = (files[index].FileName, files[index].Text.Replace("range: 5.5", "range: 5.50001"));

        Assert.NotEqual(Hash(files), Hash(edited));
    }

    [Fact]
    public void Names_models_and_icons_do_not_change_the_hash()
    {
        var rules = RulesLoader.Load(Files());
        var renamed = Changed(rules, definition => definition switch
        {
            UnitDef unit => unit with { Name = "Other", Model = "other/model", Icon = "other" },
            BuildingDef building => building with { Name = "Other", Model = "other/model", Icon = "other" },
            WeaponDef weapon => weapon with { Name = "Other" },
            LocomotorDef locomotor => locomotor with { Name = "Other" },
            TerrainDef terrain => terrain with { Name = "Other" },
            FactionDef faction => faction with { Name = "Other" },
            _ => definition,
        });

        Assert.Equal(DataHasher.Compute(rules), DataHasher.Compute(renamed));
    }

    [Fact]
    public void Two_texts_that_run_into_each_other_hash_apart()
    {
        // "ab" + "c" and "a" + "bc" would hash alike without the lengths.
        var rules = RulesLoader.Load(Files());
        var first = Changed(rules, Unit(unit => unit with { Armor = "ab", Locomotor = "c" }));
        var second = Changed(rules, Unit(unit => unit with { Armor = "a", Locomotor = "bc" }));

        Assert.NotEqual(DataHasher.Compute(first), DataHasher.Compute(second));
    }
}
