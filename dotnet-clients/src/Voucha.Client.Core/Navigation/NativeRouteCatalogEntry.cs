namespace Voucha.Client.Core.Navigation;

public sealed record NativeRouteCatalogEntry(
    string Family,
    string RepresentativePath,
    IReadOnlyList<NativeRoutePattern> Patterns,
    NativeRouteDestinationId? DestinationId,
    string? ExclusionReason = null)
{
  public bool IsIncluded => DestinationId is not null;

  public NativeRouteMatch? Match(string pathAndQuery)
  {
    foreach (var pattern in Patterns)
    {
      if (pattern.Match(pathAndQuery) is { } match)
      {
        return match;
      }
    }

    return null;
  }

  public static NativeRouteCatalogEntry Included(
      string family,
      NativeRouteDestinationId destinationId,
      string representativePath,
      IEnumerable<string> patterns) =>
      new(family, representativePath, ToPatterns(patterns), destinationId);

  public static NativeRouteCatalogEntry Excluded(
      string family,
      string reason,
      string representativePath,
      IEnumerable<string> patterns) =>
      new(family, representativePath, ToPatterns(patterns), null, reason);

  private static NativeRoutePattern[] ToPatterns(IEnumerable<string> patterns) =>
      patterns.Select(pattern => new NativeRoutePattern(pattern)).ToArray();
}
