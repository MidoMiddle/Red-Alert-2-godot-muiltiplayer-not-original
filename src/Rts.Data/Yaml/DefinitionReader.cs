using System.Collections.Immutable;
using Rts.Simulation.Math;
using Rts.Simulation.Rules;
using Rts.Simulation.World;
using YamlDotNet.RepresentationModel;

namespace Rts.Data.Yaml;

/// <summary>
/// Reads a resolved entry as a definition of its kind (docs/implementation/M3_base_economy.md, 4.1, 4.2). The <c>kind</c> names the schema:
/// the keys it allows and the fields it requires. For a template (<c>complete</c> false) the result is a placeholder that the loader does
/// not keep, but every field that is written is still checked.
/// </summary>
internal static class DefinitionReader
{
    private static readonly string[] Kinds = ["unit", "building", "weapon", "locomotor", "terrain", "faction", "economy"];

    private static readonly string[] UnitKeys =
    [
        "name", "faction", "category", "cost", "build_time", "hp", "armor", "speed", "turn_rate", "sight", "locomotor",
        "placement", "turret", "prerequisites", "weapons", "model", "icon", "harvest_capacity", "harvest_time", "unload_rate",
    ];

    private static readonly string[] BuildingKeys =
    [
        "name", "faction", "category", "cost", "build_time", "hp", "footprint", "power", "build_up", "exit_cell", "adjacent", "extends_base", "produces", "dock_cell",
        "prerequisites", "model", "icon",
    ];

    private static readonly string[] WeaponKeys = ["name", "damage", "range", "reload"];

    private static readonly string[] LocomotorKeys = ["name", "slope"];

    private static readonly string[] TerrainKeys = ["name", "code", "buildable", "speed"];

    private static readonly string[] FactionKeys = ["name", "mcv", "construction_yard", "refinery", "harvester"];

    private static readonly string[] EconomyKeys =
    [
        "name", "low_power_min_rate", "multiple_factory_bonus", "max_factory_rate", "ore_value", "gem_value", "ore_growth_interval",
        "ore_growth_chance", "ore_spread_stage", "ore_spread_chance",
    ];

    /// <summary>The kind of an entry, one of the six known kinds.</summary>
    public static string KindOf(RawDefinition definition)
    {
        var field = definition.Fields["kind"];
        return Scalars.Choice(field.FileName, field.Value, Kinds);
    }

    /// <summary>Whether the entry is a template: <c>abstract: true</c>. A template creates no definition.</summary>
    public static bool IsTemplate(RawDefinition definition) =>
        definition.Fields.TryGetValue("abstract", out var field) && Scalars.Flag(field.FileName, field.Value);

    public static UnitDef Unit(RawDefinition definition, bool complete)
    {
        var fields = new FieldReader(definition, complete, UnitKeys);
        return new UnitDef
        {
            Id = definition.Id,
            Name = fields.Required("name", Scalars.Text, ""),
            Faction = fields.Required("faction", Scalars.Id, ""),
            Category = fields.Required("category", UnitCategoryOf, UnitCategory.Infantry),
            Cost = fields.Required("cost", Scalars.Int, 0),
            BuildTime = fields.Required("build_time", Scalars.Ticks, 0),
            Hp = fields.Required("hp", Scalars.Int, 0),
            Armor = fields.Optional("armor", Scalars.Id, ""),
            Speed = fields.Required("speed", Scalars.Int, 0),
            TurnRate = fields.Required("turn_rate", Scalars.Int, 0),
            Sight = fields.Required("sight", Scalars.Int, 0),
            Locomotor = fields.Required("locomotor", Scalars.Id, ""),
            Placement = fields.Required("placement", PlacementOf, UnitPlacement.Cell),
            Turret = fields.Optional("turret", Scalars.Flag, false),
            Prerequisites = fields.Optional("prerequisites", Scalars.IdList, ImmutableArray<string>.Empty),
            Weapons = fields.Optional("weapons", Scalars.IdList, ImmutableArray<string>.Empty),
            Model = fields.Optional("model", Scalars.Path, ""),
            Icon = fields.Optional("icon", Scalars.Path, ""),
            HarvestCapacity = fields.Optional("harvest_capacity", Scalars.Int, 0),
            HarvestTime = fields.Optional("harvest_time", Scalars.Ticks, 0),
            UnloadRate = fields.Optional("unload_rate", Scalars.Int, 0),
        };
    }

    public static BuildingDef Building(RawDefinition definition, bool complete)
    {
        var fields = new FieldReader(definition, complete, BuildingKeys);
        return new BuildingDef
        {
            Id = definition.Id,
            Name = fields.Required("name", Scalars.Text, ""),
            Faction = fields.Required("faction", Scalars.Id, ""),
            Category = fields.Required("category", BuildingCategoryOf, ProductionCategory.Building),
            Cost = fields.Required("cost", Scalars.Int, 0),
            BuildTime = fields.Required("build_time", Scalars.Ticks, 0),
            Hp = fields.Required("hp", Scalars.Int, 0),
            Footprint = fields.Required("footprint", Scalars.Size, new CellSize(1, 1)),
            Power = fields.Optional("power", Scalars.Int, 0),
            BuildUp = fields.Optional("build_up", Scalars.Ticks, 0),
            ExitCell = fields.Maybe("exit_cell", Scalars.Cell),
            DockCell = fields.Maybe("dock_cell", Scalars.Cell),
            Adjacent = fields.Optional("adjacent", Scalars.Int, BuildingDef.DefaultAdjacent),
            ExtendsBase = fields.Optional("extends_base", Scalars.Flag, true),
            Produces = fields.Optional("produces", CategoriesOf, ImmutableArray<ProductionCategory>.Empty),
            Prerequisites = fields.Optional("prerequisites", Scalars.IdList, ImmutableArray<string>.Empty),
            Model = fields.Optional("model", Scalars.Path, ""),
            Icon = fields.Optional("icon", Scalars.Path, ""),
        };
    }

