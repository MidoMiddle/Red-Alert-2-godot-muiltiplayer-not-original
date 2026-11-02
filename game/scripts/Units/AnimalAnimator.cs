using System;
using Godot;

namespace Rts.Client.Units;

/// <summary>
/// The clips of an animal of the map (tools/blender/families/civ_animals.py): a skeleton of its own, not Mixamo's, its clips in its GLB —
/// <c>idle</c>, <c>graze</c>, <c>walk</c>, <c>run</c> and <c>die</c>. Standing, it grazes a while or looks about, a few times through
/// each before it picks again; walking it walks, fleeing it runs; it does not take cover (it stands). The clip is the look only: the
/// simulation never hears of it.
/// </summary>
public sealed class AnimalAnimator
{
    private const float Blend = 0.25f;

    private static readonly string[] Looped = ["idle", "graze", "walk", "run"];
    private static int _made;

    private readonly AnimationPlayer _player;
    private readonly Random _random;
    private HumanMotion _motion;
    private float _restLeft;

    private AnimalAnimator(AnimationPlayer player)
    {
        _player = player;
        _random = new Random(++_made);
        Restart();
    }

    /// <summary>The animator of a model that brings its own clips (a <c>walk</c> among them); null for any other.</summary>
    public static AnimalAnimator? Attach(Node3D model)
    {
        if (FindPlayer(model) is not { } player || !player.HasAnimation("walk"))
        {
            return null;
        }

        foreach (var name in Looped)
        {
            if (player.HasAnimation(name))
            {
                player.GetAnimation(name).LoopMode = Animation.LoopModeEnum.Linear;
            }
        }

        return new AnimalAnimator(player);
    }

    /// <summary>A new animal in the view: standing.</summary>
    public void Restart()
    {
        _motion = HumanMotion.Stand;
        Rest();
    }

    /// <summary>Plays what the animal does: <paramref name="motion"/> as a person's, cover being standing.</summary>
    public void Step(HumanMotion motion, float delta)
    {
        if (motion == HumanMotion.Cover)
        {
            motion = HumanMotion.Stand;
        }

        if (motion != _motion)
        {
            _motion = motion;
            if (motion == HumanMotion.Stand)
            {
                Rest();
            }
            else
            {
                _player.Play(motion == HumanMotion.Run ? "run" : "walk", Blend);
            }
        }
        else if (motion == HumanMotion.Stand && (_restLeft -= delta) <= 0f)
        {
            Rest();
        }
    }

    /// <summary>Plays the fall on its side; returns how long it takes.</summary>
    public float Die()
    {
        if (!_player.HasAnimation("die"))
        {
            return 0f;
        }

        _player.Play("die", 0.1);
        return (float)_player.GetAnimation("die").Length;
    }

    // Grazing two times in three, else looking about, a few times through before the next pick.
    private void Rest()
    {
        var name = _player.HasAnimation("graze") && _random.Next(3) > 0 ? "graze" : "idle";
        _player.Play(name, Blend);
        _restLeft = (float)_player.GetAnimation(name).Length * _random.Next(2, 5);
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
}
