using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Rts.Client.Audio;

/// <summary>
/// The sound of the game (docs/implementation/M6_menus_skirmish.md, 4.8), the <c>AudioManager</c> autoload. A sound is played by its
/// name from <c>sounds.yaml</c>: a random file and a random pitch of its definition, on its bus, under its priority and its limits
/// (<see cref="VoicePool"/>). A sound with a place in the world is placed on the screen (<see cref="ScreenPlacement"/>). The buses are
/// those of <c>default_bus_layout.tres</c>: Master, Music, SFX, Voice, UI, and EVA under Voice. A missing file or bus is reported once
/// and the sound is skipped, so the game plays on until the sound files arrive (step 14).
/// </summary>
public partial class AudioManager : Node
{
    /// <summary>Players in the pool: the 48 of the plan.</summary>
    public const int PlayerCount = 48;

    private const string DefinitionsPath = "res://data/audio/sounds.yaml";
    private const string FilesFolder = "res://assets/audio/";
    private const string MasterBus = "Master";
    private const string PanningSetting = "audio/general/2d_panning_strength";
    private const float DefaultPanningStrength = 0.5f;

    /// <summary>Godot cuts a 2D sound beyond this distance from the listener; far beyond any screen, the placement decides instead.</summary>
    private const float NoDistanceLimit = 1000000f;

    private const string HoverSound = "snd_ui_hover";
    private const string ClickSound = "snd_ui_click";

    private readonly Random _random = new();
    private readonly HashSet<string> _reported = new();
    private readonly Dictionary<string, AudioStream[]> _streams = new();
    private readonly VoicePool _pool = new(PlayerCount);
    private readonly AudioStreamPlayer2D[] _players = new AudioStreamPlayer2D[PlayerCount];
    private IReadOnlyDictionary<string, SoundDefinition> _sounds = new Dictionary<string, SoundDefinition>();

    /// <summary>The running manager. Set when the autoload is ready.</summary>
    public static AudioManager? Instance { get; private set; }

    public override void _Ready()
    {
        Instance = this;

        // 2D sounds are panned by their offset from the listener; with no listener of its own the viewport uses the centre of the
        // screen, so the viewport must count as a listener. The pan of a sound at the edge of the screen is set by the panning strength.
        GetViewport().SetAsAudioListener2D(true);
        var panning = ScreenPlacement.PanningStrength(ProjectSettings.GetSetting(PanningSetting, DefaultPanningStrength).AsSingle());
        for (var slot = 0; slot < PlayerCount; slot++)
        {
            var index = slot;
            var player = new AudioStreamPlayer2D
            {
                Attenuation = 0f,
                MaxDistance = NoDistanceLimit,
                PanningStrength = panning,
            };
            player.Finished += () =>
            {
                // A player that was taken again has started a new sound, which the pool must keep counting.
                if (!player.Playing)
                {
                    _pool.Release(index);
                }
            };
            AddChild(player);
            _players[slot] = player;
        }

        var errors = new List<string>();
        _sounds = SoundLibrary.Parse(Godot.FileAccess.GetFileAsString(DefinitionsPath), DefinitionsPath, errors);
        foreach (var error in errors)
        {
            GD.PushError($"[audio] {error}");
        }

        var buses = Enumerable.Range(0, AudioServer.GetBusCount()).Select(bus => AudioServer.GetBusName(bus).ToString());
        GD.Print($"[audio] {PlayerCount} players, {_sounds.Count} sounds, buses {string.Join(", ", buses)}");
        GetTree().NodeAdded += OnNodeAdded;
    }

    /// <summary>Plays a sound that has no place of its own (the interface, the staff, the voices): it sounds from the centre of the screen.</summary>
    /// <returns>False when it was not played: unknown, silent, or no player was free.</returns>
    public bool Play(string name) => Start(name, null);

    /// <summary>Plays a sound at a point of the world. It is placed on the screen; behind the camera it is not played.</summary>
    /// <returns>False when it was not played.</returns>
    public bool PlayAt(string name, Vector3 point)
    {
        var camera = GetViewport().GetCamera3D();
        if (camera is null || camera.IsPositionBehind(point))
        {
            return false;
        }

        return Start(name, camera.UnprojectPosition(point));
    }

    private bool Start(string name, Vector2? screenPoint)
    {
        if (!_sounds.TryGetValue(name, out var definition))
        {
            Report($"there is no sound '{name}' in {DefinitionsPath}");
            return false;
        }

        var size = GetViewport().GetVisibleRect().Size;
        var at = screenPoint ?? size / 2f;
        var gainDb = 0f;
        if (definition.Positional)
        {
            var placement = ScreenPlacement.Place(at.X, at.Y, size.X, size.Y);
            if (!placement.Audible)
            {
                return false;
            }

            gainDb = placement.GainDb;
        }

        var stream = Pick(definition);
        if (stream is null)
        {
            return false;
        }

        var request = new VoiceRequest(definition.Name, definition.Priority, Mathf.DbToLinear(definition.VolumeDb + gainDb), definition.MaxConcurrent, definition.CooldownMs);
        if (!_pool.TryAcquire(request, (long)Time.GetTicksMsec(), out var grant))
        {
            return false;
        }

        var player = _players[grant.Slot];
        player.Stop();
        player.Stream = stream;
        player.Bus = BusOf(definition.Bus);
        player.VolumeDb = definition.VolumeDb + gainDb;
        player.PitchScale = (float)(definition.PitchMin + _random.NextDouble() * (definition.PitchMax - definition.PitchMin));
        player.Position = at;
        player.Play();
        return true;
    }

    /// <summary>A random file of the definition, loaded on first use. Files that are not there yet are reported once.</summary>
    private AudioStream? Pick(SoundDefinition definition)
    {
        if (!_streams.TryGetValue(definition.Name, out var streams))
        {
            streams = definition.Files
                .Select(file => FilesFolder + file)
                .Where(path => ResourceLoader.Exists(path))
                .Select(path => ResourceLoader.Load<AudioStream>(path))
                .ToArray();
            var missing = definition.Files.Count - streams.Length;
            if (missing > 0)
            {
                Report($"'{definition.Name}' has {missing} of its {definition.Files.Count} files missing in {FilesFolder}; it stays silent");
            }

            _streams[definition.Name] = streams;
        }

        return streams.Length == 0 ? null : streams[_random.Next(streams.Length)];
    }

    private string BusOf(string bus)
    {
        if (AudioServer.GetBusIndex(bus) >= 0)
        {
            return bus;
        }

        Report($"there is no bus '{bus}': its sounds play on {MasterBus}");
        return MasterBus;
    }

    /// <summary>The buttons sound when they are pointed at and pressed, on the UI bus, whatever screen they are on.</summary>
    private void OnNodeAdded(Node node)
    {
        if (node is not BaseButton button)
        {
            return;
        }

        button.Pressed += () => Play(ClickSound);
        button.MouseEntered += () =>
        {
            if (!button.Disabled)
            {
                Play(HoverSound);
            }
        };
    }

    /// <summary>Prints a problem once, not on every sound that hits it.</summary>
    private void Report(string message)
    {
        if (_reported.Add(message))
        {
            GD.PushWarning($"[audio] {message}");
        }
    }
}
