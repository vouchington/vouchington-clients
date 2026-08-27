namespace Voucha.Client.Core.Navigation;

public static partial class NativeRouteCatalog
{
  private static readonly Lazy<NativeRouteCatalogEntry[]> EntryCache = new(CreateEntries);

  public static IReadOnlyList<NativeRouteCatalogEntry> Entries => EntryCache.Value;

  private static NativeRouteCatalogEntry[] CreateEntries() =>
      FeedEntries()
          .Concat(EntityEntries())
          .Concat(StaffEntries())
          .Concat(AccountEntries())
          .Concat(ExcludedEntries())
          .ToArray();

  public static (NativeRouteCatalogEntry Entry, NativeRouteMatch Match)? MatchingRoute(string pathAndQuery)
  {
    foreach (var entry in Entries)
    {
      if (entry.Match(pathAndQuery) is { } match)
      {
        return (entry, match);
      }
    }

    return null;
  }

  private static NativeRouteCatalogEntry Included(
      string family,
      NativeRouteDestinationId destinationId,
      string representativePath,
      IEnumerable<string> patterns) =>
      NativeRouteCatalogEntry.Included(family, destinationId, representativePath, patterns);

  private static NativeRouteCatalogEntry Excluded(
      string family,
      string reason,
      string representativePath,
      IEnumerable<string> patterns) =>
      NativeRouteCatalogEntry.Excluded(family, reason, representativePath, patterns);
}
