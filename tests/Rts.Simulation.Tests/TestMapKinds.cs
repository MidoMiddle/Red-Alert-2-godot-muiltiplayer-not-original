using Rts.Simulation.Map;
using Rts.Simulation.Math;
using Rts.Simulation.Rules;

namespace Rts.Simulation.Tests;

/// <summary>
/// The kinds of the map of <c>game/data</c> (map/objects.yaml, map/bridges.yaml, neutral/*.yaml; M3 step 14a), built in code for
/// <see cref="TestRules"/>: a test of Rts.Data checks that both give the same <see cref="MapTypes"/>.
/// </summary>
internal static class TestMapKinds
{
    public static void AddTo(RulesBuilder builder)
    {
        builder.Add(new MapObjectDef { Id = "pine_tree", Name = "Pine tree", Blocks = ["amphibious", "foot", "hover", "naval", "track", "wheel"], Hp = 400, CrushedBy = [], Stump = true, Speeds = [] });
        builder.Add(new MapObjectDef { Id = "boulder", Name = "Boulder", Blocks = ["amphibious", "foot", "hover", "naval", "track", "wheel"], Hp = 0, CrushedBy = [], Stump = false, Speeds = [] });
        builder.Add(new MapObjectDef { Id = "fence_wood", Name = "Wooden fence", Blocks = ["amphibious", "foot", "hover", "naval", "wheel"], Hp = 150, CrushedBy = ["track"], Stump = false, Speeds = [] });
        builder.Add(new MapObjectDef { Id = "wall_stone", Name = "Stone wall", Blocks = ["amphibious", "foot", "hover", "naval", "track", "wheel"], Hp = 1500, CrushedBy = [], Stump = false, Speeds = [] });
        builder.Add(new MapObjectDef { Id = "lamp_post", Name = "Lamp post", Blocks = [], Hp = 50, CrushedBy = ["amphibious", "foot", "hover", "naval", "track", "wheel"], Stump = false, Speeds = [] });
        builder.Add(new MapObjectDef { Id = "anti_tank_hedgehog", Name = "Anti-tank hedgehog", Blocks = ["track", "wheel"], Hp = 400, CrushedBy = [], Stump = false, Speeds = [] });
        builder.Add(new MapObjectDef { Id = "pipeline", Name = "Pipeline", Blocks = ["track", "wheel"], Hp = 50, CrushedBy = [], Stump = false, Speeds = [] });
        builder.Add(new MapObjectDef { Id = "snowdrift", Name = "Snowdrift", Blocks = [], Hp = 0, CrushedBy = ["track"], Stump = false, Speeds = [new LocomotorSpeed("foot", Percent(60)), new LocomotorSpeed("track", Percent(80)), new LocomotorSpeed("wheel", Percent(50))] });
        builder.Add(new MapObjectDef { Id = "ore_source", Name = "Ore drill", Blocks = ["amphibious", "foot", "hover", "naval", "track", "wheel"], Hp = 0, CrushedBy = [], Stump = false, Speeds = [] });
        builder.Add(new MapObjectDef { Id = "gem_source", Name = "Gem drill", Blocks = ["amphibious", "foot", "hover", "naval", "track", "wheel"], Hp = 0, CrushedBy = [], Stump = false, Speeds = [] });
        builder.Add(new MapObjectDef { Id = "bench", Name = "Bench", Blocks = [], Hp = 50, CrushedBy = ["track", "wheel"], Stump = false, Speeds = [] });
        builder.Add(new MapObjectDef { Id = "trash_bin", Name = "Trash bin", Blocks = [], Hp = 40, CrushedBy = ["track", "wheel"], Stump = false, Speeds = [] });
        builder.Add(new MapObjectDef { Id = "mailbox", Name = "Mailbox", Blocks = [], Hp = 40, CrushedBy = ["track", "wheel"], Stump = false, Speeds = [] });
        builder.Add(new MapObjectDef { Id = "phone_booth", Name = "Phone booth", Blocks = ["amphibious", "foot", "hover", "naval", "track", "wheel"], Hp = 100, CrushedBy = ["track"], Stump = false, Speeds = [] });
        builder.Add(new MapObjectDef { Id = "bus_stop", Name = "Bus stop", Blocks = [], Hp = 100, CrushedBy = ["track"], Stump = false, Speeds = [] });
        builder.Add(new MapObjectDef { Id = "billboard", Name = "Billboard", Blocks = ["amphibious", "foot", "hover", "naval", "track", "wheel"], Hp = 150, CrushedBy = [], Stump = false, Speeds = [] });
        builder.Add(new MapObjectDef { Id = "road_sign", Name = "Road sign", Blocks = [], Hp = 30, CrushedBy = ["track", "wheel"], Stump = false, Speeds = [] });
        builder.Add(new MapObjectDef { Id = "traffic_light", Name = "Traffic light", Blocks = ["amphibious", "foot", "hover", "naval", "track", "wheel"], Hp = 200, CrushedBy = ["track"], Stump = false, Speeds = [] });
        builder.Add(new MapObjectDef { Id = "flowerbed", Name = "Flowerbed", Blocks = [], Hp = 50, CrushedBy = ["track", "wheel"], Stump = false, Speeds = [] });
        builder.Add(new MapObjectDef { Id = "street_stall", Name = "Street stall", Blocks = ["track", "wheel"], Hp = 80, CrushedBy = ["track"], Stump = false, Speeds = [] });
        builder.Add(new MapObjectDef { Id = "beach_umbrella", Name = "Beach umbrella", Blocks = [], Hp = 30, CrushedBy = ["track", "wheel"], Stump = false, Speeds = [] });
        builder.Add(new MapObjectDef { Id = "sun_lounger", Name = "Sun lounger", Blocks = [], Hp = 30, CrushedBy = ["track", "wheel"], Stump = false, Speeds = [] });
        builder.Add(new MapObjectDef { Id = "beach_shower", Name = "Beach shower", Blocks = [], Hp = 50, CrushedBy = ["track", "wheel"], Stump = false, Speeds = [] });
        builder.Add(new MapObjectDef { Id = "volleyball_net", Name = "Volleyball net", Blocks = [], Hp = 40, CrushedBy = ["track", "wheel"], Stump = false, Speeds = [] });
        builder.Add(new MapObjectDef { Id = "fishing_nets", Name = "Fishing nets", Blocks = [], Hp = 20, CrushedBy = ["track", "wheel"], Stump = false, Speeds = [] });
        builder.Add(new MapObjectDef { Id = "driftwood", Name = "Driftwood", Blocks = [], Hp = 50, CrushedBy = ["track"], Stump = false, Speeds = [] });
        builder.Add(new MapObjectDef { Id = "beached_boat", Name = "Beached boat", Blocks = ["amphibious", "foot", "hover", "naval", "track", "wheel"], Hp = 300, CrushedBy = [], Stump = false, Speeds = [] });
        builder.Add(new MapObjectDef { Id = "dry_shrub", Name = "Dry shrub", Blocks = [], Hp = 30, CrushedBy = ["amphibious", "foot", "hover", "naval", "track", "wheel"], Stump = false, Speeds = [] });
        builder.Add(new MapObjectDef { Id = "hedge", Name = "Hedge", Blocks = ["amphibious", "foot", "hover", "naval", "track", "wheel"], Hp = 300, CrushedBy = [], Stump = false, Speeds = [] });
        builder.Add(new MapObjectDef { Id = "hay_bale", Name = "Hay bale", Blocks = ["amphibious", "foot", "hover", "naval", "track", "wheel"], Hp = 100, CrushedBy = ["track"], Stump = false, Speeds = [] });
        builder.Add(new MapObjectDef { Id = "well", Name = "Well", Blocks = ["amphibious", "foot", "hover", "naval", "track", "wheel"], Hp = 1500, CrushedBy = [], Stump = false, Speeds = [] });
        builder.Add(new MapObjectDef { Id = "barbed_wire", Name = "Barbed wire", Blocks = ["amphibious", "foot", "hover", "naval", "wheel"], Hp = 100, CrushedBy = ["track"], Stump = false, Speeds = [] });
        builder.Add(new MapObjectDef { Id = "farm_tractor", Name = "Farm tractor", Blocks = ["amphibious", "foot", "hover", "naval", "track", "wheel"], Hp = 400, CrushedBy = [], Stump = false, Speeds = [] });

        builder.Add(new BridgeDef { Id = "bridge_wood_low", Name = "Low wooden bridge", SegmentHp = 600, Width = 3, High = false });
        builder.Add(new BridgeDef { Id = "bridge_concrete_high", Name = "High concrete bridge", SegmentHp = 1500, Width = 3, High = true });

        builder.Add(new MapBuildingDef { Id = "neutral_shed", Name = "Shed", Footprint = new CellSize(1, 1), Hp = 300 });
        builder.Add(new MapBuildingDef { Id = "neutral_house_a", Name = "House", Footprint = new CellSize(2, 2), Hp = 800, Garrison = 3 });
        builder.Add(new MapBuildingDef { Id = "neutral_apartment", Name = "Apartment block", Footprint = new CellSize(3, 3), Hp = 1500, Garrison = 12 });
        builder.Add(new MapBuildingDef { Id = "neutral_fuel_tank", Name = "Fuel tank", Footprint = new CellSize(1, 1), Hp = 800 });
        builder.Add(new MapBuildingDef { Id = "neutral_smokestack", Name = "Smokestack", Footprint = new CellSize(1, 1), Hp = 1500 });
        builder.Add(new MapBuildingDef { Id = "neutral_cooling_tower", Name = "Cooling tower", Footprint = new CellSize(2, 2), Hp = 1500 });
        builder.Add(new MapBuildingDef { Id = "neutral_factory_hall", Name = "Factory hall", Footprint = new CellSize(4, 3), Hp = 800, Garrison = 12 });
        builder.Add(new MapBuildingDef { Id = "neutral_boiler_house", Name = "Boiler house", Footprint = new CellSize(2, 2), Hp = 800, Garrison = 3 });
        builder.Add(new MapBuildingDef { Id = "neutral_school", Name = "School", Footprint = new CellSize(3, 2), Hp = 1500, Garrison = 9 });
        builder.Add(new MapBuildingDef { Id = "neutral_office", Name = "Office", Footprint = new CellSize(2, 2), Hp = 1500, Garrison = 6 });
        builder.Add(new MapBuildingDef { Id = "neutral_church", Name = "Church", Footprint = new CellSize(2, 3), Hp = 1000, Garrison = 6 });
        builder.Add(new MapBuildingDef { Id = "neutral_shop", Name = "Shop", Footprint = new CellSize(2, 2), Hp = 800 });
        builder.Add(new MapBuildingDef { Id = "neutral_kiosk", Name = "Kiosk", Footprint = new CellSize(1, 1), Hp = 200 });
        builder.Add(new MapBuildingDef { Id = "neutral_skyscraper", Name = "Skyscraper", Footprint = new CellSize(2, 2), Hp = 2500, Garrison = 12 });
        builder.Add(new MapBuildingDef { Id = "neutral_cafe", Name = "Cafe", Footprint = new CellSize(2, 1), Hp = 600 });
        builder.Add(new MapBuildingDef { Id = "neutral_bank", Name = "Bank", Footprint = new CellSize(3, 2), Hp = 2000, Garrison = 9 });
        builder.Add(new MapBuildingDef { Id = "neutral_hotel", Name = "Hotel", Footprint = new CellSize(3, 2), Hp = 1800, Garrison = 9 });
        builder.Add(new MapBuildingDef { Id = "neutral_fire_station", Name = "Fire station", Footprint = new CellSize(3, 2), Hp = 1500, Garrison = 9 });
        builder.Add(new MapBuildingDef { Id = "neutral_auto_repair", Name = "Auto repair shop", Footprint = new CellSize(2, 2), Hp = 800 });
        builder.Add(new MapBuildingDef { Id = "neutral_fountain", Name = "Fountain", Footprint = new CellSize(2, 2), Hp = 1500 });
        builder.Add(new MapBuildingDef { Id = "neutral_beach_cabin", Name = "Beach cabin", Footprint = new CellSize(1, 1), Hp = 300 });
        builder.Add(new MapBuildingDef { Id = "neutral_beach_bar", Name = "Beach bar", Footprint = new CellSize(3, 2), Hp = 600 });
        builder.Add(new MapBuildingDef { Id = "neutral_lifeguard_tower", Name = "Lifeguard tower", Footprint = new CellSize(2, 2), Hp = 500, Garrison = 3 });
        builder.Add(new MapBuildingDef { Id = "neutral_lighthouse", Name = "Lighthouse", Footprint = new CellSize(2, 2), Hp = 2000, Garrison = 6 });
        builder.Add(new MapBuildingDef { Id = "neutral_boat_shed", Name = "Boat shed", Footprint = new CellSize(2, 2), Hp = 600 });
        builder.Add(new MapBuildingDef { Id = "neutral_adobe_house", Name = "Adobe house", Footprint = new CellSize(2, 2), Hp = 600, Garrison = 3 });
        builder.Add(new MapBuildingDef { Id = "neutral_ancient_ruins", Name = "Ancient ruins", Footprint = new CellSize(2, 2), Hp = 2500 });
        builder.Add(new MapBuildingDef { Id = "neutral_rock_mesa", Name = "Rock mesa", Footprint = new CellSize(2, 2), Hp = 4000 });
        builder.Add(new MapBuildingDef { Id = "neutral_rock_arch", Name = "Rock arch", Footprint = new CellSize(2, 1), Hp = 3000 });
        builder.Add(new MapBuildingDef { Id = "neutral_port_crane", Name = "Port crane", Footprint = new CellSize(1, 1), Hp = 1500 });
        builder.Add(new MapBuildingDef { Id = "neutral_container_stack", Name = "Container stack", Footprint = new CellSize(2, 1), Hp = 1500 });
        builder.Add(new MapBuildingDef { Id = "neutral_port_warehouse", Name = "Port warehouse", Footprint = new CellSize(3, 3), Hp = 1200, Garrison = 12 });
        builder.Add(new MapBuildingDef { Id = "neutral_water_tower", Name = "Water tower", Footprint = new CellSize(1, 1), Hp = 800 });
        builder.Add(new MapBuildingDef { Id = "neutral_farmhouse", Name = "Farmhouse", Footprint = new CellSize(2, 2), Hp = 700, Garrison = 3 });
        builder.Add(new MapBuildingDef { Id = "neutral_barn", Name = "Barn", Footprint = new CellSize(3, 2), Hp = 800 });
        builder.Add(new MapBuildingDef { Id = "neutral_silo", Name = "Silo", Footprint = new CellSize(1, 1), Hp = 1500 });
        builder.Add(new MapBuildingDef { Id = "neutral_windmill", Name = "Windmill", Footprint = new CellSize(2, 2), Hp = 700 });
        builder.Add(new MapBuildingDef { Id = "neutral_civilian_bunker", Name = "Civilian bunker", Footprint = new CellSize(2, 2), Hp = 2000, Garrison = 8, Armor = "steel", Sight = 6 });
        builder.Add(new MapBuildingDef { Id = "tech_oil_derrick", Name = "Oil derrick", Footprint = new CellSize(2, 2), Hp = 1000, Capturable = true, DeathWeapon = "oil_explosion", ExtendsBase = true });
        builder.Add(new MapBuildingDef { Id = "tech_hospital", Name = "Hospital", Footprint = new CellSize(6, 4), Hp = 800, Capturable = true, ExtendsBase = true });
        builder.Add(new MapBuildingDef { Id = "tech_airfield", Name = "Airfield", Footprint = new CellSize(3, 3), Hp = 800, Capturable = true, Superweapon = SuperweaponKind.Paradrop, Recharge = 240 * SimulationSettings.DefaultTicksPerSecond, ExtendsBase = true });
        builder.Add(new MapBuildingDef { Id = "tech_bridge_repair_hut", Name = "Bridge repair hut", Footprint = new CellSize(1, 1), Hp = 2000 });
        builder.Add(new MapBuildingDef { Id = "tech_outpost", Name = "Tech outpost", Footprint = new CellSize(4, 3), Hp = 2000, Capturable = true, Weapon = "alliance_ifv_missiles", Armor = "concrete", Sight = 6, RepairsVehicles = true, ExtendsBase = true });
    }

    private static Fixed Percent(int percent) => Fixed.FromRaw(IntMath.DivRound(percent * Fixed.OneRaw, 100));
}
