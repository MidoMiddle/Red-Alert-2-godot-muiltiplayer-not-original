using Rts.Client.Debug;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Client;

/// <summary>The totals of what a bot built, at the end of its journal (2026-10-26; <see cref="AiJournals"/>).</summary>
public sealed class AiJournalsTests
{
    private static readonly PlayerId Person = new(1);
    private static readonly PlayerId Bot = new(2);

    [Fact]
    public void The_journal_of_a_bot_ends_with_what_it_built_once_and_not_what_the_person_built()
    {
        var world = new GameWorld(seed: 1, widthCells: 20, heightCells: 20, TestRules.Standard);
        var path = Path.Combine(Path.GetTempPath(), $"journal-{Guid.NewGuid():N}.log");
        var journals = new AiJournals(new SortedDictionary<int, string> { [Bot.Value] = path });
        var conscripts = new[] { world.SpawnUnit(Bot, UnitType.Conscript, new CellPos(2, 2).Center), world.SpawnUnit(Bot, UnitType.Conscript, new CellPos(3, 2).Center) };
        var tank = world.SpawnUnit(Bot, UnitType.HeavyTank, new CellPos(5, 5).Center);
        var theirs = world.SpawnUnit(Person, UnitType.HeavyTank, new CellPos(9, 9).Center);
        var reactor = TestRules.Standard.Database.IndexOfBuilding("reactor");

        foreach (var unit in conscripts)
        {
            journals.Record(new UnitProduced(10, Bot, unit.Id, default), world);
        }

        journals.Record(new UnitProduced(20, Bot, tank.Id, default), world);
        journals.Record(new UnitProduced(20, Person, theirs.Id, default), world);
        journals.Record(new BuildingPlaced(30, default, Bot, reactor, new CellPos(12, 12)), world);
        journals.Record(new BuildingPlaced(31, default, Person, reactor, new CellPos(15, 15)), world);
        journals.Finish("the match ended");
        journals.Finish("the game left the match");

        try
        {
            var lines = File.ReadAllLines(path);
            Assert.Equal(["0:02 totals (the match ended): units — 3: conscript 2, heavy_tank 1; buildings — 1: reactor 1"], lines);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
