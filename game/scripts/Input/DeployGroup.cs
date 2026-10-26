namespace Rts.Client.Controls;

/// <summary>
/// D on a group of soldiers that dig in (RA2: the GI behind sandbags, the Desolator poisoning the ground; the owner, 2026-10-26): while
/// any of them stands, those standing dig in and those dug in stay so; with all of them dug in, they all stand up. Other units that deploy
/// (an MCV, a transport letting out, a wave fired once) go as before. Client state only: it picks the units the deploy is sent to.
/// </summary>
public static class DeployGroup
{
    /// <summary>
    /// Whether the deploy goes to a unit: one that does not dig in always; one that does when none of the group stands
    /// (<paramref name="anyStanding"/>: of the units that dig in, one is up), or when it stands itself.
    /// </summary>
    public static bool Sends(bool digsIn, bool dugIn, bool anyStanding) => !digsIn || !anyStanding || !dugIn;
}
