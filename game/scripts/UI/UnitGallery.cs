using Godot;
using Rts.Client.App;
using Rts.Client.Hud;
using Rts.Client.Units;
using Rts.Simulation.World;

namespace Rts.Client.UI;

/// <summary>
/// Every unit type in its placeholder shape, in rows, with its name under it (DisplayNames: RA2's for now) — to learn the shapes
/// until the models come, and to check them (<c>-- --ui-units</c>, with <c>--screenshot</c>). The Union's on the left rows, the
/// Alliance's after them, in the order of <see cref="UnitTypes.All"/>. With <c>--icons</c> the squares of the sidebar instead: every
/// unit and building of the data with its role colour and code.
/// </summary>
public partial class UnitGallery : Node3D
{
    private const int Columns = 10;
    private const float Spacing = 2.4f;

    public override void _Ready()
    {
        Localization.Load();
        if (System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--icons") >= 0)
        {
            ShowIcons();
            ScreenshotCapture.AttachIfRequested(this);
            return;
        }

        AddChild(new WorldEnvironment
        {
            Environment = new Environment
            {
                BackgroundMode = Environment.BGMode.Color,
                BackgroundColor = new Color(0.16f, 0.18f, 0.2f),
                AmbientLightSource = Environment.AmbientSource.Color,
                AmbientLightColor = new Color(0.6f, 0.6f, 0.65f),
            },
        });
        AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-55f, -35f, 0f), ShadowEnabled = true });

        var rules = DataFiles.Current.Rules;
        var colour = new StandardMaterial3D { AlbedoColor = Matches.PlayerColors.Of(new PlayerId(1)) };
        var rows = (UnitTypes.All.Count + Columns - 1) / Columns;
        for (var i = 0; i < UnitTypes.All.Count; i++)
        {
            var archetype = rules.Unit(UnitTypes.All[i]);
            var at = new Vector3((i % Columns - (Columns - 1) / 2f) * Spacing, 0f, (i / Columns - (rows - 1) / 2f) * Spacing);
            var view = new UnitView();
            view.Build(archetype);
            AddChild(view);
            view.SetMaterial(colour);
            var lift = archetype.Type == UnitType.KirovAirship ? 0.9f : archetype.Flies && !archetype.Jumpjet ? 0.4f : 0f;
            view.SetPose(at + new Vector3(0, lift, 0), Mathf.DegToRad(-30f), Mathf.DegToRad(-30f));

            rules.Database.TryGetUnit(archetype.Id, out var def);
            AddChild(new Label3D
            {
                Text = DisplayNames.Of(archetype.Id, def?.Name ?? archetype.Id, AppSettings.Instance?.Values.Ra2Names ?? true, key => Tr(key)),
                Position = at + new Vector3(0, 0.05f, 0.85f),
                RotationDegrees = new Vector3(-60f, 0, 0),
                FontSize = 48,
                PixelSize = 0.005f,
                OutlineSize = 8,
                Modulate = new Color(0.95f, 0.93f, 0.85f),
            });
        }

        AddChild(new Camera3D
        {
            Projection = Camera3D.ProjectionType.Orthogonal,
            Size = rows * Spacing + 1.5f,
            Position = new Vector3(0, 14f, 10f),
            RotationDegrees = new Vector3(-55f, 0, 0),
            Current = true,
        });
        ScreenshotCapture.AttachIfRequested(this);
    }

    /// <summary>The squares of the sidebar for every unit and building, as the player sees them, available.</summary>
    private void ShowIcons()
    {
        var database = DataFiles.Current.Rules.Database;
        var ra2 = AppSettings.Instance?.Values.Ra2Names ?? true;
        var grid = new GridContainer { Columns = 12, Position = new Vector2(8f, 8f) };
        grid.AddThemeConstantOverride("h_separation", 4);
        grid.AddThemeConstantOverride("v_separation", 4);
        var layer = new CanvasLayer();
        AddChild(layer);
        layer.AddChild(new ColorRect { Color = new Color(0.1f, 0.11f, 0.12f), Size = new Vector2(4000f, 4000f) });
        layer.AddChild(grid);
        for (var i = 0; i < database.Buildings.Length; i++)
        {
            var def = database.Buildings[i];
            var name = DisplayNames.Of(def.Id, def.Name, ra2, key => Tr(key));
            grid.AddChild(new BuildIcon
            {
                Cameo = new Cameo(Rts.Simulation.World.ProductionKind.Building, i, name, def.Cost, CameoState.Available, 0f, 0, "")
                {
                    Role = SidebarModel.RoleOf(def),
                    Code = SidebarModel.CodeOf(name),
                },
            });
        }

        for (var i = 0; i < database.Units.Length; i++)
        {
            var def = database.Units[i];
            var name = DisplayNames.Of(def.Id, def.Name, ra2, key => Tr(key));
            grid.AddChild(new BuildIcon
            {
                Cameo = new Cameo(Rts.Simulation.World.ProductionKind.Unit, i, name, def.Cost, CameoState.Available, 0f, 0, "")
                {
                    Role = SidebarModel.RoleOf(def.Category),
                    Code = SidebarModel.CodeOf(name),
                },
            });
        }
    }
}
