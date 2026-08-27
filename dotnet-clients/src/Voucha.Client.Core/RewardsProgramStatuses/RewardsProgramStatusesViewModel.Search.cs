using System.Diagnostics.CodeAnalysis;

namespace Voucha.Client.Core.RewardsProgramStatuses;

public sealed partial class RewardsProgramStatusesViewModel
{
  public Task RetrySearchAsync(CancellationToken cancellationToken = default) => SearchAsync(cancellationToken);

  [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Search failure is isolated from list state.")]
  public async Task SearchAsync(CancellationToken cancellationToken = default)
  {
    var query = SearchQuery.Trim();
    var generation = unchecked(++searchGeneration);
    if (query.Length == 0) { SetSearchRows([]); return; }
    SetSearchError(null);
    try
    {
      var results = await service.SearchAsync(query, cancellationToken).ConfigureAwait(true);
      if (generation != searchGeneration || SearchQuery.Trim() != query) return;
      SetSearchRows(results.Where(value => value.TopicType == "rewards_program_status"));
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    catch (Exception error) { if (generation == searchGeneration) FailSearch(error); }
  }
}
