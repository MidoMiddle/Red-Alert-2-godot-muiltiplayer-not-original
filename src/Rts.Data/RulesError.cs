namespace Rts.Data;

/// <summary>
/// One mistake in the data files (docs/implementation/M3_base_economy.md, 4.3): its file, its 1-based line and column, and the reason.
/// It reads <c>file:line:column: reason</c>, like a compiler message.
/// </summary>
public sealed record RulesError(string FileName, int Line, int Column, string Reason)
{
    public override string ToString() => $"{FileName}:{Line}:{Column}: {Reason}";
}
