using System;
using Godot;
using Rts.MapGen;

namespace Rts.Client.Maps;

/// <summary>
/// The settings of a random map as controls (docs/implementation/M7_maps.md, 4.14): seed, size, players and symmetry, relief,
/// water, forest and ore; in the editor the time of day too (<see cref="ShowLighting"/>: a match never picks it, the map has it).
/// Shared by the map editor and the skirmish screen; <see cref="Settings"/> is what they show now.
/// </summary>
public partial class MapGenOptions : VBoxContainer
{
    private const float FieldHeight = 40f;

    private SpinBox _seed = null!;

    public MapGenSettings Settings => Chosen with { Seed = (uint)_seed.Value };

    /// <summary>Columns of the settings: two in the wide panel of the editor, one in the narrow column of the skirmish screen.</summary>
    public int Columns { get; init; } = 2;

    public bool ShowLighting { get; init; }

    private MapGenSettings Chosen { get; set; } = new();

    public override void _Ready()
    {
        AddThemeConstantOverride("separation", 6);
        var seedRow = new HBoxContainer();
        seedRow.AddThemeConstantOverride("separation", 8);
        seedRow.AddChild(new Label { Text = Tr("UI_EDITOR_SEED"), VerticalAlignment = VerticalAlignment.Center });
        _seed = new SpinBox { MinValue = 0, MaxValue = 999_999, Value = 1, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        seedRow.AddChild(_seed);
        AddChild(seedRow);

        var grid = new GridContainer { Columns = Columns };
        grid.AddThemeConstantOverride("h_separation", 8);
        AddChild(grid);
        var sizes = MapGenSettings.Sizes;
        grid.AddChild(Choice(Array.ConvertAll(sizes, size => string.Format(Tr("UI_EDITOR_SIZE_ITEM"), size)), 0, i => Chosen = Chosen with { Size = sizes[i] }));
        grid.AddChild(Choice(
            [Tr("UI_EDITOR_SYMMETRY_MIRROR"), Tr("UI_EDITOR_SYMMETRY_ROTATE180"), Tr("UI_EDITOR_SYMMETRY_ROTATE90")], 0,
            i => Chosen = Chosen with { Symmetry = (MapSymmetry)i, Players = i == 2 ? 4 : 2 }));
        grid.AddChild(Choice([Tr("UI_EDITOR_RELIEF_FLAT"), Tr("UI_EDITOR_RELIEF_HILLY"), Tr("UI_EDITOR_RELIEF_MOUNTAINOUS")], 1, i => Chosen = Chosen with { Relief = (ReliefKind)i }));
        grid.AddChild(Choice([Tr("UI_EDITOR_WATER_NONE"), Tr("UI_EDITOR_WATER_RIVERS"), Tr("UI_EDITOR_WATER_LAKES")], 1, i => Chosen = Chosen with { Water = (WaterKind)i }));
        grid.AddChild(Choice([Tr("UI_EDITOR_FOREST_LOW"), Tr("UI_EDITOR_FOREST_MEDIUM"), Tr("UI_EDITOR_FOREST_HIGH")], 1, i => Chosen = Chosen with { Forest = (Amount)i }));
        grid.AddChild(Choice([Tr("UI_EDITOR_ORE_LOW"), Tr("UI_EDITOR_ORE_MEDIUM"), Tr("UI_EDITOR_ORE_HIGH")], 1, i => Chosen = Chosen with { Resources = (Amount)i }));
        if (ShowLighting)
        {
            var ids = Rts.Data.Maps.MapLighting.Ids;
            grid.AddChild(Choice([.. System.Linq.Enumerable.Select(ids, id => Tr(Climates.SceneLighting.LabelKey(id)))], ids.IndexOf(Chosen.Lighting),
                i => Chosen = Chosen with { Lighting = ids[i] }));
        }
    }

    private static OptionButton Choice(string[] items, int selected, Action<int> chosen)
    {
        var choice = new OptionButton { CustomMinimumSize = new Vector2(0, FieldHeight), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        foreach (var item in items)
        {
            choice.AddItem(item);
        }

        choice.Selected = selected;
        choice.ItemSelected += index => chosen((int)index);
        return choice;
    }
}
