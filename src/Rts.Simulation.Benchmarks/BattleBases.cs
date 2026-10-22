using Rts.Simulation.Geometry;
using Rts.Simulation.World;

namespace Rts.Simulation.Benchmarks;

/// <summary>
/// The bases of <see cref="BattleScenario"/>: ready buildings put straight into the world, which only the benchmarks and the tests
/// may do (<c>InternalsVisibleTo</c>); kept apart so that the client can link the scenario without them.
/// </summary>
public static class BattleBases
{
    /// <summary>Both bases, blue then red.</summary>
    public static void Build(GameWorld world)
    {
        Base(world, BattleScenario.Blue, BattleScenario.BlueLeft);
        Base(world, BattleScenario.Red, BattleScenario.RedLeft);
    }

    /// <summary>A ready base north of the army: a yard, two reactors, a tesla coil in front, two sentry bunkers beside it.</summary>
    private static void Base(GameWorld world, PlayerId player, int left)
    {
        var rules = world.Rules.Database;
        var top = BattleScenario.Top;
        void Place(string id, int x, int y) => world.CreateBuilding(player, rules.Buildings[rules.IndexOfBuilding(id)], new CellPos(x, y), ready: true, null);
        Place("construction_yard", left + 2, top - 14);
        Place("reactor", left + 6, top - 14);
        Place("reactor", left + 9, top - 14);
        Place("tesla_coil", left + 6, top - 10);
        Place("sentry_bunker", left + 3, top - 9);
        Place("sentry_bunker", left + 10, top - 9);
    }
}
