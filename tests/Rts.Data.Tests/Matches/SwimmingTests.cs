using Rts.Simulation;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Matches;
using Rts.Simulation.World;

namespace Rts.Data.Tests.Matches;

/// <summary>
/// Tanya of RA2 swims (docs/ra2_units/allies/tanya: <c>SpeedType=Amphibious</c>): ordered across a river she crosses it, where a soldier on
/// foot cannot. By the game data, without the fog of war; two people play, no bot.
/// </summary>
public sealed class SwimmingTests
{
    private const int Size = 64;
    private static readonly PlayerId Allied = new(1);

    // A deep river from x = 24 to x = 31, from top to bottom.
    private static GameSimulation Match()
    {
        var land = new byte[Size * Size];
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                land[(y * Size) + x] = (byte)(x is >= 24 and < 32 ? LandType.Deep : LandType.Clear);
            }
        }

        var map = MapGrid.FromLayers(Size, Size, land, new byte[Size * Size], default, null, null, [], types: GameDataFiles.Rules.MapTypes);
        var simulation = new GameSimulation(
            SimulationSettings.ForMatch(
                new MatchSetup(
                    "swimming",
                    71,
                    new MatchOptions(StartingCredits: 10_000, FogOfWar: false, StartingUnits: 0),
                    [
                        new MatchPlayer(1, PlayerKind.Human, AiDifficulty.Medium, "alliance", Color: 0, Team: 1, StartIndex: 1, new CellPos(4, 4)),
                        new MatchPlayer(2, PlayerKind.Human, AiDifficulty.Medium, "union", Color: 1, Team: 2, StartIndex: 2, new CellPos(4, 60)),
                    ],
                    [new CellPos(4, 4), new CellPos(4, 60)]),
                map),
            GameDataFiles.Rules);
        simulation.World.VisionOf(Allied).ExploreAll();
        return simulation;
    }

    private static Unit Cross(UnitType type)
    {
        var simulation = Match();
        var unit = simulation.World.SpawnUnit(Allied, type, new CellPos(16, 20).Center, snapToSubcell: true);
        simulation.Enqueue(new MoveCommand(Allied, simulation.CurrentTick, 0, [unit.Id], new CellPos(40, 20).Center));
        for (var tick = 0; tick < 1200 && unit.Position.ToCell().X < 36; tick++)
        {
            simulation.Step();
        }

        return unit;
    }

    [Fact]
    public void Tanya_swims_across_a_river()
    {
        Assert.Equal(Locomotor.Amphibious, GameDataFiles.Rules.Unit(UnitType.AllianceTanya).Locomotor);

        Assert.True(Cross(UnitType.AllianceTanya).Position.ToCell().X >= 36);
    }

    [Fact]
    public void A_soldier_on_foot_stays_on_the_bank()
    {
        Assert.True(Cross(UnitType.AllianceRifleman).Position.ToCell().X < 24);
    }
}
