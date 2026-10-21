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
        var raw = new Dictionary<string, string> { ["keys/pause"] = "P", ["keys/step"] = "P" };

        var loaded = KeyBindings.Parse(raw, _ => true);

        Assert.Equal("P", loaded.KeyOf("pause"));
        Assert.Equal("N", loaded.KeyOf("step"));
    }

    [Fact]
    public void TheDefaultsAreTheRa2Layout()
    {
        var bindings = new KeyBindings();

        Assert.Equal(["Up", "Down", "Left", "Right"], new[] { "camera_up", "camera_down", "camera_left", "camera_right" }.Select(bindings.KeyOf));
        Assert.Equal("S", bindings.KeyOf("stop"));
        Assert.Equal("X", bindings.KeyOf("scatter"));
        Assert.Equal("Z", bindings.KeyOf("waypoint_mode"));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("9")]
    public void DigitsBelongToTheControlGroups(string key)
    {
        Assert.Equal(RebindStatus.Refused, new KeyBindings().Rebind("pause", key).Status);
    }

    [Fact]
    public void AnOldFileWithTheWasdCameraGetsTheArrowsAndTheOrderKeys()
    {
        var raw = new Dictionary<string, string>
        {
            ["keys/camera_up"] = "W",
            ["keys/camera_down"] = "S",
            ["keys/camera_left"] = "A",
            ["keys/camera_right"] = "D",
            ["keys/pause"] = "O",
        };

        var loaded = KeyBindings.Parse(raw, _ => true);

        Assert.Equal("Up", loaded.KeyOf("camera_up"));
        Assert.Equal("Down", loaded.KeyOf("camera_down"));
        Assert.Equal("S", loaded.KeyOf("stop"));
        Assert.Equal("O", loaded.KeyOf("pause"));
        Assert.Empty(loaded.Unbound());
    }

    [Fact]
    public void AnOldFileKeepsCameraKeysThePlayerChose()
    {
        var raw = new Dictionary<string, string> { ["keys/camera_up"] = "I" };

        Assert.Equal("I", KeyBindings.Parse(raw, _ => true).KeyOf("camera_up"));
    }

    [Fact]
    public void ANewFileKeepsWasdForTheCamera()
    {
        var bindings = new KeyBindings();
        bindings.Rebind("camera_up", "W");

        Assert.Equal("W", KeyBindings.Parse(bindings.ToRaw(), _ => true).KeyOf("camera_up"));
    }

    [Fact]
    public void AFileKeyTakesTheDefaultOfANewActionWhichIsLeftWithoutAKey()
    {
        // An M6 file where pause is on X: scatter, new in M2 step 13, cannot have X.
        var raw = new Dictionary<string, string> { ["keys/pause"] = "X" };

        var loaded = KeyBindings.Parse(raw, _ => true);

        Assert.Equal("X", loaded.KeyOf("pause"));
        Assert.Equal(string.Empty, loaded.KeyOf("scatter"));
        Assert.Equal(["scatter"], loaded.Unbound());
    }

    [Fact]
    public void NoTwoActionsEverShareAKeyFromAFile()
    {
        var random = new System.Random(13);
        var keys = new[] { "P", "N", "S", "X", "Z", "Up", "Down", "W", "E", "T", "F3", "F5" };
        for (var round = 0; round < 200; round++)
        {
            var raw = new Dictionary<string, string>();
            if (random.Next(2) == 0)
            {
                raw["keys/layout"] = "2";
            }

            foreach (var action in KeyBindings.Actions)
            {
                if (random.Next(3) == 0)
                {
                    raw["keys/" + action.Id] = keys[random.Next(keys.Length)];
                }
            }

            var loaded = KeyBindings.Parse(raw, _ => true);
            var bound = KeyBindings.Actions.Select(action => loaded.KeyOf(action.Id)).Where(key => key.Length > 0).ToList();
            Assert.Equal(bound.Count, bound.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }
    }

    [Fact]
    public void TypeSelectIsT_SpawnIsB_AndGuardAndDeployAreBound()
    {
        var bindings = new KeyBindings();

        Assert.Equal("T", bindings.KeyOf("type_select"));
        Assert.Equal("B", bindings.KeyOf("spawn"));
        Assert.Equal("G", bindings.KeyOf("guard"));
        Assert.Equal("D", bindings.KeyOf("deploy"));
        Assert.Empty(bindings.Unbound());
    }

    [Fact]
    public void AnOlderFileWithSpawnOnTKeepsTypeSelectOnT()
    {
        var raw = new Dictionary<string, string>
        {
            ["keys/layout"] = "2",
            ["keys/spawn_type"] = "T",
        };

        var loaded = KeyBindings.Parse(raw, _ => true);

        Assert.Equal("T", loaded.KeyOf("type_select"));
        Assert.Equal("B", loaded.KeyOf("spawn"));
        Assert.Empty(loaded.Unbound());
    }

    [Theory]
    [InlineData("F1")]
    [InlineData("F2")]
    [InlineData("F3")]
    [InlineData("F4")]
    public void F1ToF4BelongToTheCameraBookmarks(string key)
    {
        Assert.Equal(RebindStatus.Refused, new KeyBindings().Rebind("pause", key).Status);
    }

    [Fact]
    public void TheDebugOverlaysAreOnF6ToF8()
    {
        var bindings = new KeyBindings();

        Assert.Equal("F6", bindings.KeyOf("debug_path"));
        Assert.Equal("F7", bindings.KeyOf("debug_cells"));
        Assert.Equal("F8", bindings.KeyOf("debug_queue"));
    }

    [Fact]
    public void ASavedF3PathKeyMovesToF6()
    {
        var bindings = new KeyBindings();
        var raw = bindings.ToRaw().ToDictionary(pair => pair.Key, pair => pair.Value);
        raw["keys/debug_path"] = "F3";

        Assert.Equal("F6", KeyBindings.Parse(raw, _ => true).KeyOf("debug_path"));
    }
}
