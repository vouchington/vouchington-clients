namespace Voucha.Client.Core.Search;

public sealed partial class OmnisearchViewModel
{
  private async Task<IReadOnlyList<OmnisearchResultGroup>> SearchCombinedAsync(
      string trimmed,
      CancellationToken cancellationToken)
  {
    var response = await client.CombinedSearchAsync(trimmed, 10, cancellationToken).ConfigureAwait(true);
    remapGroups = () => MapGroups(response, localization);
    return remapGroups();
  }

  private async Task<IReadOnlyList<OmnisearchResultGroup>> SearchFediverseAsync(
      string trimmed,
      CancellationToken cancellationToken)
  {
    var response = await client.FediverseSearchAsync(
        query: trimmed,
        providers: fediverseProviders,
        limit: 10,
        cancellationToken: cancellationToken).ConfigureAwait(true);
    remapGroups = () => MapFediverseGroups(response, localization);
    return remapGroups();
  }

  private static string? FediverseProviderFilter(string? provider) =>
      provider is "peertube" or "mastodon" or "lemmy" or "bluesky" ? provider : null;
}
