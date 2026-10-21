using Rts.Client.Units;
using Rts.Simulation.Math;

namespace Rts.Simulation.Tests.Client;

public sealed class UnitFacingTests
{
    private const float Tolerance = 1e-5f;

    [Fact]
    public void Cardinal_directions_map_to_godot_yaw()
    {
        // Godot: a model looks along −Z; rotating it by −π/2 around +Y makes it look along +X (east).
        Assert.Equal(0f, UnitFacing.ToYaw(BAngle.North), Tolerance);
        Assert.Equal(-MathF.PI / 2, UnitFacing.ToYaw(BAngle.East), Tolerance);
        Assert.Equal(-MathF.PI, UnitFacing.ToYaw(BAngle.South), Tolerance);
        Assert.Equal(-3 * MathF.PI / 2, UnitFacing.ToYaw(BAngle.West), Tolerance);
    }

    [Fact]
    public void Yaw_turns_the_godot_forward_vector_toward_the_map_direction()
    {
        var random = new TestRandom(5);
        for (var i = 0; i < 100; i++)
        {
            var angle = BAngle.FromUnits(random.NextInt(BAngle.FullTurn));
            var yaw = UnitFacing.ToYaw(angle);

            // Rotating (0, 0, −1) by yaw around +Y gives (−sin yaw, 0, −cos yaw); map X is Godot X, map Y is Godot Z.
            var (x, y) = FixedMath.Direction(angle);
            Assert.Equal(x.Raw / 65536f, -MathF.Sin(yaw), 1e-3f);
            Assert.Equal(y.Raw / 65536f, -MathF.Cos(yaw), 1e-3f);
        }
    }

    [Fact]
    public void Interpolation_takes_the_short_arc_through_north()
    {
        var from = BAngle.FromDegrees(350);
        var to = BAngle.FromDegrees(10);

        Assert.Equal(UnitFacing.ToYaw(from), UnitFacing.InterpolatedYaw(from, to, 0f), Tolerance);
        // Halfway is north (0° or 360°, the same rotation), not south.
        var halfway = UnitFacing.InterpolatedYaw(from, to, 0.5f);
        Assert.Equal(1f, MathF.Cos(halfway), 1e-3f);
        var end = UnitFacing.InterpolatedYaw(from, to, 1f);
        Assert.Equal(MathF.Cos(UnitFacing.ToYaw(to)), MathF.Cos(end), Tolerance);
        Assert.Equal(MathF.Sin(UnitFacing.ToYaw(to)), MathF.Sin(end), Tolerance);
    }
}
