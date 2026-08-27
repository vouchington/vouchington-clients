using System.Diagnostics.CodeAnalysis;

namespace Voucha.Client.Core.PointValuations;

public sealed partial class PointValuationsViewModel
{
  public Task RetrySearchAsync(CancellationToken cancellationToken = default) => SearchAsync(cancellationToken);

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Search failures are presentation state.")]
  public async Task SearchAsync(CancellationToken cancellationToken = default)
  {
    var query = SearchQuery.Trim();
    var generation = unchecked(++searchGeneration);
    if (query.Length == 0) { SetSearchResults([]); return; }
    SetSearchError(null);
    try
    {
      var results = await service.SearchAsync(query, cancellationToken).ConfigureAwait(true);
      if (generation == searchGeneration && SearchQuery.Trim() == query)
        SetSearchResults(results.Where(item => valuations.All(value => value.RewardsProgramId != item.Id)));
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    catch (Exception error) { if (generation == searchGeneration) FailSearch(error); }
  }
}
