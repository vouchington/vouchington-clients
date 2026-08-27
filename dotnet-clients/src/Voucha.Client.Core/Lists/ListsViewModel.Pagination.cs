using Voucha.Client.Core.Api;
using Voucha.Client.Core.Pagination;

namespace Voucha.Client.Core.Lists;

public sealed partial class ListsViewModel
{
  private readonly CursorPaginationState<ListSummaryRow, string> listPages = new(row => row.Id);
  private readonly CursorPaginationState<ListItemRow, string> itemPages = new(row => row.Id);

  public bool HasMoreLists => listPages.HasLoadedPage && listPages.HasMore;
  public bool HasMoreItems => HasSelectedList && itemPages.HasLoadedPage && itemPages.HasMore;
  public bool IsLoadingListPage => listPages.IsLoading;
  public bool IsLoadingItemPage => itemPages.IsLoading;
  public bool HasListPaginationError => listPages.LastError is not null;
  public bool HasItemPaginationError => itemPages.LastError is not null;

  public Task LoadMoreListsAsync(CancellationToken cancellationToken = default) =>
      RunContinuationAsync(() => LoadListsPageAsync(replace: false, cancellationToken));

  public Task LoadMoreItemsAsync(CancellationToken cancellationToken = default) =>
      RunContinuationAsync(() => LoadItemsPageAsync(replace: false, cancellationToken));

  private async Task LoadListsPageAsync(bool replace, CancellationToken cancellationToken)
  {
    if (replace) listPages.Reset();
    var request = listPages.BeginNextPage();
    if (request is null) return;
    NotifyPaginationChanged();
    try
    {
      var response = await client.FetchListsAsync(
          new FetchListsRequest(request.Cursor), cancellationToken).ConfigureAwait(true);
      var rows = response.Results
          .Select(reference => reference.Id)
          .Where(id => id is not null && response.Lists.ContainsKey(id))
          .Select(id => ListSummaryRow.FromList(response.Lists[id!], localization));
      if (listPages.Complete(
          request, rows, response.PageInfo.EndCursor,
          response.PageInfo.HasNextPage || response.PageInfo.HasMore == true)) Lists = listPages.Items;
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      listPages.Cancel(request);
      throw;
    }
    catch (OperationCanceledException ex)
    {
      if (!listPages.Fail(request, ex.Message)) return;
      if (replace) throw new InvalidOperationException(ex.Message, ex);
    }
    catch (Exception ex)
    {
      if (!listPages.Fail(request, ex.Message)) return;
      throw;
    }
    finally { NotifyPaginationChanged(); }
  }

  private async Task LoadItemsPageAsync(bool replace, CancellationToken cancellationToken)
  {
    if (replace) itemPages.Reset();
    if (SelectedList is not { } selected) { Items = []; return; }
    var request = itemPages.BeginNextPage();
    if (request is null) return;
    NotifyPaginationChanged();
    try
    {
      var response = await client.FetchListItemsAsync(
          new FetchListItemsRequest(
              selected.Id, MediaTypeForFilter(SelectedFilter), After: request.Cursor),
          cancellationToken).ConfigureAwait(true);
      var rows = response.Results
          .Select(reference => reference.Id)
          .Where(id => id is not null && response.ListItems.ContainsKey(id))
          .Select(id => ListItemRow.FromItem(response.ListItems[id!], localization));
      if (SelectedList?.Id == selected.Id && itemPages.Complete(
          request, rows, response.PageInfo.EndCursor,
          response.PageInfo.HasNextPage || response.PageInfo.HasMore == true)) Items = itemPages.Items;
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      itemPages.Cancel(request);
      throw;
    }
    catch (OperationCanceledException ex)
    {
      if (!itemPages.Fail(request, ex.Message)) return;
      if (replace) throw new InvalidOperationException(ex.Message, ex);
    }
    catch (Exception ex)
    {
      if (!itemPages.Fail(request, ex.Message)) return;
      throw;
    }
    finally { NotifyPaginationChanged(); }
  }

  private async Task RunContinuationAsync(Func<Task> load)
  {
    ErrorMessage = null;
    try { await load().ConfigureAwait(true); }
    catch (OperationCanceledException) { }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      ErrorMessage = ex.Message;
    }
  }

  private void SynchronizePaginationItems()
  {
    SynchronizeListPaginationItems();
    itemPages.ReplaceItems(Items);
  }

  private void SynchronizeListPaginationItems() => listPages.ReplaceItems(Lists);

  private void NotifyPaginationChanged()
  {
    OnPropertyChanged(nameof(HasMoreLists));
    OnPropertyChanged(nameof(HasMoreItems));
    OnPropertyChanged(nameof(IsLoadingListPage));
    OnPropertyChanged(nameof(IsLoadingItemPage));
    OnPropertyChanged(nameof(HasListPaginationError));
    OnPropertyChanged(nameof(HasItemPaginationError));
  }
}
