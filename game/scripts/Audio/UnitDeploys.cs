using System.Collections.Generic;
using Godot;
using Rts.Client.Terrain;
using Rts.Simulation.World;

namespace Rts.Client.Audio;

/// <summary>
/// The sound of a unit deploying where it stands and getting up again (<c>deploys</c> of <c>bindings.yaml</c>): the Rifleman digging
/// in behind its sandbags. The simulation raises no event for it, only turns <see cref="Unit.Deployed"/>, so this watches the units
/// that have such a sound and plays it where one turns, if the local player sees it. Placed in the main scene, next to the host.
/// </summary>
public partial class UnitDeploys : Node
{
    private const string BindingsPath = "res://data/audio/bindings.yaml";

    /// <summary>Height above the ground of the sound: a soldier's middle.</summary>
    private const float SoundHeight = 0.3f;

    private readonly Dictionary<EntityId, bool> _deployed = new();
    private readonly HashSet<EntityId> _seen = new();
    private readonly List<EntityId> _gone = new();
    private SimulationHost? _host;
    private SoundBindings _bindings = SoundBindings.Empty;

    public override void _Ready()
    {
        // CombatSounds reads the same file and reports its mistakes.
        _bindings = SoundBindings.Parse(Godot.FileAccess.GetFileAsString(BindingsPath), BindingsPath, []);
        _host = GetNodeOrNull<SimulationHost>("../SimulationHost");
    }

    public override void _Process(double delta)
    {
        if (_host is null || _bindings.Deploys.Count == 0)
        {
            return;
        }

        var world = _host.Simulation.World;
        _seen.Clear();
        foreach (var entity in world.Entities)
        {
            if (entity is not Unit { IsAlive: true } unit || !_bindings.Deploys.TryGetValue(unit.Archetype.Id, out var sounds))
            {
                continue;
            }

            _seen.Add(unit.Id);
            // A unit new to the watch is only noted: it may have come deployed (a loaded match, a replay jumped ahead).
            if (_deployed.TryGetValue(unit.Id, out var was) && was != unit.Deployed && FogOfWar.Shows(world, unit)
                && WorldCoords.Terrain is not null && (unit.Deployed ? sounds.On : sounds.Off) is { Length: > 0 } sound
                && AudioManager.Instance is not null)
            {
                AudioManager.Instance.PlayAt(sound, WorldCoords.ToGodotOnGround(unit.Position, SoundHeight));
            }

            _deployed[unit.Id] = unit.Deployed;
        }

        _gone.Clear();
        foreach (var id in _deployed.Keys)
        {
            if (!_seen.Contains(id))
            {
                _gone.Add(id);
            }
        }

        foreach (var id in _gone)
        {
            _deployed.Remove(id);
        }
    }
}
