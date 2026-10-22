using System;
using System.Collections.Generic;
using System.Linq;

namespace Rts.Data;

/// <summary>
/// The data files cannot be loaded (docs/implementation/M3_base_economy.md, 4.3). <see cref="Errors"/> holds every mistake that was found,
/// in the order of the files and then of the lines; the message lists them one per line.
/// </summary>
public sealed class RulesException(IReadOnlyList<RulesError> errors)
    : Exception(string.Join("\n", errors.Select(error => error.ToString())))
{
    public IReadOnlyList<RulesError> Errors { get; } = errors;
}
