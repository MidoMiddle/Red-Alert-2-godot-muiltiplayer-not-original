using System;
using System.Collections.Generic;
using Godot;
using Rts.Client.Terrain;
using Rts.Client.Units;
using Rts.Simulation.World;

namespace Rts.Client.Audio;

/// <summary>
/// The loops of the fliers (<c>movement</c> of <c>bindings.yaml</c>): the rotor of the helicopter, the engine of a jet, playing as long
/// as the flier is in the air and on the screen, and following it. As it sets down the loop dies away and runs slower, as a rotor
/// spinning down; on the ground it is silent. Only fliers the local player sees sound, as with the shots (<see cref="CombatSounds"/>);
/// of each sound only the loudest <see cref="SoundDefinition.MaxConcurrent"/> play. Placed in the main scene, next to the host.
/// </summary>
public partial class MovementSounds : Node
{
    private const string BindingsPath = "res://data/audio/bindings.yaml";

    /// <summary>Below this share of its flying height a flier is on the ground, its engine off.</summary>
    private const float RunningShare = 0.05f;

    /// <summary>The pitch of a loop on the ground and in the air: a rotor spinning up as it rises.</summary>
    private const float GroundPitch = 0.7f;

    private readonly Dictionary<EntityId, AudioStreamPlayer2D> _loops = new();
    private readonly List<Wanted> _wanted = new();
    private readonly Dictionary<string, int> _playing = new();
    private readonly HashSet<EntityId> _kept = new();
    private readonly List<EntityId> _gone = new();
    private SimulationHost? _host;
    private SoundBindings _bindings = SoundBindings.Empty;

    private readonly record struct Wanted(EntityId Id, string Sound, Vector2 At, float GainDb, float Level);

    public override void _Ready()
    {
        // CombatSounds reads the same file and reports its mistakes.
        _bindings = SoundBindings.Parse(Godot.FileAccess.GetFileAsString(BindingsPath), BindingsPath, []);
        _host = GetNodeOrNull<SimulationHost>("../SimulationHost");
    }

    public override void _ExitTree()
    {
        foreach (var player in _loops.Values)
        {
            player.QueueFree();
        }

        _loops.Clear();
    }

    public override void _Process(double delta)
    {
        _wanted.Clear();
        var camera = GetViewport().GetCamera3D();
        if (_host is not null && AudioManager.Instance is not null && WorldCoords.Terrain is not null && camera is not null)
        {
            Gather(_host.Simulation.World, camera);
        }

        // The loudest first: when there are more fliers of a sound than it may play at once, the far ones are left out.
        _wanted.Sort((a, b) => b.GainDb.CompareTo(a.GainDb));
        _playing.Clear();
        _kept.Clear();
        foreach (var wanted in _wanted)
        {
            if (Sound(wanted))
            {
                _kept.Add(wanted.Id);
            }
        }

        _gone.Clear();
        foreach (var id in _loops.Keys)
        {
            if (!_kept.Contains(id))
            {
                _gone.Add(id);
            }
        }

        foreach (var id in _gone)
        {
            _loops[id].QueueFree();
            _loops.Remove(id);
        }
    }

    /// <summary>Every flier with a loop that is in the air, seen and on the screen, with where it sounds from and how loud.</summary>
    private void Gather(GameWorld world, Camera3D camera)
    {
        var size = GetViewport().GetVisibleRect().Size;
        foreach (var entity in world.Entities)
        {
            if (entity is not Unit { IsAlive: true } unit || !_bindings.Movement.TryGetValue(unit.Archetype.Id, out var sound)
                || !FogOfWar.Shows(world, unit))
            {
                continue;
            }

            var flying = UnitAltitude.Of(unit.Archetype);
            var height = UnitAltitude.Of(unit);
            var level = flying > 0f ? Math.Clamp(height / flying, 0f, 1f) : 1f;
            var point = WorldCoords.ToGodotOnUnit(unit, unit.Position, height);
            if (level < RunningShare || camera.IsPositionBehind(point))
            {
                continue;
            }

            var at = camera.UnprojectPosition(point);
            var placement = ScreenPlacement.Place(at.X, at.Y, size.X, size.Y);
            if (placement.Audible)
            {
                _wanted.Add(new Wanted(unit.Id, sound, at, placement.GainDb + Mathf.LinearToDb(level), level));
            }
        }
    }

    /// <summary>Starts or moves the loop of a flier; false when its sound already plays as often as it may, or cannot play.</summary>
    private bool Sound(Wanted wanted)
    {
        if (AudioManager.Instance?.Definition(wanted.Sound) is not { } definition
            || _playing.GetValueOrDefault(wanted.Sound) >= definition.MaxConcurrent)
        {
            return false;
        }

        if (!_loops.TryGetValue(wanted.Id, out var player))
        {
            player = AudioManager.Instance.NewLoopPlayer(wanted.Sound, out _);
            if (player is null)
            {
                return false;
            }

            _loops[wanted.Id] = player;
        }

        _playing[wanted.Sound] = _playing.GetValueOrDefault(wanted.Sound) + 1;
        player.Position = wanted.At;
        player.VolumeDb = definition.VolumeDb + wanted.GainDb;
        // A hair of pitch of its own for each flier, so that two of them do not ring as one.
        player.PitchScale = (GroundPitch + ((1f - GroundPitch) * wanted.Level)) * (1f + (((wanted.Id.Value % 7) - 3) * 0.01f));
        if (!player.Playing)
        {
            // From a point of its own in the loop, for the same reason.
            var length = (float)(player.Stream?.GetLength() ?? 0.0);
            player.Play(length * ((wanted.Id.Value * 0.37f) % 1f));
        }

        return true;
    }
}
