using Rts.Simulation.Events;
using Rts.Simulation.Matches;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Matches;

/// <summary>
/// A count of what the players did, by the events of every tick, kept by a test beside the simulation's own
/// (docs/implementation/M5_first_bot.md, step 11): it reads the events while the fallen are still in the world, as <see cref="MatchStats"/>
/// does, and the credits every player had at the start. <see cref="AssertAgrees"/> holds the statistics against it.
/// </summary>
public sealed class EventTally
{
    private readonly int[] _unitsBuilt = new int[PlayerId.MaxPlayers + 1];
    private readonly int[] _buildingsBuilt = new int[PlayerId.MaxPlayers + 1];
    private readonly int[] _unitsLost = new int[PlayerId.MaxPlayers + 1];
    private readonly int[] _buildingsLost = new int[PlayerId.MaxPlayers + 1];
    private readonly int[] _unitsDestroyed = new int[PlayerId.MaxPlayers + 1];
    private readonly int[] _buildingsDestroyed = new int[PlayerId.MaxPlayers + 1];
    private readonly int[] _startCredits = new int[PlayerId.MaxPlayers + 1];
    private bool _started;

    /// <summary>The events the tally saw: how many of each kind of interest, for a test to know it saw something.</summary>
    public int Deaths { get; private set; }

    /// <summary>Counts the events of <paramref name="simulation"/> from its next tick on.</summary>
    public static EventTally Attach(GameSimulation simulation)
    {
        var tally = new EventTally();
        simulation.Probe = phase =>
        {
            if (!tally._started)
            {
                tally._started = true;
                foreach (var player in simulation.World.Players)
                {
                    tally._startCredits[player.Id.Value] = player.Credits;
                }
            }

            if (phase == "statistics")
            {
                tally.Count(simulation.World, simulation.LastTickEvents);
            }
        };
        return tally;
    }

    private void Count(GameWorld world, IReadOnlyList<SimEvent> events)
    {
        foreach (var simEvent in events)
        {
            switch (simEvent)
            {
                case UnitProduced produced:
                    _unitsBuilt[produced.Player.Value]++;
                    break;
                case BuildingPlaced placed:
                    _buildingsBuilt[placed.Player.Value]++;
                    break;
                case EntityDestroyed destroyed:
                    Assert.True(world.TryGet(destroyed.Entity, out var dead));
                    if (dead.Owner.IsNeutral)
                    {
                        break;
                    }

                    Deaths++;
                    var building = dead is Building;
                    (building ? _buildingsLost : _unitsLost)[dead.Owner.Value]++;
                    if (!destroyed.KillerOwner.IsNeutral && destroyed.KillerOwner != dead.Owner)
                    {
                        (building ? _buildingsDestroyed : _unitsDestroyed)[destroyed.KillerOwner.Value]++;
                    }

                    break;
            }
        }
    }

    /// <summary>
    /// The statistics of <paramref name="world"/> say what the events said, for every player; and every player's credits are those of
    /// the start, plus the harvest, less the spending, plus the sales.
    /// </summary>
    public void AssertAgrees(GameWorld world)
    {
        foreach (var player in world.Players)
        {
            var id = player.Id.Value;
            var stats = world.Stats.Of(player.Id);
            Assert.Equal(
                (_unitsBuilt[id], _buildingsBuilt[id], _unitsLost[id], _buildingsLost[id], _unitsDestroyed[id], _buildingsDestroyed[id]),
                (stats.UnitsBuilt, stats.BuildingsBuilt, stats.UnitsLost, stats.BuildingsLost, stats.UnitsDestroyed, stats.BuildingsDestroyed));
            Assert.Equal(player.Credits, _startCredits[id] + stats.CreditsHarvested - stats.CreditsSpent + stats.CreditsFromSales);
        }
    }
}
