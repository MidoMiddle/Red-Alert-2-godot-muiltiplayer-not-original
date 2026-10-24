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
