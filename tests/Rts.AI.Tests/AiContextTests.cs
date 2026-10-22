using System.Reflection;
using Rts.AI.View;
using Rts.Simulation;
using Rts.Simulation.Ai;
using Rts.Simulation.Random;
using Rts.Simulation.World;

namespace Rts.AI.Tests;

/// <summary>
/// A module sees no more than its player (docs/implementation/M5_first_bot.md, 4.5, risk «the bot cheats by accident»): nothing a module
/// gets leads to the world, the simulation, the turn or the generator of chance.
/// </summary>
public sealed class AiContextTests
{
    private static readonly Type[] Forbidden = [typeof(GameWorld), typeof(GameSimulation), typeof(AiTurn), typeof(DeterministicRandom)];

    [Theory]
    [InlineData(typeof(AiContext))]
    [InlineData(typeof(AiWorldView))]
    [InlineData(typeof(AiMemory))]
    public void Nothing_public_leads_to_the_world(Type type)
    {
        const BindingFlags Public = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
        var types = type.GetProperties(Public).Select(property => property.PropertyType)
            .Concat(type.GetFields(Public).Select(field => field.FieldType))
            .Concat(type.GetMethods(Public).SelectMany(method => method.GetParameters().Select(parameter => parameter.ParameterType).Append(method.ReturnType)))
            .SelectMany(Unwrap)
            .ToList();

        Assert.DoesNotContain(types, Forbidden.Contains);
    }

    /// <summary>A type and the arguments of its generic types, so that a list of worlds counts too.</summary>
    private static IEnumerable<Type> Unwrap(Type type) =>
        type.IsGenericType ? type.GetGenericArguments().SelectMany(Unwrap).Append(type) : [type];
}
