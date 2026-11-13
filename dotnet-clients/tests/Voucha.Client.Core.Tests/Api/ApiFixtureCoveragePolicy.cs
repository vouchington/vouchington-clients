namespace Voucha.Client.Core.Tests.Api;

/// <summary>
/// Pending-aware rules shared by the fixture coverage gates. A producer may ship a fixture marked
/// <c>pending: true</c> before any client uses it; only unregistered fixtures are exempted.
/// </summary>
internal static class ApiFixtureCoveragePolicy
{
  /// <summary>Ids that must be registered but are not: unregistered fixtures that are not pending.</summary>
  public static IReadOnlyList<string> Unregistered(
      IEnumerable<FixtureCoverageCandidate> candidates,
      IEnumerable<string> registered)
  {
    var registeredIds = new HashSet<string>(registered, StringComparer.Ordinal);
    return candidates
        .Where(candidate => !candidate.Pending && !registeredIds.Contains(candidate.Id))
        .Select(candidate => candidate.Id)
        .OrderBy(id => id, StringComparer.Ordinal)
        .ToList();
  }

  /// <summary>True when a consumed fixture is exempt: pending and not registered by any client.</summary>
  public static bool IsExemptPending(FixtureCoverageCandidate candidate, IEnumerable<string> registered) =>
      candidate.Pending && !registered.Contains(candidate.Id, StringComparer.Ordinal);
}
