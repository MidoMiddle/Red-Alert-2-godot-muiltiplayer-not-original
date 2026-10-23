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

    /// <param name="bothBots">
    /// The person's seat played by a bot too — bot against bot, the arena of step 14. A match needs a person, so the seat stays a
    /// person's: its commands are played back from a replay as they were, only the bot of the other seat is run again.
    /// </param>
    /// <param name="settings">The settings of the bots, «Medium» of the data by default (the tuning of step 14).</param>
    /// <param name="credits">The credits every player starts with; the default of the match when null.</param>
    /// <param name="noOre">The map without its ore: a match with no income (section 6, «a bot without money»).</param>
    public BotMatch(
        string mapId = "frozen-crossing", ulong seed = 3, bool shortGame = false, bool bothBots = false, AiSettings? settings = null,
        int? credits = null, bool noOre = false)
    {
        var map = RtsMapReader.Read(File.ReadAllText(Path.Combine(GameDataFiles.Folder, "maps", mapId + ".rtsmap")));
        Setup = MatchSetups.OneOnOne(map, GameDataFiles.Rules.DefaultFaction.Id, seed);
        Setup = Setup with { Options = Setup.Options with { ShortGame = shortGame, StartingCredits = credits ?? Setup.Options.StartingCredits } };
        var grid = map.ToMapGrid();
        if (noOre)
        {
            for (var y = 0; y < grid.Height; y++)
            {
                for (var x = 0; x < grid.Width; x++)
                {
                    grid.SetOre(new Rts.Simulation.Geometry.CellPos(x, y), Rts.Simulation.Map.OreCell.Empty);
                }
            }
        }

        Simulation = new GameSimulation(SimulationSettings.ForMatch(Setup, grid), GameDataFiles.Rules);
        foreach (var player in Setup.Players.Where(player => player.Kind == PlayerKind.Ai || bothBots))
        {
            var bot = settings is null && player.Kind == PlayerKind.Ai
                ? (AiPlayer)MatchBots.Create(Setup, player, GameDataFiles.Rules)
                : MatchBots.Create(Setup, player, settings ?? AiSettings.Medium);
            Simulation.AttachAi(bot);
            if (player.Id == Bot)
            {
                Brain = bot;
            }
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
    /// Gives the person <paramref name="count"/> units of <paramref name="type"/> round <paramref name="near"/> — their start by default —
    /// on the next tick, by commands of the person, so that a replay has them too. A cell with no room is passed over.
    /// </summary>
    public void GivePerson(UnitType type, int count, Rts.Simulation.Geometry.CellPos? near = null)
    {
        var person = Setup.Players.First(player => player.Kind == PlayerKind.Human);
        var at = near ?? person.Start;
        var given = 0;
        for (var ring = 2; given < count && ring < 8; ring++)
        {
            for (var dx = -ring; dx <= ring && given < count; dx += 2)
            {
                var cell = new Rts.Simulation.Geometry.CellPos(at.X + dx, at.Y + ring);
                if (World.Map.Contains(cell) && World.Map.CanEnter(cell, World.Rules.Unit(type).Locomotor) && !World.Map.HasBuilding(cell))
                {
                    Simulation.Enqueue(new Rts.Simulation.Commands.SpawnTestUnitCommand(person.Id, Simulation.CurrentTick, _personSequence++, type, cell.Center));
                    given++;
                }
            }
        }
    }

    /// <summary>
    /// Gives the person a ready base round their start, as the world stands: a yard, two reactors, barracks and a tesla coil, each on
    /// the nearest cell the placement rules allow — the passive person «with a base and defense» of section 6. Not by commands: a replay
    /// of such a match does not play back.
    /// </summary>
    public void GivePersonABase()
    {
        var person = Setup.Players.First(player => player.Kind == PlayerKind.Human);
        foreach (var id in new[] { "construction_yard", "reactor", "reactor", "barracks", "tesla_coil" })
        {
            var def = Data.Buildings[Data.IndexOfBuilding(id)];
            var placed = false;
            for (var ring = 0; ring < 12 && !placed; ring++)
            {
                for (var dy = -ring; dy <= ring && !placed; dy++)
                {
                    for (var dx = -ring; dx <= ring && !placed; dx++)
                    {
                        var cell = new Rts.Simulation.Geometry.CellPos(person.Start.X + dx + 2, person.Start.Y + dy + 2);
                        if (Rts.Simulation.World.PlacementRules.Check(World, person.Id, def, cell, needsBase: id != "construction_yard")
                            == Rts.Simulation.World.PlacementProblem.None)
                        {
                            World.CreateBuilding(person.Id, def, cell, ready: true, null);
                            placed = true;
                        }
                    }
                }
            }
        }
    }

    /// <summary>The person's units of <paramref name="type"/> attack-move to <paramref name="cell"/> on the next tick.</summary>
    public void PersonAttacks(UnitType type, Rts.Simulation.Geometry.CellPos cell)
    {
        var person = Setup.Players.First(player => player.Kind == PlayerKind.Human);
        var units = World.Entities.OfType<Unit>().Where(unit => unit.IsAlive && unit.Owner == person.Id && unit.Type == type).Select(unit => unit.Id);
        Simulation.Enqueue(new Rts.Simulation.Commands.AttackMoveCommand(
            person.Id, Simulation.CurrentTick, _personSequence++, [.. units], cell.Center));
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
