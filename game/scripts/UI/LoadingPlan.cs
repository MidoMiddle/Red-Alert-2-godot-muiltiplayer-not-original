using System.Collections.Generic;

namespace Rts.Client.UI;

/// <summary>
/// The stages of the loading screen and the share of the bar each one ends at (docs/implementation/M6_menus_skirmish.md, 4.14).
/// The label shows the stage, the bar the percent. Plain data, so the tests can check the order and the percents.
/// </summary>
public static class LoadingPlan
{
    /// <summary>One stage: the key of its label and the percent of the bar once it is done.</summary>
    public readonly record struct Stage(string LabelKey, int Percent);

    public static IReadOnlyList<Stage> Stages { get; } =
    [
        new("UI_LOADING_DATA", 10),
        new("UI_LOADING_MAP", 20),
        new("UI_LOADING_TERRAIN", 40),
        new("UI_LOADING_MODELS", 70),
        new("UI_LOADING_SHADERS", 90),
        new("UI_LOADING_START", 100),
    ];

    /// <summary>Hints shown under the bar; one is picked at random each time.</summary>
    public static IReadOnlyList<string> HintKeys { get; } =
    [
        "UI_LOADING_HINT_SHIFT",
        "UI_LOADING_HINT_ESC",
        "UI_LOADING_HINT_SCROLL",
    ];
}
