using System;
using System.Collections.Generic;
using Godot;

namespace Rts.Client.Units;

/// <summary>What a person of the map is seen doing; <see cref="HumanAnimator"/> picks the clip.</summary>
public enum HumanMotion
{
    Stand,
    Walk,
    Run,
    Cover,
}

/// <summary>
/// The clips of a person with the Mixamo skeleton (docs/art/mixamo.md): every look of tools/blender/units/civilians.py is skinned to the
/// same bones, so the one library of <c>assets/models/unit/human_clips</c> plays on all of them. Standing, an idle now and then gives way
/// to another (the calm <c>idle_0</c> most often); walking and running, the walk and the run the person was given at the start, played
/// as fast as the feet must go to keep to the ground; in cover, a crouch, with the clips of getting down and up again. The clip is the
/// look only: the simulation never hears of it.
/// </summary>
public sealed class HumanAnimator
{
    public const string ClipsScene = "res://assets/models/unit/human_clips/human_clips.glb";
    public const string TablePath = "res://data/visuals/human_clips.yaml";

    /// <summary>Of how fast its clip goes, the most and the least a walk or a run is played at to keep its feet on the ground.</summary>
    private const float MinPace = 0.5f;
    private const float MaxPace = 3f;

    private const float Blend = 0.2f;

    /// <summary>How much faster the clips of getting down and up are played: Mixamo's take seconds, a frightened passer-by does not.</summary>
    private const float CoverPace = 2f;

    private static HumanClipTable? _table;
    private static bool _loaded;
    private static Animation[]? _clips;
    private static NodePath? _clipsSkeleton;
    private static readonly Dictionary<string, AnimationLibrary> Libraries = new();
    private static int _made;

    private readonly AnimationPlayer _player;
    private readonly string _unitId;
    private readonly Random _random;
    private HumanClip _walk = null!;
    private HumanClip _run = null!;
    private HumanMotion _motion;
    private float _idleLeft;

    private HumanAnimator(AnimationPlayer player, string unitId)
    {
        _player = player;
        _unitId = unitId;
        _random = new Random(++_made);
        Restart();
    }

    /// <summary>
    /// The animator of a model with a skeleton, its clips added; null for a model without one, or when the clips are not there (an
    /// error, once): the person then stands still in its pose.
    /// </summary>
    public static HumanAnimator? Attach(Node3D model, string unitId)
    {
        if (FindSkeleton(model) is not { } skeleton || !Load())
        {
            return null;
        }

        // The player stands under the root of the model's scene, so the tracks name the skeleton as they do in the clips' scene.
        var root = skeleton.Owner as Node3D ?? model;
        var player = new AnimationPlayer { Name = "Clips" };
        root.AddChild(player);
        player.AddAnimationLibrary("", LibraryFor(root.GetPathTo(skeleton)));
        return new HumanAnimator(player, unitId);
    }

    /// <summary>A new person in the view: its walk and its run picked again, standing.</summary>
    public void Restart()
    {
        _walk = Pick("walk");
        _run = Pick("run");
        _motion = HumanMotion.Stand;
        PlayIdle(true);
    }

    /// <summary>Plays what the person does: <paramref name="speed"/> is how fast it is seen to go (units a second).</summary>
    public void Step(HumanMotion motion, float speed, float delta)
    {
        if (motion != _motion)
        {
            Change(_motion, motion);
            _motion = motion;
        }

        switch (motion)
        {
            case HumanMotion.Walk:
                _player.SpeedScale = Pace(_walk, speed);
                break;
            case HumanMotion.Run:
                _player.SpeedScale = _player.CurrentAnimation.ToString() == _run.Name ? Pace(_run, speed) : 1f;
                break;
            case HumanMotion.Stand:
                _player.SpeedScale = 1f;
                _idleLeft -= delta;
                if (_idleLeft <= 0f && _player.CurrentAnimation.ToString().StartsWith("idle_", StringComparison.Ordinal))
                {
                    PlayIdle(false);
                }

                break;
            default:
                _player.SpeedScale = 1f;
                break;
        }
    }

    /// <summary>Plays a fall; returns how long it takes.</summary>
    public float Die()
    {
        var falls = _table!.Family("die", _unitId);
        if (falls.IsEmpty)
        {
            return 0f;
        }

        var fall = falls[_random.Next(falls.Length)];
        _player.SpeedScale = 1f;
        _player.ClearQueue();
        _player.Play(fall.Name, 0.1);
        return fall.Length;
    }

