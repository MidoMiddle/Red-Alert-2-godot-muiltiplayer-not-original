using System.Collections.Immutable;
using Rts.AI.View;
using Rts.Simulation;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Systems;
using Rts.Simulation.Tests;
using Rts.Simulation.World;

namespace Rts.AI.Tests;

/// <summary>
/// A world on clear land of 64 × 64 cells seen by the bot of <see cref="Me"/>: units and buildings are put in place and moved by hand, then
/// <see cref="Look"/> brings the sight of the players, the view and the memory of the bot up to date, as a turn of the bot does.
/// </summary>
internal sealed class Scene
{
    public static readonly PlayerId Me = new(1);
    public static readonly PlayerId Enemy = new(2);

    /// <summary>Four starts in the corners; the bot's own is the first.</summary>
    public static readonly ImmutableArray<CellPos> Corners = [new(8, 8), new(56, 56), new(56, 8), new(8, 56)];

    public Scene(ImmutableArray<CellPos> mapStarts = default)
    {
        Simulation = new GameSimulation(new SimulationSettings(1, new MapGrid(64, 64)), TestRules.Standard);
        var starts = mapStarts.IsDefault ? Corners : mapStarts;
        View = new AiWorldView(World, Me, starts[0], starts);
        Memory = new AiMemory();
    }

    public GameSimulation Simulation { get; }

    public GameWorld World => Simulation.World;

    public AiWorldView View { get; }

    public AiMemory Memory { get; }

    /// <summary>A turn for a module of the bot, after the last <see cref="Look"/>: its commands go to the next tick.</summary>
    public AiContext Context()
    {
        var context = new AiContext(View, Memory);
        context.Begin(new Rts.Simulation.Ai.AiTurn(Simulation, Me) { Tick = View.Tick });
        return context;
    }

    public Unit Spawn(PlayerId owner, UnitType type, int x, int y) => World.SpawnUnit(owner, type, new CellPos(x, y).Center);

    public Building Build(PlayerId owner, string id, int x, int y) =>
        World.CreateBuilding(owner, TestRules.BuildingDef(id), new CellPos(x, y), ready: true, null);

    public static void Move(Unit unit, int x, int y) => unit.Position = new CellPos(x, y).Center;

    /// <summary>The turn of the bot after <paramref name="tick"/>.</summary>
    public void Look(int tick = 0)
    {
        VisibilitySystem.Update(World);
        View.Refresh(World, tick);
        Memory.Update(View);
    }
}
