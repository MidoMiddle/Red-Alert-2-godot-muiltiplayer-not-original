using Rts.Client.Matches;
using Rts.Data.Maps;
using Rts.Data.Tests;
using Rts.Simulation;
using Rts.Simulation.Events;
using Rts.Simulation.Matches;
using Rts.Simulation.Replays;
using Rts.Simulation.Rules;
using Rts.Simulation.World;

namespace Rts.AI.Tests;

/// <summary>
/// A match on a ready map by the real data (docs/implementation/M5_first_bot.md, section 6, «scenarios without the client»): a person who
/// does nothing on the first start against the «Medium» bot of <see cref="MatchBots"/> on the second. Steps it and writes down what the
/// bot did, so that a scenario can ask when.
/// </summary>
internal sealed class BotMatch
{
    public static readonly PlayerId Bot = new(2);

    private readonly List<(int Tick, string Text)> _timeline = [];
    private uint _personSequence;

    public BotMatch(string mapId = "frozen-crossing", ulong seed = 3)
    {
        var map = RtsMapReader.Read(File.ReadAllText(Path.Combine(GameDataFiles.Folder, "maps", mapId + ".rtsmap")));
        Setup = MatchSetups.OneOnOne(map, GameDataFiles.Rules.DefaultFaction.Id, seed);
        Simulation = new GameSimulation(SimulationSettings.ForMatch(Setup, map.ToMapGrid()), GameDataFiles.Rules);
        foreach (var player in Setup.Players.Where(player => player.Kind == PlayerKind.Ai))
        {
            var bot = (AiPlayer)MatchBots.Create(Setup, player);
            Simulation.AttachAi(bot);
            Brain ??= bot;
        }

        Recorder = new ReplayRecorder(Simulation);
    }

    public MatchSetup Setup { get; }

    /// <summary>The bot of <see cref="Bot"/>: what it sees, remembers and plans.</summary>
    public AiPlayer? Brain { get; }

    public GameSimulation Simulation { get; }

    public ReplayRecorder Recorder { get; }

    public GameWorld World => Simulation.World;

    private RulesDatabase Data => GameDataFiles.Rules.Database;

    public IReadOnlyList<(int Tick, string Text)> Timeline => _timeline;

    /// <summary>The commands of the bot that changed nothing.</summary>
    public IEnumerable<Rts.Simulation.Commands.Command> RefusedBotCommands =>
        Simulation.RefusedCommands.Where(command => command.Player == Bot && command.Sequence >= Rts.Simulation.Ai.AiTurn.FirstSequence);

    /// <summary>
    /// Gives the person <paramref name="count"/> units of <paramref name="type"/> round their start on the next tick, by commands of the
    /// person — so that a replay has them too. A cell with no room is passed over.
    /// </summary>
    public void GivePerson(UnitType type, int count)
    {
        var person = Setup.Players.First(player => player.Kind == PlayerKind.Human);
        var given = 0;
        for (var ring = 2; given < count && ring < 8; ring++)
        {
            for (var dx = -ring; dx <= ring && given < count; dx += 2)
            {
                var cell = new Rts.Simulation.Geometry.CellPos(person.Start.X + dx, person.Start.Y + ring);
                if (World.Map.Contains(cell) && World.Map.CanEnter(cell, World.Rules.Unit(type).Locomotor) && !World.Map.HasBuilding(cell))
                {
                    Simulation.Enqueue(new Rts.Simulation.Commands.SpawnTestUnitCommand(person.Id, Simulation.CurrentTick, _personSequence++, type, cell.Center));
                    given++;
                }
            }
        }
    }

    /// <summary>Plays until <paramref name="minutes"/> of the match are over.</summary>
    public BotMatch PlayTo(int minutes)
    {
        while (Simulation.CurrentTick < minutes * 900)
        {
            Step();
        }

        return this;
    }

    /// <summary>Plays one tick and writes down what the bot did in it.</summary>
    public void Step()
    {
        Simulation.Step();
        Recorder.AfterStep();
        Note();
    }

    /// <summary>The first tick a line of the timeline starts with <paramref name="text"/>, or null.</summary>
    public int? When(string text) => _timeline.Where(line => line.Text.StartsWith(text, StringComparison.Ordinal)).Select(line => (int?)line.Tick).FirstOrDefault();

    public List<Unit> UnitsOfBot(Func<Unit, bool> which) =>
        World.Entities.OfType<Unit>().Where(unit => unit.IsAlive && unit.Owner == Bot && which(unit)).ToList();

    public List<Building> BuildingsOfBot(string id) =>
        World.Entities.OfType<Building>().Where(building => building.IsAlive && building.Owner == Bot && building.Def?.Id == id).ToList();

    public string Report() => string.Join("\n", _timeline.Select(line => $"{line.Tick / 900}:{line.Tick % 900 / 15:00} {line.Text}"));

    private void Note()
    {
        var tick = Simulation.CurrentTick - 1;
        foreach (var simEvent in Simulation.LastTickEvents)
        {
            switch (simEvent)
            {
                case Deployed deployed when World.TryGet(deployed.Building, out var yard) && yard.Owner == Bot:
                    _timeline.Add((tick, "deployed"));
                    break;
                case BuildingPlaced placed when placed.Player == Bot:
                    _timeline.Add((tick, $"placed {Data.Buildings[placed.DefIndex].Id} at {placed.Cell.X},{placed.Cell.Y}"));
                    break;
                case UnitProduced produced when produced.Player == Bot && World.TryGet(produced.Unit, out var unit) && unit is Unit made:
                    _timeline.Add((tick, $"produced {made.Archetype.Id}"));
                    break;
                case HarvesterUnloading unloading when World.TryGet(unloading.Harvester, out var harvester) && harvester.Owner == Bot:
                    if (When("unload") is null)
                    {
                        _timeline.Add((tick, "unload"));
                    }

                    break;
                case CannotPlace refused when refused.Player == Bot:
                    _timeline.Add((tick, $"cannot place {Data.Buildings[refused.DefIndex].Id}: {refused.Problem}"));
                    break;
                case CannotDeploy refused when refused.Player == Bot:
                    _timeline.Add((tick, $"cannot deploy: {refused.Problem}"));
                    break;
            }
        }
    }
}
