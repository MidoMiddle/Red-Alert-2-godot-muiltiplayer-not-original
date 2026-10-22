using Godot;
using Rts.Simulation.World;

namespace Rts.Client.Terrain;

/// <summary>
/// Symbolic buildings (docs/implementation/conventions.md, section 12; M7_maps.md, 4.8): a block over the footprint, beige for
/// civilian houses and grey-teal for tech buildings, with a mark of their own — a tower on the oil derrick, a white cross on
/// the hospital, a pale runway on the airfield — and a band in the owner's colour on a building of a player. The buildings
/// players place are drawn by <see cref="PlayerBuildings"/> (M3 step 13), one view each. The footprint sinks a little into the
/// ground so a block on uneven ground leaves no gap. Rebuilt whenever the map changes (a building is placed or falls), which is rare.
/// </summary>
public partial class Buildings : Node3D
{
    private const float Margin = 0.08f;
    private const float Sink = 0.3f;

    private static readonly Color Civilian = new(0.78f, 0.7f, 0.55f);
    private static readonly Color Tech = new(0.45f, 0.58f, 0.6f);

    private object? _builtMap;
    private int _builtVersion = -1;

    public override void _Process(double delta)
    {
        // The map of the terrain: a match's, or the editor's (M7 step 12), which has no world and shows its start buildings.
        var map = GetParent<TerrainBuilder>().Map;
        var world = GetNodeOrNull<SimulationHost>("../../SimulationHost")?.Simulation.World;
        if (!ReferenceEquals(map, _builtMap) || map.Version != _builtVersion)
        {
            Rebuild(map, world);
        }
    }

    private void Rebuild(Rts.Simulation.Map.MapGrid map, GameWorld? world)
    {
        foreach (var child in GetChildren())
        {
            child.QueueFree();
        }

        _builtMap = map;
        _builtVersion = map.Version;
        if (world is null)
        {
            foreach (var start in map.StartBuildings)
            {
                Add(start.Kind, start.Cell, start.Owner);
            }

            return;
        }

        foreach (var entity in world.Entities)
        {
            if (entity is Building { IsAlive: true, Def: null } building)
            {
                Add(building.Kind, building.Cell, building.Owner);
            }
        }
    }

    private void Add(BuildingKind kind, Rts.Simulation.Geometry.CellPos cell, PlayerId owner)
    {
        var type = BuildingTypes.Get(kind);
        var tech = type.Capturable;
        var height = kind switch
        {
            BuildingKind.NeutralShed => 0.35f,
            BuildingKind.NeutralHouseA => 0.6f,
            BuildingKind.NeutralApartment => 1.4f,
            BuildingKind.TechAirfield => 0.2f,
            _ => 0.5f,
        };
        var centre = new Vector3(cell.X + type.Width / 2f, 0, cell.Y + type.Height / 2f);
        var ground = WorldCoords.Terrain!.HeightAt(centre.X, centre.Z);
        var size = new Vector3(type.Width - 2 * Margin, height + Sink, type.Height - 2 * Margin);
        Box(size, centre + new Vector3(0, ground + (height - Sink) / 2, 0), tech ? Tech : Civilian);
        var top = ground + height;

        switch (kind)
        {
            case BuildingKind.TechOilDerrick:
                Box(new Vector3(0.18f, 1.1f, 0.18f), centre + new Vector3(0.4f, top + 0.55f, 0.4f), new Color(0.2f, 0.2f, 0.22f));
                break;
            case BuildingKind.TechHospital:
                Box(new Vector3(1.4f, 0.04f, 0.4f), centre + new Vector3(0, top + 0.02f, 0), Colors.White);
                Box(new Vector3(0.4f, 0.04f, 1.4f), centre + new Vector3(0, top + 0.02f, 0), Colors.White);
                break;
            case BuildingKind.TechAirfield:
                Box(new Vector3(type.Width - 0.6f, 0.03f, 0.5f), centre + new Vector3(0, top + 0.015f, 0), new Color(0.85f, 0.85f, 0.8f));
                break;
        }

        if (!owner.IsNeutral)
        {
            Box(new Vector3(size.X + 0.02f, 0.08f, size.Z + 0.02f), centre + new Vector3(0, top - 0.1f, 0), OwnerColor(owner));
        }
    }

    /// <summary>The colour of a player's band.</summary>
    internal static Color OwnerColor(PlayerId owner) => owner.Value switch
    {
        1 => new Color(0.2f, 0.45f, 0.95f),
        2 => new Color(0.9f, 0.25f, 0.2f),
        var other => Color.FromHsv(other * 0.13f % 1f, 0.7f, 0.9f),
    };

    private void Box(Vector3 size, Vector3 position, Color color) =>
        AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = size, Material = new StandardMaterial3D { AlbedoColor = color } },
            Position = position,
        });
}
