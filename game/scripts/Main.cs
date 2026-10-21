using Godot;
using Rts.Simulation;

namespace Rts.Client;

/// <summary>
/// Entry scene. For now it only proves that the client is wired to the simulation library.
/// </summary>
public partial class Main : Node3D
{
    public override void _Ready()
    {
        var text = $"Simulation v{SimulationVersion.Current}";
        GetNode<Label>("Overlay/Info").Text = text;
        GD.Print(text);
    }
}