    public static WeaponDef Weapon(RawDefinition definition, bool complete)
    {
        var fields = new FieldReader(definition, complete, WeaponKeys);
        return new WeaponDef
        {
            Id = definition.Id,
            Name = fields.Required("name", Scalars.Text, ""),
            Damage = fields.Optional("damage", Scalars.Int, 0),
            Range = fields.Optional("range", Scalars.FixedNumber, Fixed.Zero),
            Reload = fields.Optional("reload", Scalars.Ticks, 0),
        };
    }

    public static LocomotorDef Locomotor(RawDefinition definition, bool complete)
    {
        var fields = new FieldReader(definition, complete, LocomotorKeys);
        return new LocomotorDef
        {
            Id = definition.Id,
            Name = fields.Required("name", Scalars.Text, ""),
            Slope = fields.Required("slope", Scalars.Percent, Fixed.Zero),
        };
    }

    public static TerrainDef Terrain(RawDefinition definition, bool complete)
    {
        var fields = new FieldReader(definition, complete, TerrainKeys);
        return new TerrainDef
        {
            Id = definition.Id,
            Name = fields.Required("name", Scalars.Text, ""),
            Code = fields.Required("code", Scalars.Code, ' '),
            Buildable = fields.Optional("buildable", Scalars.Flag, false),
            Speed = fields.Required("speed", Scalars.Speeds, ImmutableArray<LocomotorSpeed>.Empty),
        };
    }

    public static FactionDef Faction(RawDefinition definition, bool complete)
    {
        var fields = new FieldReader(definition, complete, FactionKeys);
        return new FactionDef
        {
            Id = definition.Id,
            Name = fields.Required("name", Scalars.Text, ""),
            Mcv = fields.Required("mcv", Scalars.Id, ""),
            ConstructionYard = fields.Required("construction_yard", Scalars.Id, ""),
            Refinery = fields.Required("refinery", Scalars.Id, ""),
            Harvester = fields.Required("harvester", Scalars.Id, ""),
        };
    }

    public static EconomyDef Economy(RawDefinition definition, bool complete)
    {
        var fields = new FieldReader(definition, complete, EconomyKeys);
        return new EconomyDef
        {
            Id = definition.Id,
            Name = fields.Required("name", Scalars.Text, ""),
            LowPowerMinRate = fields.Required("low_power_min_rate", Scalars.Percent, Fixed.Zero),
            MultipleFactoryBonus = fields.Required("multiple_factory_bonus", Scalars.Percent, Fixed.Zero),
            MaxFactoryRate = fields.Required("max_factory_rate", Scalars.Percent, Fixed.Zero),
            OreValue = fields.Required("ore_value", Scalars.Int, 0),
            GemValue = fields.Required("gem_value", Scalars.Int, 0),
            OreGrowthInterval = fields.Required("ore_growth_interval", Scalars.Ticks, 0),
            OreGrowthChance = fields.Required("ore_growth_chance", Scalars.Percent, Fixed.Zero),
            OreSpreadStage = fields.Required("ore_spread_stage", Scalars.Int, 0),
            OreSpreadChance = fields.Required("ore_spread_chance", Scalars.Percent, Fixed.Zero),
        };
    }

    /// <summary>A list of production categories, <c>[building, defense]</c>; each at most once.</summary>
    private static ImmutableArray<ProductionCategory> CategoriesOf(string fileName, YamlNode node)
    {
        if (node is not YamlSequenceNode sequence)
        {
            throw RulesFormatException.At(fileName, node, "expected a list of categories such as [infantry]");
        }

        var categories = ImmutableArray.CreateBuilder<ProductionCategory>(sequence.Children.Count);
        foreach (var child in sequence.Children)
        {
            var category = Scalars.Choice(fileName, child, "building", "defense", "infantry", "vehicle") switch
            {
                "building" => ProductionCategory.Building,
                "defense" => ProductionCategory.Defense,
                "infantry" => ProductionCategory.Infantry,
                _ => ProductionCategory.Vehicle,
            };
            if (categories.Contains(category))
            {
                throw RulesFormatException.At(fileName, child, $"category '{((YamlScalarNode)child).Value}' repeats");
            }

            categories.Add(category);
        }

        return categories.MoveToImmutable();
    }

    private static UnitCategory UnitCategoryOf(string fileName, YamlNode node) =>
        Scalars.Choice(fileName, node, "infantry", "vehicle") == "infantry" ? UnitCategory.Infantry : UnitCategory.Vehicle;

    private static ProductionCategory BuildingCategoryOf(string fileName, YamlNode node) =>
        Scalars.Choice(fileName, node, "building", "defense") == "building" ? ProductionCategory.Building : ProductionCategory.Defense;

    private static UnitPlacement PlacementOf(string fileName, YamlNode node) =>
        Scalars.Choice(fileName, node, "cell", "subcell") == "cell" ? UnitPlacement.Cell : UnitPlacement.Subcell;
}
