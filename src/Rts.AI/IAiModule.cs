namespace Rts.AI;

/// <summary>
/// A module of a bot (docs/implementation/M5_first_bot.md, 4.6): it thinks when <see cref="AiSchedule"/> says so, and sees the world only
/// through <see cref="AiContext"/>.
/// </summary>
public interface IAiModule
{
    AiModuleKind Kind { get; }

    /// <summary>One turn of the module, after tick <see cref="AiContext.Tick"/>.</summary>
    void Update(AiContext context);
}
