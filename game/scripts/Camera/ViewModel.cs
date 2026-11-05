using Godot;
using Rts.Simulation.World;

namespace Rts.Client;

/// <summary>
/// The weapon in the hands of the player inside a soldier (Possession; the owner, 2026-11-05: from the unit's own model). A copy of the
/// soldier's model at its own size stands with its eyes at the camera and turns with the look, posed by the clips of its rifle
/// (<see cref="Units.HumanAnimator"/>, aiming); only its <c>weapon</c> node is seen, so the gun is where his hands hold it. A soldier
/// whose model has no weapon, or who has no model, holds a plain rifle of blocks. A shot kicks it back and up and flashes at the muzzle;
/// on the move it sways. The look only.
/// </summary>
public partial class ViewModel : Node3D
{
    // The eyes over the soldier's feet, as the camera of Possession stands; the copy a little back and to the right of them.
    private const float Eyes = 0.5f;
    private static readonly Vector3 Shift = new(0.04f, 0f, 0.06f);

    // Where the muzzle is seen, from the camera: right, down, ahead.
    private static readonly Vector3 Muzzle = new(0.18f, -0.096f, -0.45f);

    private const float KickBack = 0.05f;
    private const float KickUp = 0.05f;
    private const float KickEase = 9f;
    private const float FlashSeconds = 0.06f;

    private static readonly StandardMaterial3D Steel = new() { AlbedoColor = new Color(0.16f, 0.17f, 0.18f), Roughness = 0.5f, Metallic = 0.6f };
    private static readonly StandardMaterial3D Wood = new() { AlbedoColor = new Color(0.33f, 0.22f, 0.13f), Roughness = 0.8f };
    private static readonly StandardMaterial3D Fire = new()
    {
        AlbedoColor = new Color(1f, 0.8f, 0.4f),
        EmissionEnabled = true,
        Emission = new Color(1f, 0.7f, 0.3f),
        EmissionEnergyMultiplier = 4f,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
    };

    private Node3D? _model;
    private Units.HumanAnimator? _animator;
    private MeshInstance3D? _flash;
    private float _kick;
    private float _flashLeft;
    private float _sway;

    /// <summary>Where the muzzle is now, in the world: the tracer of a shot leaves from there.</summary>
    public Vector3 MuzzleAt => GlobalTransform * Muzzle;

    /// <summary>How far up a shot has kicked the look, in radians: the camera adds it while it eases off.</summary>
    public float Lift => _kick * KickUp;

    /// <summary>The weapon of <paramref name="archetype"/>: that of its model, else the plain rifle.</summary>
    public static ViewModel Of(UnitArchetype archetype)
    {
        var view = new ViewModel { Name = "ViewModel" };
        if (Models.ModelLibrary.Instantiate("unit/" + archetype.Id) is { } model && model.FindChild("weapon", true, false) is Node3D weapon)
        {
            view._model = model;
            view.AddChild(model);
            Keep(model, weapon);
            view._animator = Units.HumanAnimator.Attach(model, archetype.Id);
            if (model.FindChild("Clips", true, false) is AnimationPlayer clips && clips.HasAnimation("rifle_idle_1"))
            {
                clips.Play("rifle_idle_1");
            }
        }
        else
        {
            view.AddChild(PlainRifle());
        }

        view._flash = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.016f, Height = 0.032f },
            MaterialOverride = Fire,
            Position = Muzzle,
            Visible = false,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        return view;
    }

    public override void _Ready()
    {
        if (_flash is not null && _flash.GetParent() is null)
        {
            AddChild(_flash);
        }
    }

    /// <summary>A shot: the kick, the soldier's own clip of it, the flash.</summary>
    public void Shot()
    {
        _kick = 1f;
        _flashLeft = FlashSeconds;
        _animator?.Fire();
    }

    /// <summary>Stands it at the camera, <paramref name="walking"/> swaying, the kick easing off.</summary>
    public void Follow(Transform3D camera, bool walking, float delta)
    {
        _kick = Mathf.Max(0f, _kick - (_kick * KickEase * delta) - (delta * 0.5f));
        _sway += delta * (walking ? 9f : 1.5f);
        var sway = walking
            ? new Vector3(Mathf.Cos(_sway * 0.5f) * 0.008f, Mathf.Abs(Mathf.Sin(_sway)) * -0.01f, 0f)
            : new Vector3(0f, Mathf.Sin(_sway) * 0.002f, 0f);
        var back = new Vector3(0f, 0f, _kick * KickBack);
        if (_model is not null)
        {
            // The soldier's feet under the camera, the body turned with the look about the eyes.
            _model.Transform = new Transform3D(Basis.Identity, new Vector3(0f, -Eyes, 0f) + Shift + sway + back);
        }
        else if (GetChildCount() > 0 && GetChild(0) is Node3D rifle)
        {
            rifle.Position = new Vector3(0.09f, -0.09f, -0.24f) + sway + back;
        }

        GlobalTransform = camera;
        if (_flash is not null)
        {
            _flashLeft -= delta;
            _flash.Visible = _flashLeft > 0f;
            _flash.Position = Muzzle + sway;
        }
    }

    // Only the weapon is seen: every other mesh of the copy is hidden, none casts a shadow.
    private static void Keep(Node node, Node weapon)
    {
        if (node is GeometryInstance3D mesh)
        {
            var part = node == weapon || weapon.IsAncestorOf(node);
            mesh.Visible = part;
            mesh.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        }

        foreach (var child in node.GetChildren())
        {
            Keep(child, weapon);
        }
    }

    // A rifle of blocks: a stock, a body, a barrel, along -Z.
    private static Node3D PlainRifle()
    {
        var rifle = new Node3D { Name = "PlainRifle" };
        rifle.AddChild(Block(new Vector3(0.035f, 0.05f, 0.12f), new Vector3(0f, -0.01f, 0.1f), Wood));
        rifle.AddChild(Block(new Vector3(0.04f, 0.045f, 0.2f), new Vector3(0f, 0f, -0.05f), Steel));
        rifle.AddChild(Block(new Vector3(0.015f, 0.015f, 0.2f), new Vector3(0f, 0.01f, -0.24f), Steel));
        return rifle;
    }

    private static MeshInstance3D Block(Vector3 size, Vector3 at, Material material) =>
        new()
        {
            Mesh = new BoxMesh { Size = size },
            MaterialOverride = material,
            Position = at,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
}