    private void Change(HumanMotion from, HumanMotion to)
    {
        _player.ClearQueue();
        switch (to)
        {
            case HumanMotion.Cover:
                Then("crouch_down", "crouch", CoverPace);
                break;
            case HumanMotion.Run when from == HumanMotion.Cover:
                Then("crouch_sprint", _run.Name, 1f);
                break;
            case HumanMotion.Run:
                _player.Play(_run.Name, Blend);
                break;
            case HumanMotion.Walk:
                _player.Play(_walk.Name, Blend);
                break;
            default:
                if (from == HumanMotion.Cover && Has("crouch_up"))
                {
                    // Up again, then the calm idle once through before another is picked.
                    var calm = Calm();
                    _player.Play("crouch_up", Blend, CoverPace);
                    _player.Queue(calm.Name);
                    _idleLeft = (_table!.Clips["crouch_up"].Length / CoverPace) + calm.Length;
                }
                else
                {
                    PlayIdle(true);
                }

                break;
        }
    }

    // A clip of getting from one pose to another, then the clip that holds the new one.
    private void Then(string first, string next, float pace)
    {
        if (Has(first))
        {
            _player.Play(first, Blend, pace);
            _player.Queue(next);
        }
        else
        {
            _player.Play(next, Blend);
        }
    }

    private void PlayIdle(bool calm)
    {
        var idle = calm ? Calm() : Idle();
        _player.Play(idle.Name, calm ? 0f : 0.4);
        // Played once or twice through before the next is picked.
        _idleLeft = idle.Length * (1 + _random.Next(2));
    }

    // The calm idle, idle_0, half the time; another idle else.
    private HumanClip Idle() => _random.Next(2) == 0 ? Calm() : Pick("idle");

    private HumanClip Calm() => _table!.Clips.TryGetValue("idle_0", out var calm) ? calm : Pick("idle");

    private HumanClip Pick(string family)
    {
        var clips = _table!.Family(family, _unitId);
        return clips.IsEmpty ? Calm() : clips[_random.Next(clips.Length)];
    }

    private static bool Has(string name) => _table!.Clips.ContainsKey(name);

    private static float Pace(HumanClip clip, float speed) =>
        clip.Stride is { } stride ? Mathf.Clamp(speed / stride, MinPace, MaxPace) : 1f;

    private static Skeleton3D? FindSkeleton(Node node)
    {
        if (node is Skeleton3D skeleton)
        {
            return skeleton;
        }

        foreach (var child in node.GetChildren())
        {
            if (FindSkeleton(child) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    // The table and the clips, read once.
    private static bool Load()
    {
        if (_loaded)
        {
            return _clips is not null;
        }

        _loaded = true;
        try
        {
            _table = HumanClipTable.Read(FileAccess.GetFileAsString(TablePath));
        }
        catch (HumanClipTableException e)
        {
            GD.PushError($"[clips] {TablePath}:{e.Message}: the people stand still");
            return false;
        }

        if (!ResourceLoader.Exists(ClipsScene) || GD.Load<PackedScene>(ClipsScene)?.Instantiate<Node3D>() is not { } scene)
        {
            GD.PushError($"[clips] no {ClipsScene}: the people stand still");
            return false;
        }

        var player = FindPlayer(scene);
        var skeleton = FindSkeleton(scene);
        if (player is null || skeleton is null)
        {
            GD.PushError($"[clips] {ClipsScene} has no clips on a skeleton: the people stand still");
            scene.Free();
            return false;
        }

        var clips = new List<Animation>();
        foreach (var name in player.GetAnimationList())
        {
            if (_table.Clips.TryGetValue(name, out var clip))
            {
                var animation = (Animation)player.GetAnimation(name).Duplicate();
                animation.ResourceName = name;
                animation.LoopMode = clip.Loop ? Animation.LoopModeEnum.Linear : Animation.LoopModeEnum.None;
                clips.Add(animation);
            }
        }

        foreach (var name in _table.Clips.Keys)
        {
            if (!player.HasAnimation(name))
            {
                GD.PushError($"[clips] {ClipsScene} has no clip {name} of the table");
            }
        }

        _clipsSkeleton = scene.GetPathTo(skeleton);
        _clips = [.. clips];
        scene.Free();
        return true;
    }

    private static AnimationPlayer? FindPlayer(Node node)
    {
        if (node is AnimationPlayer player)
        {
            return player;
        }

        foreach (var child in node.GetChildren())
        {
            if (FindPlayer(child) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    // The clips with their tracks on the skeleton at this path from the root of the model: the same library for every model laid out
    // as the clips' scene is, a copy with its tracks moved for another.
    private static AnimationLibrary LibraryFor(NodePath skeleton)
    {
        var key = skeleton.ToString();
        if (Libraries.TryGetValue(key, out var library))
        {
            return library;
        }

        library = new AnimationLibrary();
        var moved = key != _clipsSkeleton!.ToString();
        foreach (var clip in _clips!)
        {
            var animation = moved ? (Animation)clip.Duplicate() : clip;
            if (moved)
            {
                for (var t = 0; t < animation.GetTrackCount(); t++)
                {
                    var bone = animation.TrackGetPath(t).GetConcatenatedSubNames();
                    animation.TrackSetPath(t, new NodePath($"{key}:{bone}"));
                }
            }

            library.AddAnimation(clip.ResourceName, animation);
        }

        Libraries[key] = library;
        return library;
    }
}
