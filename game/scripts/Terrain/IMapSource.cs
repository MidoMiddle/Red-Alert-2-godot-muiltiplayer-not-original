using System;
using Godot;
using Rts.Simulation.Map;

namespace Rts.Client.Terrain;

/// <summary>
/// A scene that shows a map without a match: the map editor (docs/implementation/M7_maps.md, 4.13). The terrain and the
/// camera take the map from it as they take it from the <see cref="SimulationHost"/> of a match.
/// </summary>
public interface IMapSource
{
    /// <summary>The map to show now; a new object or a new <see cref="MapGrid.Version"/> means it changed.</summary>
    MapGrid CurrentMap { get; }

    /// <summary>The look of the map (climate, time of day, texture layer); a new object means it changed.</summary>
    Climates.MapLook CurrentLook { get; }
}

public static class MapSources
{
    /// <summary>
    /// The map of the scene <paramref name="node"/> belongs to: the match of a sibling <c>SimulationHost</c>, otherwise the
    /// nearest <see cref="IMapSource"/> above it. Read it every time: a new match or a new map replaces the object.
    /// </summary>
    public static Func<MapGrid> Find(Node node)
    {
        if (node.GetNodeOrNull<SimulationHost>("../SimulationHost") is { } host)
        {
            return () => host.Simulation.World.Map;
        }

        for (var parent = node.GetParent(); parent is not null; parent = parent.GetParent())
        {
            if (parent is IMapSource source)
            {
                return () => source.CurrentMap;
            }
        }

        throw new InvalidOperationException($"{node.Name} has no map: no SimulationHost beside it and no map source above it.");
    }

    /// <summary>The look of the map of the scene <paramref name="node"/> belongs to, found as <see cref="Find"/> finds the map.</summary>
    public static Func<Climates.MapLook> FindLook(Node node)
    {
        if (node.GetNodeOrNull<SimulationHost>("../SimulationHost") is { } host)
        {
            return () => host.Look;
        }

        for (var parent = node.GetParent(); parent is not null; parent = parent.GetParent())
        {
            if (parent is IMapSource source)
            {
                return () => source.CurrentLook;
            }
        }

        throw new InvalidOperationException($"{node.Name} has no map: no SimulationHost beside it and no map source above it.");
    }
}
