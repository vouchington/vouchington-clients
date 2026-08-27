namespace Voucha.Client.Core.Search;

public sealed partial class OmnisearchViewModel
{
  public Task SearchInitialQueryAsync(CancellationToken cancellationToken = default)
  {
    if (!shouldSearchInitialQuery) return Task.CompletedTask;
    shouldSearchInitialQuery = false;
    return SearchAsync(cancellationToken);
  }

  public Task ApplySearchQueryAsync(string? rawQuery, CancellationToken cancellationToken = default)
  {
    shouldSearchInitialQuery = false;
    Query = rawQuery?.Trim() ?? string.Empty;
    return SearchAsync(cancellationToken);
  }

  public Task ApplyFediverseSearchQueryAsync(
      string? rawQuery,
      string? provider,
      CancellationToken cancellationToken = default)
  {
    fediverseProviders = FediverseProviderFilter(provider);
    SelectedFediverseProvider = FediverseProviderExtensions.Parse(fediverseProviders);
    return ApplySearchQueryAsync(rawQuery, cancellationToken);
  }

  public Task SelectFediverseProviderAsync(
      FediverseProvider provider,
      CancellationToken cancellationToken = default)
  {
    SelectedFediverseProvider = provider;
    fediverseProviders = provider.QueryValue();
    return SearchAsync(cancellationToken);
  }

  private void SetInitialQuery(string? initialQuery)
  {
    var trimmedInitialQuery = initialQuery?.Trim();
    if (string.IsNullOrEmpty(trimmedInitialQuery)) return;
    query = trimmedInitialQuery;
    shouldSearchInitialQuery = true;
  }
}
