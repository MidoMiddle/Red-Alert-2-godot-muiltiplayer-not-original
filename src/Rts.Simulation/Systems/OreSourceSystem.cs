using System.Collections.Generic;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.World;

namespace Rts.Simulation.Systems;

/// <summary>
/// Ore and gem sources erupt (docs/implementation/M3_base_economy.md, 4.12, step 16), right after <see cref="OreGrowthSystem"/>. A source
/// of id n erupts on the ticks t &gt; 0 with (t + n × <see cref="PhaseStep"/>) mod <c>source_interval</c> = 0, so the drills of a map do
/// not beat together. It takes, in index order, the cells within <c>source_radius</c> (dx² + dy² ≤ radius², not its own) that can take
/// its resource: an empty cell new ore may grow on (<see cref="OreGrowthSystem.CanTakeOre"/>) or a cell of its own kind below the last
/// stage; one roll of the world's generator picks one, which gets stage 0 or a stage more. With no such cell there is no roll and no
/// eruption, so a map without sources draws nothing from the generator. Ore of the other kind is left alone.
/// </summary>
public static class OreSourceSystem
{
    /// <summary>The largest <c>source_radius</c> the data may give.</summary>
    public const int MaxRadius = 8;

    /// <summary>How far apart in ticks the eruptions of sources with neighbouring ids fall.</summary>
    public const int PhaseStep = 37;

    public static void Update(GameWorld world, List<SimEvent>? events)
    {
        var map = world.Map;
        if (map.Sources.Count == 0 || world.CurrentTick == 0)
        {
            return;
        }

        var economy = world.Rules.Economy;
        List<CellPos>? candidates = null;
        foreach (var source in map.Sources)
        {
            if (!EruptsOn(world.CurrentTick, source.Id, economy.SourceInterval))
            {
                continue;
            }

            var kind = source.Kind == MapObjectKind.GemSource ? OreKind.Gems : OreKind.Ore;
            candidates ??= new List<CellPos>();
            candidates.Clear();
            Candidates(map, source.Cell, economy.SourceRadius, kind, candidates);
            if (candidates.Count == 0)
            {
                continue;
            }

            var cell = candidates[world.Random.NextInt(candidates.Count)];
            var ore = map.OreAt(cell);
            map.SetOre(cell, ore.IsEmpty ? new OreCell(kind, 0) : ore with { Stage = ore.Stage + 1 });
            events?.Add(new OreSourceErupted(world.CurrentTick, source.Id, cell, kind));
        }
    }

    /// <summary>Whether the source of id <paramref name="sourceId"/> erupts on <paramref name="tick"/>.</summary>
    public static bool EruptsOn(int tick, int sourceId, int interval) =>
        tick > 0 && (int)(((long)tick + (long)sourceId * PhaseStep) % interval) == 0;

    /// <summary>The cells within <paramref name="radius"/> of <paramref name="center"/> that can take a stage of <paramref name="kind"/>, in index order.</summary>
    public static void Candidates(MapGrid map, CellPos center, int radius, OreKind kind, List<CellPos> into)
    {
        for (var dy = -radius; dy <= radius; dy++)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                if ((dx == 0 && dy == 0) || dx * dx + dy * dy > radius * radius)
                {
                    continue;
                }

                var cell = new CellPos(center.X + dx, center.Y + dy);
                if (!map.Contains(cell))
                {
                    continue;
                }

                var ore = map.OreAt(cell);
                if (ore.Kind == kind ? ore.Stage < OreCell.MaxStage : OreGrowthSystem.CanTakeOre(map, cell))
                {
                    into.Add(cell);
                }
            }
        }
    }
}
