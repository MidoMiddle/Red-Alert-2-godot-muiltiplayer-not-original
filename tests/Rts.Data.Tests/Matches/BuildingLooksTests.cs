using Rts.Client.Terrain;

namespace Rts.Data.Tests.Matches;

/// <summary>
/// The placeholder looks of the buildings by the game data (the owner's wish, 2026-10-24): every building of the players has one of its
/// own, inside its footprint, and no two of a faction look the same.
/// </summary>
public sealed class BuildingLooksTests
{
    [Fact]
    public void Every_building_of_the_players_has_a_look_of_its_own_within_its_footprint()
    {
        foreach (var def in GameDataFiles.Rules.Database.Buildings.Where(def => def.Faction is "alliance" or "union"))
        {
            Assert.True(BuildingLooks.Known(def.Id), def.Id);
            var (halfW, halfH) = (def.Footprint.Width / 2f, def.Footprint.Height / 2f);
            Assert.All(BuildingLooks.Of(def), part => Assert.True(
                part.Tilt != 0f || (MathF.Abs(part.X) + part.SizeX / 2f <= halfW + 0.05f && MathF.Abs(part.Z) + part.SizeZ / 2f <= halfH + 0.05f),
                $"{def.Id}: a part at {part.X}, {part.Z} sticks out of {def.Footprint.Width}x{def.Footprint.Height}"));
        }
    }

    [Fact]
    public void Every_building_and_object_of_the_map_has_a_look_and_it_stays_in_its_cells()
    {
        // 2026-10-25: the buildings and props of the element documents; the older ones keep the placeholders of Buildings.cs and
        // MapObjects.cs (or their models).
        string[] older = ["neutral_shed", "neutral_house_a", "neutral_apartment", "neutral_fuel_tank", "neutral_smokestack", "neutral_cooling_tower",
            "neutral_factory_hall", "neutral_boiler_house", "tech_oil_derrick", "tech_hospital", "tech_airfield", "tech_bridge_repair_hut"];
        foreach (var type in Rts.Simulation.World.BuildingTypes.All.Where(type => !older.Contains(type.Id)))
        {
            Assert.True(BuildingLooks.Known(type.Id), type.Id);
            Assert.All(BuildingLooks.Of(type.Id, type.Width, type.Height, false), part => Assert.True(
                part.Tilt != 0f || (MathF.Abs(part.X) + part.SizeX / 2f <= type.Width / 2f + 0.05f && MathF.Abs(part.Z) + part.SizeZ / 2f <= type.Height / 2f + 0.05f),
                $"{type.Id}: a part at {part.X}, {part.Z} sticks out of {type.Width}x{type.Height}"));
        }

        string[] olderObjects = ["pine_tree", "boulder", "fence_wood", "wall_stone", "lamp_post", "anti_tank_hedgehog", "pipeline", "snowdrift", "ore_source", "gem_source"];
        foreach (var type in Rts.Simulation.Map.MapObjectTypes.All.Where(type => !olderObjects.Contains(type.Id)))
        {
            Assert.True(PropLooks.Known(type.Id), type.Id);
            Assert.All(PropLooks.Of(type.Id), part => Assert.True(
                part.Tilt != 0f || part.Yaw != 0f || (MathF.Abs(part.X) + part.SizeX / 2f <= 0.55f && MathF.Abs(part.Z) + part.SizeZ / 2f <= 0.55f),
                $"{type.Id}: a part at {part.X}, {part.Z} sticks out of its cell"));
        }
    }

    [Fact]
    public void No_two_buildings_of_a_faction_look_the_same()
    {
        foreach (var faction in new[] { "alliance", "union" })
        {
            var looks = GameDataFiles.Rules.Database.Buildings.Where(def => def.Faction == faction)
                .Select(def => (def.Id, Parts: string.Join(";", BuildingLooks.Of(def)))).ToList();
            Assert.Equal(looks.Count, looks.Select(look => look.Parts).Distinct().Count());
        }
    }
}
