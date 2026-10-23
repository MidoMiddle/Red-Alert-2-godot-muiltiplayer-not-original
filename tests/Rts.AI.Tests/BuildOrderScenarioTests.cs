using Rts.Data.Tests;
using Rts.Simulation.Pathfinding;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.World;

namespace Rts.AI.Tests;

/// <summary>
/// The base of the «Medium» bot by the build order of the data (docs/implementation/M5_first_bot.md, step 5): every step is built in
/// its order, the exits of the factories and the docks of the refineries stay open to the rest of the map, and no command of the bot is
/// refused.
/// </summary>
[Trait("Category", "Slow")]
public sealed class BuildOrderScenarioTests
{
    private static readonly string[] Order =
        GameDataFiles.Rules.Database.BuildOrders.Single(order => order.Id == "standard").Steps.ToArray();

    /// <summary>Whether the placed buildings hold every step of the order, in its order, whatever else came between.</summary>
    private static bool FollowsTheOrder(BotMatch match)
    {
        var placed = match.Timeline.Where(line => line.Text.StartsWith("placed ", StringComparison.Ordinal))
            .Select(line => line.Text.Split(' ')[1]).ToList();
        var next = 0;
        foreach (var id in placed)
        {
            next += next < Order.Length && id == Order[next] ? 1 : 0;
        }

        return next == Order.Length;
    }

    /// <summary>Every exit and dock of the bot's base is free and has a way by ground to the start of the person.</summary>
    private static void PassagesAreOpen(BotMatch match)
    {
        var map = match.World.Map;
        var person = match.Setup.Players[0].Start;
        foreach (var building in match.World.Entities.OfType<Building>().Where(b => b.IsAlive && b.Owner == BotMatch.Bot && b.Def is not null))
        {
            foreach (var opening in new[] { building.Def!.ExitCell, building.Def.DockCell })
            {
                if (opening is not { } relative)
                {
                    continue;
                }

                var cell = new CellPos(building.Cell.X + relative.X, building.Cell.Y + relative.Y);
                Assert.False(map.HasBuilding(cell), $"{building.Def.Id} at {building.Cell}: its opening {cell} is built over");
                var path = AStar.FindPath(map, cell, person, Locomotor.Track, maxExpansions: 200_000);
                Assert.True(path.Status == PathStatus.Found, $"{building.Def.Id} at {building.Cell}: no way from {cell} to {person}");
            }
        }
    }

    [Theory]
    [InlineData("frozen-crossing", 12)]
    [InlineData("twin-rivers", 13)]
    [InlineData("taiga-front", 13)]
    public void The_bot_builds_the_whole_order_keeps_its_passages_open_and_gives_no_refused_command(string map, int minutes)
    {
        var match = new BotMatch(map).PlayTo(minutes);

        Assert.True(FollowsTheOrder(match), match.Report());
        PassagesAreOpen(match);
        Assert.Empty(match.RefusedBotCommands);
    }

    [Fact]
    public void The_tesla_coil_stands_on_the_side_of_the_enemy()
    {
        var match = new BotMatch().PlayTo(12);

        // Frozen Crossing: the bot is on the east start, the person on the west.
        var coil = Assert.Single(match.BuildingsOfBot("tesla_coil"));
        var yard = Assert.Single(match.BuildingsOfBot("construction_yard"));
        Assert.True(coil.Cell.X < yard.Cell.X, match.Report());
    }
}
