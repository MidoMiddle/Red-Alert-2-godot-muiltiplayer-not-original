using Rts.Client.UI;

namespace Rts.Simulation.Tests.Client;

/// <summary>
/// The stages of the loading screen (docs/implementation/M6_menus_skirmish.md, 4.14): six stages, and the bar ends each one at its percent.
/// </summary>
public sealed class LoadingPlanTests
{
    [Fact]
    public void Stages_end_the_bar_at_the_percents_of_the_plan()
    {
        Assert.Equal(new[] { 10, 20, 40, 70, 90, 100 }, LoadingPlan.Stages.Select(stage => stage.Percent).ToArray());
    }

    [Fact]
    public void The_last_stage_fills_the_bar()
    {
        Assert.Equal(100, LoadingPlan.Stages[^1].Percent);
    }

    [Fact]
    public void Every_stage_has_its_own_label_key()
    {
        var keys = LoadingPlan.Stages.Select(stage => stage.LabelKey).ToList();

        Assert.All(keys, key => Assert.Matches("^UI_LOADING_[A-Z]+$", key));
        Assert.Equal(keys.Count, keys.Distinct().Count());
    }

    [Fact]
    public void Hints_are_keys_of_the_loading_screen()
    {
        Assert.NotEmpty(LoadingPlan.HintKeys);
        Assert.All(LoadingPlan.HintKeys, key => Assert.StartsWith("UI_LOADING_HINT_", key));
    }
}
