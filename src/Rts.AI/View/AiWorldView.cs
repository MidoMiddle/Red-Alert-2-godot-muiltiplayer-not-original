using System.Collections.Generic;
using System.Collections.Immutable;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Rules;
using Rts.Simulation.Visibility;
using Rts.Simulation.World;

namespace Rts.AI.View;

/// <summary>
/// The world as one bot may know it (docs/implementation/M5_first_bot.md, 4.5) — the only way its modules read the world:
/// <list type="bullet">
/// <item>its own units and buildings and those of its allies in full;</item>
/// <item>enemies only while its player sees them (<see cref="Vision.Sees"/>), and only once the bot has noticed them — the
/// <see cref="Perception"/> of M8 step 3;</item>
/// <item>land and ore only in explored cells; in the shroud a cell counts as enterable, as the units of a person take it;</item>
/// <item>in the fog a foreign building does not block a cell: what the bot knows of it is in its <see cref="AiMemory"/>.</item>
/// <item>with the open map of «Brutal» (<see cref="Rts.Simulation.Matches.PlayerModifiers.RevealMap"/>, M8 step 2) every cell is in sight
/// and every enemy seen.</item>
/// </list>
/// The world itself is not reachable from here, nor anything of it that can be changed. Rebuilt on every turn of the bot; the lists are in
/// the order of the entity ids.
/// </summary>
public sealed class AiWorldView
{
    private readonly List<Unit> _ownUnits = new();
    private readonly List<Building> _ownBuildings = new();
    private readonly List<Entity> _allied = new();
    private readonly List<Entity> _enemies = new();
    private GameWorld _world;

    internal AiWorldView(GameWorld world, PlayerId me, CellPos? start, ImmutableArray<CellPos> mapStarts, int perceptionDelay = 0)
    {
        _world = world;
        Me = me;
        Start = start;
        MapStarts = mapStarts.IsDefault ? [] : mapStarts;
        Perception = new PerceptionBuffer(perceptionDelay);
    }

    /// <summary>What the bot has noticed of what its player sees (M8 step 3).</summary>
    public PerceptionBuffer Perception { get; }

    /// <summary>The player of the bot.</summary>
    public PlayerId Me { get; }

    /// <summary>The tick just executed.</summary>
    public int Tick { get; private set; }

    /// <summary>The bot's own player: credits, power, production queues, what it may build.</summary>
    public PlayerState Player => _world.Player(Me);

    /// <summary>The rules of the match: the data every player knows.</summary>
    public GameRules Rules => _world.Rules;

    public int Width => _world.Map.Width;

    public int Height => _world.Map.Height;

    /// <summary>The bot's own start, or null outside a match.</summary>
    public CellPos? Start { get; }

    /// <summary>Every start of the map, taken or not (<see cref="Rts.Simulation.Matches.MatchSetup.MapStarts"/>).</summary>
    public ImmutableArray<CellPos> MapStarts { get; }

    public IReadOnlyList<Unit> OwnUnits => _ownUnits;

    public IReadOnlyList<Building> OwnBuildings => _ownBuildings;

    /// <summary>The living units and buildings of the allies, in full.</summary>
    public IReadOnlyList<Entity> Allied => _allied;

    /// <summary>The living units and buildings of the enemies that the bot's player sees now and the bot has noticed.</summary>
    public IReadOnlyList<Entity> VisibleEnemies => _enemies;

    /// <summary>Whether <paramref name="player"/> is the bot's player or an ally of it.</summary>
    public bool IsAlly(PlayerId player) => (_world.AlliesOf(Me) & Bit(player)) != 0;

    /// <summary>Whether <paramref name="player"/> plays against the bot: not neutral, not an ally.</summary>
    public bool IsEnemy(PlayerId player) => !player.IsNeutral && !IsAlly(player);

    /// <summary>Whether the bot's player sees <paramref name="entity"/> now — an enemy only once the bot has noticed it.</summary>
    public bool Sees(Entity entity) =>
        Vision.Sees(_world, Me, entity) && (!IsEnemy(entity.Owner) || !Perception.IsNoticing(entity.Id.Value, Tick));

    /// <summary>Whether <paramref name="entity"/>, an enemy in sight, is not yet noticed: not to be taken for gone.</summary>
    public bool IsNoticing(Entity entity) => Perception.IsNoticing(entity.Id.Value, Tick);

    public bool Contains(CellPos cell) => _world.Map.Contains(cell);

