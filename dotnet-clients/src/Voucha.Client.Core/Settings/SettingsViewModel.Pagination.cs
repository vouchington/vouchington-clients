using Voucha.Client.Core.Api;
using Voucha.Client.Core.Pagination;

namespace Voucha.Client.Core.Settings;

public sealed partial class SettingsViewModel
{
  private readonly CursorPaginationState<ApiKey, string> apiKeyPages = new(item => item.Id);
  private readonly CursorPaginationState<AuthSession, string> sessionPages = new(item => item.Id);
  private readonly CursorPaginationState<WebPushSubscription, string> pushSubscriptionPages = new(item => item.Id);

  public bool HasMoreApiKeys => apiKeyPages.HasLoadedPage && apiKeyPages.HasMore;
  public bool HasMoreSessions => sessionPages.HasLoadedPage && sessionPages.HasMore;
  public bool HasMorePushSubscriptions => pushSubscriptionPages.HasLoadedPage && pushSubscriptionPages.HasMore;
  public bool IsLoadingMoreApiKeys => IsLoading || apiKeyPages.IsLoading;
  public bool IsLoadingMoreSessions => IsLoading || sessionPages.IsLoading;
  public bool IsLoadingMorePushSubscriptions => IsLoading || pushSubscriptionPages.IsLoading;
  public bool HasApiKeyPaginationError => apiKeyPages.LastError is not null;
  public bool HasSessionPaginationError => sessionPages.LastError is not null;
  public bool HasPushSubscriptionPaginationError => pushSubscriptionPages.LastError is not null;

  public Task LoadMoreApiKeysAsync(CancellationToken cancellationToken = default) =>
      ContinueAsync(
          apiKeyPages,
          async (after, token) =>
          {
            var page = await settingsService.FetchApiKeysPageAsync(after, 25, token).ConfigureAwait(true);
            return (page.Results, page.PageInfo);
          },
          items => ApiKeys = items,
          NotifyApiKeyPagination,
          cancellationToken);

  public Task LoadMoreSessionsAsync(CancellationToken cancellationToken = default) =>
      ContinueAsync(
          sessionPages,
          async (after, token) =>
          {
            var page = await settingsService.FetchAuthSessionsPageAsync(after, 25, token).ConfigureAwait(true);
            return (page.Results, page.PageInfo);
          },
          items => Sessions = items,
          NotifySessionPagination,
          cancellationToken);

  public Task LoadMorePushSubscriptionsAsync(CancellationToken cancellationToken = default) =>
      ContinueAsync(
          pushSubscriptionPages,
          async (after, token) =>
          {
            var page = await settingsService.FetchPushSubscriptionsPageAsync(after, 25, token).ConfigureAwait(true);
            return (page.Results, page.PageInfo);
          },
          items => PushSubscriptions = items,
          NotifyPushSubscriptionPagination,
          cancellationToken);

  private async Task ContinueAsync<T>(
      CursorPaginationState<T, string> state,
      Func<string?, CancellationToken, Task<(IReadOnlyList<T> Items, PageInfo PageInfo)>> fetch,
      Action<IReadOnlyList<T>> apply,
      Action notify,
      CancellationToken cancellationToken)
  {
    if (IsLoading) return;
    var request = state.BeginNextPage();
    if (request is null) return;
    notify();
    try
    {
      var page = await fetch(request.Cursor, cancellationToken).ConfigureAwait(true);
      if (state.Complete(
          request,
          page.Items,
          page.PageInfo.EndCursor,
          page.PageInfo.HasNextPage || page.PageInfo.HasMore == true)) apply(state.Items);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      state.Cancel(request);
    }
    catch (OperationCanceledException ex)
    {
      state.Fail(request, ex.Message);
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      state.Fail(request, ex.Message);
    }
    finally
    {
      notify();
    }
  }

  private static void ReplacePage<T>(CursorPaginationState<T, string> state, IReadOnlyList<T> items, PageInfo pageInfo)
  {
    state.Reset(items);
    state.RestoreContinuation(pageInfo.EndCursor, pageInfo.HasNextPage || pageInfo.HasMore == true);
  }

  private void ReplaceApiKeyPage(ApiKeyListResponse response)
  {
    ReplacePage(apiKeyPages, response.Results, response.PageInfo);
    ApiKeys = apiKeyPages.Items;
    NotifyApiKeyPagination();
  }

  private void ReplaceSessionPage(AuthSessionListResponse response)
  {
    ReplacePage(sessionPages, response.Results, response.PageInfo);
    Sessions = sessionPages.Items;
    NotifySessionPagination();
  }

  private void ReplacePushSubscriptionPage(WebPushSubscriptionListResponse response)
  {
    ReplacePage(pushSubscriptionPages, response.Results, response.PageInfo);
    PushSubscriptions = pushSubscriptionPages.Items;
    NotifyPushSubscriptionPagination();
  }

  private void InvalidateSettingsPagination()
  {
    apiKeyPages.Reset(ApiKeys);
    sessionPages.Reset(Sessions);
    pushSubscriptionPages.Reset(PushSubscriptions);
    NotifyAllSettingsPagination();
  }

  private void RemoveApiKeyFromPage(string id)
  {
    apiKeyPages.Remove(item => item.Id == id);
    ApiKeys = apiKeyPages.Items;
  }

  private void RemoveSessionFromPage(string id)
  {
    sessionPages.Remove(item => item.Id == id);
    Sessions = sessionPages.Items;
  }

  private void RemovePushSubscriptionFromPage(string id)
  {
    pushSubscriptionPages.Remove(item => item.Id == id);
    PushSubscriptions = pushSubscriptionPages.Items;
  }

  private void ClearSessionPage()
  {
    sessionPages.Reset();
    sessionPages.RestoreContinuation(null, false);
    Sessions = [];
    NotifySessionPagination();
  }

  private void NotifyAllSettingsPagination()
  {
    NotifyApiKeyPagination();
    NotifySessionPagination();
    NotifyPushSubscriptionPagination();
  }

  private void NotifyApiKeyPagination()
  {
    OnPropertyChanged(nameof(HasMoreApiKeys));
    OnPropertyChanged(nameof(IsLoadingMoreApiKeys));
    OnPropertyChanged(nameof(HasApiKeyPaginationError));
  }

  private void NotifySessionPagination()
  {
    OnPropertyChanged(nameof(HasMoreSessions));
    OnPropertyChanged(nameof(IsLoadingMoreSessions));
    OnPropertyChanged(nameof(HasSessionPaginationError));
  }

  private void NotifyPushSubscriptionPagination()
  {
    OnPropertyChanged(nameof(HasMorePushSubscriptions));
    OnPropertyChanged(nameof(IsLoadingMorePushSubscriptions));
    OnPropertyChanged(nameof(HasPushSubscriptionPaginationError));
  }
}
