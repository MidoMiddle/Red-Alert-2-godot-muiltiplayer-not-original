using System;
using System.Collections.Generic;
using Godot;
using Rts.Simulation.World;

namespace Rts.Client.Audio;

/// <summary>
/// What the player's units say (<c>voices</c> of <c>bindings.yaml</c>): a line when the player selects them, when they are sent to
/// move and when they are sent to attack (<see cref="SimulationHost.UnitsOrdered"/>). One unit answers for the group: the first of
/// them that has a line for it; the line is one of the files of its sound, picked by the <see cref="AudioManager"/>, from the middle
/// of the screen on the Voice bus. Placed in the main scene, next to the host and the input.
/// </summary>
public partial class UnitVoices : Node
{
    private const string BindingsPath = "res://data/audio/bindings.yaml";

    private readonly List<EntityId> _selected = new();
    private SimulationHost? _host;
    private PlayerInput? _input;
    private SoundBindings _bindings = SoundBindings.Empty;

    public override void _Ready()
    {
        // CombatSounds reads the same file and reports its mistakes.
        _bindings = SoundBindings.Parse(Godot.FileAccess.GetFileAsString(BindingsPath), BindingsPath, []);
        _host = GetNodeOrNull<SimulationHost>("../SimulationHost");
        _input = GetNodeOrNull<PlayerInput>("../PlayerInput");
        if (_host is not null)
        {
            _host.UnitsOrdered += OnOrdered;
        }
    }

    public override void _ExitTree()
    {
        if (_host is not null)
        {
            _host.UnitsOrdered -= OnOrdered;
        }
    }

    // A selection that gained a unit is a new choice of the player; one that only lost the dead is not.
    public override void _Process(double delta)
    {
        if (_input is null)
        {
            return;
        }

        var now = _input.Selection.Units;
        var gained = false;
        foreach (var id in now)
        {
            gained |= !_selected.Contains(id);
        }

        _selected.Clear();
        _selected.AddRange(now);
        if (gained)
        {
            Say(now, voice => voice.Select);
        }
    }

    private void OnOrdered(IReadOnlyList<EntityId> units, bool attack) => Say(units, voice => attack ? voice.Attack : voice.Move);

    /// <summary>The first of the player's living <paramref name="units"/> with a line for it says it.</summary>
    private void Say(IReadOnlyList<EntityId> units, Func<VoiceSounds, string> line)
    {
        if (_host is null || AudioManager.Instance is null)
        {
            return;
        }

        var world = _host.Simulation.World;
        foreach (var id in units)
        {
            if (world.TryGet(id, out var entity) && entity is Unit { IsAlive: true } unit && unit.Owner == SimulationHost.LocalPlayer
                && _bindings.Voices.TryGetValue(unit.Archetype.Id, out var voice) && line(voice) is { Length: > 0 } sound)
            {
                AudioManager.Instance.Play(sound);
                return;
            }
        }
    }
}
