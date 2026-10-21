using Godot;

namespace Rts.Client.Debug;

/// <summary>
/// Debug graph of the path queue (docs/implementation/M2_unit_control.md, 4.14), F8: a bar per tick for the A* nodes, a line
/// for the requests waiting, and the max and average under it. The scale grows with the busiest tick (at least
/// <see cref="MinScale"/> nodes), and the red budget line shows once the bars come near it. It reads
/// <see cref="SimulationHost.QueueHistory"/>.
/// </summary>
public partial class QueueGraph : Control
{
    private const float Width = 620f;
    private const float Height = 90f;
    private const float TextHeight = 22f;
    private const int FontSize = 16;
    private const int MinScale = 500;

    private static readonly Color Background = new(0f, 0f, 0f, 0.55f);
    private static readonly Color NodesColor = new(1f, 0.6f, 0.15f);
    private static readonly Color BudgetColor = new(1f, 0.25f, 0.2f, 0.8f);
    private static readonly Color QueueColor = new(0.3f, 0.9f, 1f);

    private SimulationHost? _host;

    public void Bind(SimulationHost host) => _host = host;

    public override void _Ready()
    {
        Visible = false;
        MouseFilter = MouseFilterEnum.Ignore;
        CustomMinimumSize = new Vector2(Width, Height + TextHeight);
        Size = CustomMinimumSize;
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (inputEvent.IsActionPressed("debug_queue", false, true))
        {
            Visible = !Visible;
        }
    }

    public override void _Process(double delta)
    {
        if (Visible)
        {
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(0, 0, Width, Height + TextHeight), Background);
        if (_host is null)
        {
            return;
        }

        var history = _host.QueueHistory;
        var budget = _host.Simulation.World.PathRequests.Budget;
        var top = Mathf.Max(MinScale, history.MaxNodes() * 5 / 4);
        var queueTop = Mathf.Max(10, history.MaxQueue());
        var step = Width / TickHistory.Capacity;
        if (budget <= top)
        {
            var budgetY = Height - Height * budget / top;
            DrawLine(new Vector2(0, budgetY), new Vector2(Width, budgetY), BudgetColor);
        }

        Vector2? previous = null;
        for (var i = 0; i < history.Count; i++)
        {
            var x = i * step;
            var bar = Height * history.NodesAt(i) / top;
            if (bar > 0f)
            {
                DrawRect(new Rect2(x, Height - bar, Mathf.Max(1f, step - 0.5f), bar), NodesColor);
            }

            var point = new Vector2(x + step / 2, Height - Height * history.QueueAt(i) / queueTop);
            if (previous is { } from)
            {
                DrawLine(from, point, QueueColor, 1.5f);
            }

            previous = point;
        }

        DrawString(
            ThemeDB.FallbackFont,
            new Vector2(4, Height + TextHeight - 4),
            $"A* nodes per tick (scale {top}, budget {budget}) · {history.Describe()}",
            HorizontalAlignment.Left,
            Width - 8,
            FontSize);
    }
}
