using Rts.Client.Controls;

namespace Rts.Simulation.Tests.Client;

/// <summary>Rules of the key bindings (docs/implementation/M6_menus_skirmish.md, 4.6): defaults, rebinding, conflicts, reset.</summary>
public sealed class KeyBindingsTests
{
    [Fact]
    public void EveryActionHasADifferentDefaultKey()
    {
        var defaults = KeyBindings.Actions.Select(action => action.DefaultKey).ToList();

        Assert.Equal(defaults.Count, defaults.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void RebindingToAFreeKeyChangesOnlyThatAction()
    {
        var bindings = new KeyBindings();

        var result = bindings.Rebind("pause", "O");

        Assert.Equal(RebindStatus.Set, result.Status);
        Assert.Equal("O", bindings.KeyOf("pause"));
        Assert.Equal("N", bindings.KeyOf("step"));
    }

    [Fact]
    public void RebindingToAUsedKeySwapsTheKeys()
    {
        var bindings = new KeyBindings();

        var result = bindings.Rebind("pause", "N");

        Assert.Equal(new RebindResult(RebindStatus.Swapped, "step", "P"), result);
        Assert.Equal("N", bindings.KeyOf("pause"));
        Assert.Equal("P", bindings.KeyOf("step"));
    }

    [Theory]
    [InlineData("Escape")]
    [InlineData("Shift")]
    [InlineData("Enter")]
    [InlineData("")]
    public void ReservedAndModifierKeysAreRefused(string key)
    {
        var bindings = new KeyBindings();

        Assert.Equal(RebindStatus.Refused, bindings.Rebind("pause", key).Status);
        Assert.Equal("P", bindings.KeyOf("pause"));
    }

    [Fact]
    public void AnUnknownActionIsRefused()
    {
        var bindings = new KeyBindings();

        Assert.Equal(RebindStatus.Refused, bindings.Rebind("fly", "O").Status);
    }

    [Fact]
    public void ResetRestoresEveryDefault()
    {
        var bindings = new KeyBindings();
        bindings.Rebind("pause", "N");
        bindings.Rebind("camera_up", "I");

        bindings.Reset();

        foreach (var action in KeyBindings.Actions)
        {
            Assert.Equal(action.DefaultKey, bindings.KeyOf(action.Id));
        }
    }

    [Fact]
    public void KeysSurviveASaveAndALoad()
    {
        var bindings = new KeyBindings();
        bindings.Rebind("pause", "O");
        bindings.Rebind("step", "P");

        var loaded = KeyBindings.Parse(bindings.ToRaw(), _ => true);

        Assert.Equal("O", loaded.KeyOf("pause"));
        Assert.Equal("P", loaded.KeyOf("step"));
    }

    [Fact]
    public void MissingAndUnknownFileEntriesGetTheDefaults()
    {
        var raw = new Dictionary<string, string>
        {
            ["keys/fly"] = "O",
            ["keys/pause"] = "Escape",
        };

        var loaded = KeyBindings.Parse(raw, _ => true);

        Assert.Equal("P", loaded.KeyOf("pause"));
        Assert.Equal("N", loaded.KeyOf("step"));
    }

    [Fact]
    public void KeysTheGameDoesNotKnowGetTheDefaults()
    {
        var raw = new Dictionary<string, string> { ["keys/pause"] = "Hyperkey" };

        var loaded = KeyBindings.Parse(raw, key => key != "Hyperkey");

        Assert.Equal("P", loaded.KeyOf("pause"));
    }

    [Fact]
    public void ADuplicateKeyInTheFileFallsBackToTheDefault()
    {
        var raw = new Dictionary<string, string> { ["keys/step"] = "P" };

        var loaded = KeyBindings.Parse(raw, _ => true);

        Assert.Equal("P", loaded.KeyOf("pause"));
        Assert.Equal("N", loaded.KeyOf("step"));
    }
}