    /// <summary>Whether the bot's player sees the whole map (M8 step 2).</summary>
    public bool RevealsMap => Player.Modifiers.RevealMap;

    /// <summary>Whether the match is played without the fog of war: what the bot has explored stays in sight, as for a person.</summary>
    public bool NoFog => Vision.NoFog(_world);

    public CellVisibility StateOf(CellPos cell) =>
        (RevealsMap && Contains(cell)) || (NoFog && _world.VisionOf(Me).IsExplored(cell)) ? CellVisibility.Visible : _world.VisionOf(Me).StateAt(cell);

    public bool IsVisible(CellPos cell) => (RevealsMap && Contains(cell)) || _world.VisionOf(Me).IsVisible(cell) || (NoFog && _world.VisionOf(Me).IsExplored(cell));

    public bool IsExplored(CellPos cell) => (RevealsMap && Contains(cell)) || _world.VisionOf(Me).IsExplored(cell);

    /// <summary>The land of an explored cell; null in the shroud and off the map.</summary>
    public LandType? LandAt(CellPos cell) => Contains(cell) && IsExplored(cell) ? _world.Map.LandAt(cell) : null;

    /// <summary>
    /// Whether <paramref name="locomotor"/> can enter the cell as far as the bot knows: in sight — as it is; in the fog — as it is, but a
    /// foreign building there does not count; in the shroud — yes, as for the units of a person. Off the map — no.
    /// </summary>
    public bool CanEnter(CellPos cell, Locomotor locomotor)
    {
        if (!Contains(cell))
        {
            return false;
        }

        if (!IsExplored(cell))
        {
            return true;
        }

        if (!IsVisible(cell) && _world.Map.HasBuilding(cell) && _world.BuildingAt(cell) is { } building && !IsAlly(building.Owner))
        {
            return true;
        }

        return _world.Map.CanEnter(cell, locomotor);
    }

    /// <summary>
    /// Whether <paramref name="def"/> can be placed with its north-west cell on <paramref name="corner"/> by the rules of the game
    /// (<see cref="PlacementRules.Check"/>) — asked only of explored cells: a footprint that reaches into the shroud is no. With
    /// <paramref name="needsBase"/> false and the MCV as <paramref name="ignoreUnit"/> it tells where the MCV can deploy.
    /// </summary>
    public bool CanPlace(BuildingDef def, CellPos corner, bool needsBase = true, EntityId ignoreUnit = default)
    {
        for (var y = corner.Y; y < corner.Y + def.Footprint.Height; y++)
        {
            for (var x = corner.X; x < corner.X + def.Footprint.Width; x++)
            {
                if (!IsExplored(new CellPos(x, y)))
                {
                    return false;
                }
            }
        }

        return PlacementRules.Check(_world, Me, def, corner, needsBase, ignoreUnit) == PlacementProblem.None;
    }

    /// <summary>Whether the bot's player has a ready factory of <paramref name="category"/>.</summary>
    public bool HasFactory(ProductionCategory category) => ProductionRules.HasFactory(_world, Me, category);

    /// <summary>The ore of an explored cell (as it is now); none in the shroud.</summary>
    public OreCell OreAt(CellPos cell) => Contains(cell) && IsExplored(cell) ? _world.Map.OreAt(cell) : OreCell.Empty;

    /// <summary>The view after tick <paramref name="tick"/> of <paramref name="world"/>.</summary>
    internal void Refresh(GameWorld world, int tick)
    {
        _world = world;
        Tick = tick;
        _ownUnits.Clear();
        _ownBuildings.Clear();
        _allied.Clear();
        _enemies.Clear();
        foreach (var entity in world.Entities)
        {
            if (!entity.IsAlive || entity is not (Unit or Building))
            {
                continue;
            }

            if (entity.Owner == Me)
            {
                if (entity is Unit unit)
                {
                    _ownUnits.Add(unit);
                }
                else
                {
                    _ownBuildings.Add((Building)entity);
                }
            }
            else if (IsAlly(entity.Owner))
            {
                _allied.Add(entity);
            }
            else if (IsEnemy(entity.Owner) && Vision.Sees(world, Me, entity) && Perception.Sight(entity.Id.Value, tick))
            {
                _enemies.Add(entity);
            }
        }

        Perception.EndTick(tick, Player.LastUnderAttack);
    }

    private static int Bit(PlayerId player) => player.Value is >= 1 and <= PlayerId.MaxPlayers ? 1 << (player.Value - 1) : 0;
}
