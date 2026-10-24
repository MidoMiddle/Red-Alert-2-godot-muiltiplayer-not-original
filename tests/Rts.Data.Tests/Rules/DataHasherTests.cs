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

        foreach (var definition in rules.Armors)
        {
            builder.Add((ArmorDef)change(definition));
        }

        foreach (var definition in rules.Warheads)
        {
            builder.Add((WarheadDef)change(definition));
        }

        foreach (var definition in rules.Projectiles)
        {
            builder.Add((ProjectileDef)change(definition));
        }

        foreach (var definition in rules.BuildOrders)
        {
            builder.Add((BuildOrderDef)change(definition));
        }

        foreach (var definition in rules.Armies)
        {
            builder.Add((ArmyDef)change(definition));
        }

        foreach (var definition in rules.Difficulties)
        {
            builder.Add((DifficultyDef)change(definition));
        }

        foreach (var definition in rules.Personalities)
        {
            builder.Add((PersonalityDef)change(definition));
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
        ("unit threat", Unit(u => u with { Threat = u.Threat + 1 })),
        ("unit stance", Unit(u => u with { Stance = Rts.Simulation.World.Stance.HoldPosition })),
        ("unit crusher", Unit(u => u with { Crusher = !u.Crusher })),
        ("unit crushable", Unit(u => u with { Crushable = !u.Crushable })),
        ("unit armor", Unit(u => u with { Armor = "light" })),
        ("unit speed", Unit(u => u with { Speed = u.Speed + 1 })),
        ("unit turn rate", Unit(u => u with { TurnRate = u.TurnRate + 1 })),
        ("unit sight", Unit(u => u with { Sight = u.Sight + 1 })),
        ("unit locomotor", Unit(u => u with { Locomotor = "wheel" })),
        ("unit placement", Unit(u => u with { Placement = UnitPlacement.Subcell })),
        ("unit turret", Unit(u => u with { Turret = false })),
        ("unit prerequisites", Unit(u => u with { Prerequisites = ["war_factory", "reactor"] })),
        ("unit primary", Unit(u => u with { Primary = "" })),
        ("unit secondary", Unit(u => u with { Secondary = "heavy_tank_cannon" })),
        ("unit turret turn rate", Unit(u => u with { TurretTurnRate = 1024 })),
        ("building armor", Building(b => b with { Armor = "heavy" })),
        ("building primary", Building(b => b with { Primary = "heavy_tank_cannon" })),
        ("weapon min range", d => d is WeaponDef w ? w with { MinRange = Fixed.One } : d),
        ("weapon projectile", d => d is WeaponDef w ? w with { Projectile = "shell2" } : d),
        ("weapon warhead", d => d is WeaponDef w ? w with { Warhead = "ap_shell2" } : d),
        ("weapon burst", d => d is WeaponDef w ? w with { Burst = 2 } : d),
        ("weapon burst delay", d => d is WeaponDef w ? w with { BurstDelay = 3 } : d),
        ("weapon targets", d => d is WeaponDef w ? w with { Targets = WeaponTargets.Ground | WeaponTargets.Air } : d),
        ("armor id", d => d is ArmorDef { Id: "steel" } a ? a with { Id = "steel2" } : d),
        ("warhead verse", d => d is WarheadDef w ? w with { Verses = w.Verses.SetItem(0, w.Verses[0] with { Percent = w.Verses[0].Percent + Fixed.Epsilon }) } : d),
        ("warhead spread", d => d is WarheadDef w ? w with { Spread = Fixed.One } : d),
        ("warhead percent at max", d => d is WarheadDef w ? w with { PercentAtMax = Fixed.One } : d),
        ("warhead wall damage", d => d is WarheadDef w ? w with { WallDamage = false } : d),
        ("warhead affects air", d => d is WarheadDef w ? w with { AffectsAir = true } : d),
        ("projectile motion", d => d is ProjectileDef p ? p with { Motion = ProjectileMotion.Homing } : d),
        ("projectile speed", d => d is ProjectileDef p ? p with { Speed = 41 } : d),
        ("projectile arc", d => d is ProjectileDef p ? p with { Arc = Fixed.One } : d),
        ("projectile inaccuracy", d => d is ProjectileDef p ? p with { Inaccuracy = Fixed.One } : d),
        ("projectile turn rate", d => d is ProjectileDef p ? p with { TurnRate = 1 } : d),
        ("projectile lifetime", d => d is ProjectileDef p ? p with { Lifetime = 1 } : d),
        ("building faction", Building(b => b with { Faction = "union2" })),
        ("building category", Building(b => b with { Category = ProductionCategory.Defense })),
        ("building cost", Building(b => b with { Cost = b.Cost + 1 })),
        ("building build time", Building(b => b with { BuildTime = b.BuildTime + 1 })),
        ("building hp", Building(b => b with { Hp = b.Hp + 1 })),
        ("building threat", Building(b => b with { Threat = b.Threat + 1 })),
        ("building width", Building(b => b with { Footprint = b.Footprint with { Width = b.Footprint.Width + 1 } })),
        ("building height", Building(b => b with { Footprint = b.Footprint with { Height = b.Footprint.Height + 1 } })),
        ("building power", Building(b => b with { Power = b.Power + 1 })),
        ("building build-up", Building(b => b with { BuildUp = b.BuildUp + 1 })),
        ("building adjacent", Building(b => b with { Adjacent = b.Adjacent + 1 })),
        ("building extends base", Building(b => b with { ExtendsBase = false })),
        ("building wall", Building(b => b with { Wall = !b.Wall })),
        ("building powered", Building(b => b with { Powered = !b.Powered })),
        ("building sight", Building(b => b with { Sight = b.Sight + 1 })),
        ("building radar", Building(b => b with { Radar = !b.Radar })),
        ("building produces", Building(b => b with { Produces = [ProductionCategory.Infantry] })),
        ("building dock", Building(b => b with { DockCell = new CellPos(1, 4) })),
        ("unit harvest capacity", Unit(u => u with { HarvestCapacity = 1 })),
        ("unit harvest time", Unit(u => u with { HarvestTime = 1 })),
        ("unit unload rate", Unit(u => u with { UnloadRate = 1 })),
        ("building exit x", Building(b => b with { ExitCell = new CellPos(2, 3) })),
        ("building exit y", Building(b => b with { ExitCell = new CellPos(1, 4) })),
        ("building no exit", Building(b => b with { ExitCell = null })),
        ("building prerequisites", Building(b => b with { Prerequisites = [] })),
        ("faction mcv", d => d is FactionDef f ? f with { Mcv = "heavy_tank" } : d),
        ("faction construction yard", d => d is FactionDef f ? f with { ConstructionYard = "reactor" } : d),
        ("faction refinery", d => d is FactionDef f ? f with { Refinery = "reactor" } : d),
        ("faction harvester", d => d is FactionDef f ? f with { Harvester = "mcv" } : d),
        ("build order steps in another order", d => d is BuildOrderDef o ? o with { Steps = [.. o.Steps.Reverse()] } : d),
        ("build order a step fewer", d => d is BuildOrderDef o ? o with { Steps = o.Steps.RemoveAt(0) } : d),
        ("army share", d => d is ArmyDef a ? a with { Shares = [a.Shares[0] with { Share = a.Shares[0].Share + Fixed.FromRatio(1, 100) }, .. a.Shares[1..]] } : d),
        ("army against infantry", d => d is ArmyDef a ? a with { AgainstInfantry = [] } : d),
        ("army against vehicles", d => d is ArmyDef a ? a with { AgainstVehicles = [] } : d),
        ("difficulty value", d => d is DifficultyDef l ? l with { Values = l.Values.SetItem("min_attack_army", l.Values["min_attack_army"] + 1) } : d),
        ("difficulty bonus", d => d is DifficultyDef l ? l with { Values = l.Values.SetItem("income_bonus", l.Values["income_bonus"] + 1) } : d),
        ("difficulty choice", d => d is DifficultyDef l ? l with { Values = l.Values.SetItem("micro", l.Values["micro"] + 1) } : d),
        ("personality weight", d => d is PersonalityDef p ? p with { Weights = p.Weights with { Harassment = p.Weights.Harassment + Fixed.Epsilon } } : d),
        ("personality army", d => d is PersonalityDef p ? p with { Armies = [p.Armies[0] with { Army = "other" }] } : d),
        ("personality build order", d => d is PersonalityDef p ? p with { BuildOrders = [p.BuildOrders[0] with { Optimized = "other" }] } : d),
        ("personality first attack", d => d is PersonalityDef p ? p with { FirstAttackMax = p.FirstAttackMax + 1 } : d),
        ("personality waves", d => d is PersonalityDef p ? p with { WaveInterval = p.WaveInterval + 1 } : d),
        ("personality retreat", d => d is PersonalityDef p ? p with { RetreatThreshold = p.RetreatThreshold + Fixed.Epsilon } : d),
        ("personality targets", d => d is PersonalityDef p ? p with { TargetPriority = [.. p.TargetPriority.Reverse()] } : d),
    ];

    [Fact]
    public void The_hash_of_the_dataset_is_pinned()
    {
        // Changes only when the dataset or the encoding of the hash changes; then the replays of earlier data stop matching it.
        // M3 step 6: the adjacent and extends_base of every building joined the encoding; step 8: produces and the economy records;
        // step 10: the harvest keys of units and the dock of buildings; step 12: the radar of buildings; M4 step 1: armor, warheads, projectiles, the weapons in full, and who carries which; step 3: the turn rate of turrets; step 6: the threat of units and buildings; step 7: the stance of units; step 8: crusher and crushable; step 10: wall and powered; step 11: the sight of buildings. M5 step 5: the build orders of the bots (the dataset has one now); step 6: their armies (and one in the dataset); step 14: the levels of the bots (and «Medium» in the dataset). M8 step 1: the build order and the army leave the level for the personality (one in the dataset), the levels gain the keys of M8; step 2: the bonuses of the player in every level; step 7: no counter_shift; step 10: the harvesters of a level, fixed or by the ore; step 10a: what a level must know before it attacks. The Alliance: the starting infantry of a faction and which one is the default. Every unit: whether it is available later. Every building: whether it stands on the water; stage 2c: whether it repairs vehicles, its discount, its bonus of the ore, its death weapon; stage 2d: whether it reveals the map, its veil; stage 2e: its garrison, its chain bonus and chain; stage 3a: the jet pack of a unit, the temporal warhead; stage 3b: the talents of units, their death and deploy weapons, the suicide of a weapon, the bombs and toxin of a warhead, what can be taken and infiltrated; stage 3c: the hit points and armor of a shot, the splinters of a weapon, the disguise of a still unit; stage 3d: passengers, sizes, the IFV's weapons, robots and their control, the parasite of a warhead; stage 3e: under the water, sensors, a parasite of ships that holds them still; stage 3f: the ammo of a unit, a building that rearms aircraft; then self-repair, the units a carrier launches, the pads, the repair and the freeing of a warhead; stage 2d: the superweapon of a building, its charge, its weapon, the build limit. Loose ends, stage 1: the paradrop of a faction; stage 3: whether a unit trains; stage 5: the countries, what only they have or may not, the paradrop of a building. The weapon of a unit deployed (the Rifleman's sandbags).
        Assert.Equal(0xaf98fb7dd731596eUL, Hash(Files()));
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
            WeaponDef weapon => weapon with { Name = "Other", Report = "other" },
            ArmorDef armor => armor with { Name = "Other" },
            WarheadDef warhead => warhead with { Name = "Other", InfantryDeath = "other" },
            ProjectileDef projectile => projectile with { Name = "Other", Visual = "other" },
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
