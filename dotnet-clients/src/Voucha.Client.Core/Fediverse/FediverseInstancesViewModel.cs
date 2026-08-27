using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Fediverse;

public sealed class FediverseInstancesViewModel(
    VouchaApiClient client,
    IUiLocalization? localization = null) : ObservableObject
{
  private readonly IUiLocalization localization = localization ?? UiLocalization.English;
  private IReadOnlyList<FediverseInstanceRow> items = [];
  private string? endCursor;
  private bool canLoadMore;
  private bool isLoading;
  private string? errorMessage;
  private int requestGeneration;
  private CancellationTokenSource? requestCts;
  private string? activeQuery;
  private const string ActiveSort = "best";

  public IReadOnlyList<FediverseInstanceRow> Items { get => items; private set => SetProperty(ref items, value); }
  public bool CanLoadMore { get => canLoadMore; private set => SetProperty(ref canLoadMore, value); }
  public bool IsLoading { get => isLoading; private set => SetProperty(ref isLoading, value); }
  public string? ErrorMessage { get => errorMessage; private set => SetProperty(ref errorMessage, value); }

  public Task LoadAsync(string? query = null, CancellationToken cancellationToken = default)
  {
    return LoadPageAsync(NormalizeQuery(query), null, append: false, cancellationToken);
  }

  public Task LoadMoreAsync(CancellationToken cancellationToken = default) =>
      CanLoadMore && !IsLoading
          ? LoadPageAsync(activeQuery, endCursor, append: true, cancellationToken)
          : Task.CompletedTask;

  private async Task LoadPageAsync(
      string? query,
      string? after,
      bool append,
      CancellationToken cancellationToken)
  {
    var generation = ++requestGeneration;
    if (requestCts is { } previousRequest)
    {
      await previousRequest.CancelAsync().ConfigureAwait(true);
      previousRequest.Dispose();
    }
    requestCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
    var requestToken = requestCts.Token;
    IsLoading = true;
    ErrorMessage = null;
    try
    {
      var response = await client.FetchFediverseInstancesAsync(
          query,
          sort: ActiveSort,
          after,
          limit: 25,
          requestToken).ConfigureAwait(true);
      if (generation != requestGeneration) return;
      var next = response.OrderedInstances.Select(item => FediverseInstanceRow.From(item, localization));
      Items = append
          ? Items.Concat(next).DistinctBy(item => item.Id).ToArray()
          : next.DistinctBy(item => item.Id).ToArray();
      if (!append) activeQuery = query;
      endCursor = response.PageInfo.EndCursor;
      CanLoadMore = response.PageInfo.HasNextPage && !string.IsNullOrWhiteSpace(endCursor);
    }
    catch (OperationCanceledException) when (requestToken.IsCancellationRequested)
    {
      if (generation == requestGeneration) ErrorMessage = null;
    }
    catch (Exception ex) when (generation == requestGeneration)
    {
      ErrorMessage = ex.Message;
    }
    finally
    {
      if (generation == requestGeneration) IsLoading = false;
    }
  }

  private static string? NormalizeQuery(string? query) =>
      string.IsNullOrWhiteSpace(query) ? null : query.Trim();
}
