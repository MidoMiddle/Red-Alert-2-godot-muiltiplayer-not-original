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
/// unit and building of the data with its role colour and code. With <c>--buildings</c> every building of the players in its look
/// (<see cref="Terrain.BuildingLooks"/>), the Allies' rows first, in the order of the sidebar. With <c>--models</c> every model of the
/// table of models (docs/art/model_standards.md) on a plate of its footprint, to check its size against the cells.
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

        var buildings = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--buildings") >= 0;
        var models = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--models") >= 0;
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

        if (models)
        {
            ShowModels();
            ScreenshotCapture.AttachIfRequested(this);
            return;
        }

        if (buildings)
        {
            ShowBuildings();
            ScreenshotCapture.AttachIfRequested(this);
            return;
        }

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

    /// <summary>
    /// Every model of the table on a plate of its footprint, a cell grid on it, its key under it; a model of a cell (an object, a tree,
    /// ore) nine times on a plate of 3×3, each cell picking its kind, size and turn.
    /// </summary>
    private void ShowModels()
    {
        const int ModelColumns = 6;
        const float Step = 6f;
        var database = DataFiles.Current.Rules.Database;
        var keys = new System.Collections.Generic.List<string>(Models.ModelLibrary.Table.Keys);
        keys.Sort(System.StringComparer.Ordinal);
        var rows = (keys.Count + ModelColumns - 1) / ModelColumns;
        for (var i = 0; i < keys.Count; i++)
        {
            var key = keys[i];
            var id = key[(key.IndexOf('/') + 1)..];
            var single = key.StartsWith("building/", System.StringComparison.Ordinal) || key.StartsWith("unit/", System.StringComparison.Ordinal);
            var (width, height) = key.StartsWith("building/", System.StringComparison.Ordinal)
                ? database.TryGetBuilding(id, out var def) ? (def.Footprint.Width, def.Footprint.Height)
                : Rts.Simulation.World.BuildingTypes.TryGetById(id, out var type) ? (type.Width, type.Height) : (1, 1)
                : single ? (1, 1) : (3, 3);
            var at = new Vector3((i % ModelColumns - (ModelColumns - 1) / 2f) * Step, 0f, (i / ModelColumns - (rows - 1) / 2f) * Step);
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    AddChild(new MeshInstance3D
                    {
                        Mesh = new PlaneMesh { Size = new Vector2(0.94f, 0.94f) },
                        Position = at + new Vector3(x - width / 2f + 0.5f, 0.005f, y - height / 2f + 0.5f),
                        MaterialOverride = new StandardMaterial3D { AlbedoColor = (x + y) % 2 == 0 ? new Color(0.32f, 0.34f, 0.32f) : new Color(0.26f, 0.28f, 0.27f) },
                    });
                }
            }

            for (var copy = 0; copy < (single ? 1 : 9); copy++)
            {
                var cell = single ? (Vector2I?)null : new Vector2I(i * 3 + copy % 3, copy / 3);
                if (Models.ModelLibrary.Instantiate(key, Matches.PlayerColors.Of(new PlayerId(1)), cell: cell) is { } model)
                {
                    model.Position += at + (single ? Vector3.Zero : new Vector3(copy % 3 - 1, 0, copy / 3 - 1));
                    AddChild(model);
                }
            }

            AddChild(new Label3D
            {
                Text = key,
                Position = at + new Vector3(0, 0.05f, height / 2f + 0.7f),
                RotationDegrees = new Vector3(-60f, 0, 0),
                FontSize = 40,
                PixelSize = 0.01f,
                OutlineSize = 10,
                Modulate = new Color(0.95f, 0.93f, 0.85f),
            });
        }

        AddChild(new Camera3D
        {
            Projection = Camera3D.ProjectionType.Orthogonal,
            // Tall enough for the rows and wide enough for the columns of a 16:9 screen.
            Size = Mathf.Max(rows * Step + 3f, (ModelColumns * Step + 1f) * 9f / 16f),
            Position = new Vector3(0, 30f, 21f),
            RotationDegrees = new Vector3(-55f, 0, 0),
            Current = true,
        });
    }

    /// <summary>Every building of the players in its look, in rows of <see cref="BuildingColumns"/>, the Allies first.</summary>
    private void ShowBuildings()
    {
        const int BuildingColumns = 7;
        const float Step = 5f;
        var database = DataFiles.Current.Rules.Database;
        var ra2 = AppSettings.Instance?.Values.Ra2Names ?? true;
        var defs = new System.Collections.Generic.List<Rts.Simulation.Rules.BuildingDef>();
        foreach (var faction in new[] { "alliance", "union" })
        {
            var ofFaction = new System.Collections.Generic.List<Rts.Simulation.Rules.BuildingDef>();
            foreach (var def in database.Buildings)
            {
                if (def.Faction == faction)
                {
                    ofFaction.Add(def);
                }
            }

            ofFaction.Sort((a, b) => SidebarModel.Ra2Place(a.Id).CompareTo(SidebarModel.Ra2Place(b.Id)));
            defs.AddRange(ofFaction);
            // A faction starts a row of its own.
            while (defs.Count % BuildingColumns != 0)
            {
                defs.Add(null!);
            }
        }

        var rows = (defs.Count + BuildingColumns - 1) / BuildingColumns;
        for (var i = 0; i < defs.Count; i++)
        {
            if (defs[i] is not { } def)
            {
                continue;
            }

            var at = new Vector3((i % BuildingColumns - (BuildingColumns - 1) / 2f) * Step, 0f, (i / BuildingColumns - (rows - 1) / 2f) * Step);
            var body = new Node3D { Position = at };
            AddChild(body);
            Terrain.BuildingLookView.AddTo(body, def, Matches.PlayerColors.Of(new PlayerId(def.Faction == "union" ? 2 : 1)), 0f);
            AddChild(new MeshInstance3D
            {
                Mesh = new PlaneMesh { Size = new Vector2(def.Footprint.Width, def.Footprint.Height) },
                Position = at + new Vector3(0, 0.005f, 0),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.3f, 0.32f, 0.3f) },
            });
            AddChild(new Label3D
            {
                Text = DisplayNames.Of(def.Id, def.Name, ra2, key => Tr(key)),
                Position = at + new Vector3(0, 0.05f, def.Footprint.Height / 2f + 0.6f),
                RotationDegrees = new Vector3(-60f, 0, 0),
                FontSize = 64,
                PixelSize = 0.01f,
                OutlineSize = 10,
                Modulate = new Color(0.95f, 0.93f, 0.85f),
            });
        }

        AddChild(new Camera3D
        {
            Projection = Camera3D.ProjectionType.Orthogonal,
            Size = rows * Step + 1f,
            Position = new Vector3(0, 30f, 21f),
            RotationDegrees = new Vector3(-55f, 0, 0),
            Current = true,
        });
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
