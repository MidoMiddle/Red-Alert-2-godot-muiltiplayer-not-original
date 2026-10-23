using System.Collections.Generic;
using System.Collections.Immutable;
using Rts.AI.View;
using Rts.Simulation.Ai;
using Rts.Simulation.Commands;
using Rts.Simulation.World;

namespace Rts.AI;

/// <summary>
/// How many commands a bot gives (docs/implementation/M8_ai_difficulty.md, 4.3, step 3): its APM. The modules do not give commands
/// themselves — they leave them here, and at the end of the bot's turn at most <see cref="AiSettings.ActionsPerSecond"/> go out in any
/// <see cref="WindowTicks"/> ticks running, the most urgent module first (<see cref="AiModuleKind"/> by <see cref="PriorityOf"/>), each in
/// the order it came. One command is one action, however many units it moves. What waits:
/// <list type="bullet">
/// <item>a command the same as one waiting — but for its tick and number — is not left twice: a module that decides the same again does
/// not pile it up;</item>
/// <item>one that waited longer than <see cref="MaxWaitTicks"/> is dropped: its module has decided anew by then;</item>
/// <item>as it goes out, its units are those still alive and the bot's, and its target still alive and known; a command with nothing left
/// is dropped, so that a late command is never one the game refuses.</item>
/// </list>
/// </summary>
public sealed class ActionBudget(int actionsPerSecond)
{
    /// <summary>The ticks the limit counts over: a second.</summary>
    public const int WindowTicks = 15;

    /// <summary>The ticks a command may wait for its turn.</summary>
    public const int MaxWaitTicks = 15;

    private readonly List<(Command Command, int Priority, int Order, int Since)> _waiting = new();
    private readonly Queue<int> _sent = new();
    private int _order;

    public int ActionsPerSecond { get; } = actionsPerSecond;

    /// <summary>The commands waiting for their turn.</summary>
    public int Waiting => _waiting.Count;

    /// <summary>The order the modules go out in: the squads first — a fight cannot wait — then the base, the economy, the army, scouting.</summary>
    public static int PriorityOf(AiModuleKind kind) => kind switch
    {
        AiModuleKind.Squads => 0,
        AiModuleKind.Defense => 1,
        AiModuleKind.BuildPlanner => 2,
        AiModuleKind.Economy => 3,
        AiModuleKind.Production => 4,
        AiModuleKind.Scouting => 5,
        _ => 6,
    };

    /// <summary>Leaves <paramref name="command"/> of <paramref name="module"/>, given after <paramref name="tick"/>, to wait for its turn.</summary>
    internal void Add(Command command, AiModuleKind module, int tick)
    {
        var plain = Plain(command);
        foreach (var waiting in _waiting)
        {
            if (Plain(waiting.Command) == plain)
            {
                return;
            }
        }

        _waiting.Add((command, PriorityOf(module), _order++, tick));
    }

    /// <summary>Gives the commands whose turn has come, for the tick after <see cref="AiTurn.Tick"/>, each with a new number.</summary>
    internal void Release(AiTurn turn, AiWorldView view)
    {
        var tick = turn.Tick;
        _waiting.RemoveAll(waiting => tick - waiting.Since > MaxWaitTicks);
        while (_sent.Count > 0 && tick - _sent.Peek() >= WindowTicks)
        {
            _sent.Dequeue();
        }

        _waiting.Sort((a, b) => a.Priority != b.Priority ? a.Priority.CompareTo(b.Priority) : a.Order.CompareTo(b.Order));
        while (_waiting.Count > 0 && _sent.Count < ActionsPerSecond)
        {
            var command = Current(_waiting[0].Command, view);
            _waiting.RemoveAt(0);
            if (command is not null)
            {
                turn.Issue(command with { ExecuteTick = turn.ExecuteTick, Sequence = turn.NextSequence() });
                _sent.Enqueue(tick);
            }
        }
    }

    private static Command Plain(Command command) => command with { ExecuteTick = 0, Sequence = 0 };

    /// <summary>The command as it can go out now: its units still alive and the bot's, its target still known; null when nothing is left.</summary>
    private static Command? Current(Command command, AiWorldView view)
    {
        if (command is AttackCommand strike && !Known(view, strike.Target))
        {
            return null;
        }

        if (command is not UnitsCommand order)
        {
            return command switch
            {
                DeployCommand deploy => Alive(view, deploy.Unit) ? command : null,
                SellCommand sell => Standing(view, sell.Building) ? command : null,
                _ => command,
            };
        }

        var units = ImmutableArray.CreateBuilder<EntityId>(order.Units.Length);
        foreach (var unit in order.Units)
        {
            if (Alive(view, unit))
            {
                units.Add(unit);
            }
        }

        if (units.Count == 0)
        {
            return null;
        }

        if (units.Count == order.Units.Length)
        {
            return command;
        }

        var left = units.ToImmutable();
        return command switch
        {
            MoveCommand move => new MoveCommand(move.Player, move.ExecuteTick, move.Sequence, left, move.Target, move.Queue),
            AttackMoveCommand attackMove => new AttackMoveCommand(attackMove.Player, attackMove.ExecuteTick, attackMove.Sequence, left, attackMove.Target, attackMove.Queue),
            AttackCommand attack => new AttackCommand(attack.Player, attack.ExecuteTick, attack.Sequence, left, attack.Target),
            HarvestCommand harvest => new HarvestCommand(harvest.Player, harvest.ExecuteTick, harvest.Sequence, left, harvest.Cell),
            _ => null,
        };
    }

    private static bool Alive(AiWorldView view, EntityId id)
    {
        foreach (var unit in view.OwnUnits)
        {
            if (unit.Id == id)
            {
                return true;
            }
        }

        return false;
    }

    private static bool Standing(AiWorldView view, EntityId id)
    {
        foreach (var building in view.OwnBuildings)
        {
            if (building.Id == id)
            {
                return true;
            }
        }

        return false;
    }

    private static bool Known(AiWorldView view, EntityId id)
    {
        foreach (var enemy in view.VisibleEnemies)
        {
            if (enemy.Id == id)
            {
                return true;
            }
        }

        return false;
    }
}
