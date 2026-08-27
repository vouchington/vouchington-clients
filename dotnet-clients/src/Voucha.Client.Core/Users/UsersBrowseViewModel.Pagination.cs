using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Users;

public sealed partial class UsersBrowseViewModel
{
  private const int PageLimit = 25;

  public bool HasMoreResults => pagination.HasLoadedPage && pagination.HasMore;
  public bool IsLoadingMoreResults => pagination.IsLoading;
  public bool HasMoreResultsError => pagination.LastError is not null;

  public async Task SearchAsync(CancellationToken cancellationToken = default)
  {
    var requested = Query.Trim();
    var current = ++generation;
    SearchError = null;
    if (requested.Length == 0)
    {
      ResetResults();
      return;
    }

    IsSearching = true;
    try
    {
      var response = await service.SearchUsersAsync(
          new SearchUsersRequest(requested, Limit: PageLimit),
          cancellationToken).ConfigureAwait(true);
      if (current != generation) return;
      pagination.Reset(response.Results);
      pagination.RestoreContinuation(
          response.PageInfo.EndCursor,
          response.PageInfo.HasNextPage || response.PageInfo.HasMore == true);
      NotifyResultsPagination();
    }
    catch (OperationCanceledException) { throw; }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      if (current == generation)
      {
        ResetResults();
        SearchError = Failure(ex, UiMessageKey.NativeSwiftMembershipSearchFailure);
      }
    }
    finally { if (current == generation) IsSearching = false; }
  }

  public async Task LoadMoreResultsAsync(CancellationToken cancellationToken = default)
  {
    if (!pagination.HasLoadedPage) return;
    var request = pagination.BeginNextPage();
    if (request is null) return;
    NotifyResultsPagination();
    try
    {
      var response = await service.SearchUsersAsync(
          new SearchUsersRequest(Query.Trim(), request.Cursor, PageLimit),
          cancellationToken).ConfigureAwait(true);
      pagination.Complete(
          request,
          response.Results,
          response.PageInfo.EndCursor,
          response.PageInfo.HasNextPage || response.PageInfo.HasMore == true);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      pagination.Cancel(request);
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      pagination.Fail(request, ex.Message);
    }
    finally
    {
      NotifyResultsPagination();
    }
  }

  private void ResetResults()
  {
    pagination.Reset();
    NotifyResultsPagination();
  }

  private void NotifyResultsPagination()
  {
    OnPropertyChanged(nameof(Results));
    OnPropertyChanged(nameof(HasMoreResults));
    OnPropertyChanged(nameof(IsLoadingMoreResults));
    OnPropertyChanged(nameof(HasMoreResultsError));
  }
}
