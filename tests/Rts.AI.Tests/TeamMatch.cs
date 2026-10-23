using Rts.Data.Maps;
using Rts.Data.Tests;
using Rts.Simulation;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Matches;
using Rts.Simulation.Replays;
using Rts.Simulation.World;

namespace Rts.AI.Tests;

/// <summary>
/// A match of two teams on Four Corners by the real data (docs/implementation/M8_ai_difficulty.md, 4.8, step 11): players 1 and 2 —
/// the top corners — against 3 and 4, the bottom ones. Who is a bot and of what level is the test's; the bots are those of
/// <see cref="MatchBots.For"/>, one board for a team. The people do nothing but what the test gives them, by commands, so the replay
/// plays back.
/// </summary>
internal sealed class TeamMatch
{
    private readonly Dictionary<PlayerId, AiPlayer> _bots = [];
    private uint _personSequence;

    /// <param name="bots">The level of every seat a bot plays, by slot; the others are people.</param>
    public TeamMatch(IReadOnlyDictionary<int, AiDifficulty> bots, ulong seed = 5)
    {
        var map = RtsMapReader.Read(File.ReadAllText(Path.Combine(GameDataFiles.Folder, "maps", "four-corners.rtsmap")));
        var faction = GameDataFiles.Rules.DefaultFaction.Id;
        Setup = new MatchSetup(
            map.Id,
            seed,
            new MatchOptions(),
            [
                .. map.Starts.Select(start => new MatchPlayer(
                    start.Slot,
                    bots.ContainsKey(start.Slot) ? PlayerKind.Ai : PlayerKind.Human,
                    bots.GetValueOrDefault(start.Slot, AiDifficulty.Medium),
                    faction,
                    Color: start.Slot - 1,
                    Team: start.Slot <= 2 ? 1 : 2,
                    start.Slot,
                    start.Cell)),
            ],
            [.. map.Starts.Select(start => start.Cell)]);
        Simulation = new GameSimulation(SimulationSettings.ForMatch(Setup, map.ToMapGrid()), GameDataFiles.Rules);
        var create = MatchBots.For(Setup, GameDataFiles.Rules);
        foreach (var player in Setup.Players.Where(player => player.Kind == PlayerKind.Ai))
        {
            var bot = (AiPlayer)create(player);
            Simulation.AttachAi(bot);
            _bots[player.Id] = bot;
        }

        Recorder = new ReplayRecorder(Simulation);
    }

    public MatchSetup Setup { get; }

    public GameSimulation Simulation { get; }

    public ReplayRecorder Recorder { get; }

    public GameWorld World => Simulation.World;

    public AiPlayer Bot(int slot) => _bots[new PlayerId(slot)];

    public CellPos Start(int slot) => Setup.Players.First(player => player.Slot == slot).Start;

    public void Step()
    {
        Simulation.Step();
        Recorder.AfterStep();
    }

    /// <summary>
    /// Gives the person of <paramref name="slot"/> <paramref name="count"/> units of <paramref name="type"/> round <paramref name="near"/> on
    /// the next tick, by commands; a cell with no room is passed over.
    /// </summary>
    public List<CellPos> Give(int slot, UnitType type, int count, CellPos near)
    {
        var cells = new List<CellPos>();
        for (var ring = 2; cells.Count < count && ring < 10; ring++)
        {
            for (var dx = -ring; dx <= ring && cells.Count < count; dx += 2)
            {
                var cell = new CellPos(near.X + dx, near.Y + ring);
                if (World.Map.Contains(cell) && World.Map.CanEnter(cell, World.Rules.Unit(type).Locomotor) && !World.Map.HasBuilding(cell))
                {
                    Simulation.Enqueue(new SpawnTestUnitCommand(new PlayerId(slot), Simulation.CurrentTick, _personSequence++, type, cell.Center));
                    cells.Add(cell);
                }
            }
        }

        return cells;
    }

    /// <summary>
    /// Gives the person of <paramref name="slot"/> a ready base round their start, as <see cref="BotMatch.GivePersonABase"/> does: not by
    /// commands, so the replay of such a match does not play back.
    /// </summary>
    public void GiveBase(int slot)
    {
        var data = GameDataFiles.Rules.Database;
        var start = Start(slot);
        foreach (var id in new[] { "construction_yard", "reactor", "reactor", "barracks" })
        {
            var def = data.Buildings[data.IndexOfBuilding(id)];
            var placed = false;
            for (var ring = 0; ring < 12 && !placed; ring++)
            {
                for (var dy = -ring; dy <= ring && !placed; dy++)
                {
                    for (var dx = -ring; dx <= ring && !placed; dx++)
                    {
                        var cell = new CellPos(start.X + dx + 2, start.Y + dy + 2);
                        if (PlacementRules.Check(World, new PlayerId(slot), def, cell, needsBase: id != "construction_yard") == PlacementProblem.None)
                        {
                            World.CreateBuilding(new PlayerId(slot), def, cell, ready: true, null);
                            placed = true;
                        }
                    }
                }
            }
        }
    }

    /// <summary>The armed units of the person of <paramref name="slot"/> attack-move to <paramref name="cell"/> on the next tick.</summary>
    public void AttackMove(int slot, CellPos cell)
    {
        var units = World.Entities.OfType<Unit>()
            .Where(unit => unit.IsAlive && unit.Owner.Value == slot && unit.Archetype.Armed && unit.Harvester is null)
            .Select(unit => unit.Id);
        Simulation.Enqueue(new AttackMoveCommand(new PlayerId(slot), Simulation.CurrentTick, _personSequence++, [.. units], cell.Center));
    }
}
