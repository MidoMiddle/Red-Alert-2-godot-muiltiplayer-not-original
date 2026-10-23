using Godot;
using Rts.Client.App;
using Rts.Simulation.Matches;

namespace Rts.Client.UI;

/// <summary>
/// The names and the hints of the levels and the personalities of the bots (M8 step 13), for the skirmish screen and the results. A
/// personality the game has no key for — one added to the data only — shows the name the data gives it, without a hint.
/// </summary>
public static class SkirmishNames
{
    public static string Level(Node node, AiDifficulty level) => node.Tr(level switch
    {
        AiDifficulty.Easy => "UI_DIFFICULTY_EASY",
        AiDifficulty.Hard => "UI_DIFFICULTY_HARD",
        AiDifficulty.Brutal => "UI_DIFFICULTY_BRUTAL",
        _ => "UI_DIFFICULTY_MEDIUM",
    });

    public static string LevelHint(Node node, AiDifficulty level) => node.Tr(level switch
    {
        AiDifficulty.Easy => "UI_DIFFICULTY_EASY_TIP",
        AiDifficulty.Hard => "UI_DIFFICULTY_HARD_TIP",
        AiDifficulty.Brutal => "UI_DIFFICULTY_BRUTAL_TIP",
        _ => "UI_DIFFICULTY_MEDIUM_TIP",
    });

    public static string Personality(Node node, string id) => id switch
    {
        MatchPlayer.RandomPersonality => node.Tr("UI_RANDOM"),
        "balanced" => node.Tr("UI_PERSONALITY_BALANCED"),
        "aggressor" => node.Tr("UI_PERSONALITY_AGGRESSOR"),
        "builder" => node.Tr("UI_PERSONALITY_BUILDER"),
        "saboteur" => node.Tr("UI_PERSONALITY_SABOTEUR"),
        _ => DataFiles.Current.Rules.Database.TryGetPersonality(id, out var personality) ? personality.Name : id,
    };

    public static string PersonalityHint(Node node, string id) => id switch
    {
        MatchPlayer.RandomPersonality => node.Tr("UI_PERSONALITY_RANDOM_TIP"),
        "balanced" => node.Tr("UI_PERSONALITY_BALANCED_TIP"),
        "aggressor" => node.Tr("UI_PERSONALITY_AGGRESSOR_TIP"),
        "builder" => node.Tr("UI_PERSONALITY_BUILDER_TIP"),
        "saboteur" => node.Tr("UI_PERSONALITY_SABOTEUR_TIP"),
        _ => "",
    };
}
