using System.Net;
using Voucha.Client.Core.Api;
using Voucha.Client.Core.Localization;
using Voucha.Client.Core.Pagination;

namespace Voucha.Client.Core.Settings;

public sealed partial class SettingsViewModel
{
  private readonly CursorPaginationState<OAuthGrant, string> oauthGrantPages = new(grant => grant.Id);
  private readonly HashSet<string> revokedGrantIds = new(StringComparer.Ordinal);
  private readonly HashSet<string> revokingGrantIds = new(StringComparer.Ordinal);
  private bool replaceOAuthGrantsOnRetry;
  private UiMessageKey? oauthGrantNoticeKey;
  public IReadOnlyList<OAuthGrant> OAuthGrants => oauthGrantPages.Items;
  public IReadOnlyList<SettingsOAuthGrantRow> LocalizedOAuthGrants => OAuthGrants
      .Select(grant => new SettingsOAuthGrantRow(grant, localization)).ToArray();
  public bool HasNoOAuthGrants => oauthGrantPages.HasLoadedPage && !oauthGrantPages.HasMore && OAuthGrants.Count == 0;
  public bool HasMoreOAuthGrants => oauthGrantPages.HasMore && (oauthGrantPages.HasLoadedPage || oauthGrantPages.LastError is not null);
  public bool IsLoadingMoreOAuthGrants => IsLoading || oauthGrantPages.IsLoading;
  public bool HasOAuthGrantPaginationError => oauthGrantPages.LastError is not null;
  public string? OAuthGrantNotice => oauthGrantNoticeKey is { } key ? localization.Localize(key) : null;

  private Task LoadOAuthGrantsAsync(CancellationToken cancellationToken)
  {
    oauthGrantPages.Reset(OAuthGrants);
    replaceOAuthGrantsOnRetry = true;
    return FetchOAuthGrantPageAsync(true, cancellationToken);
  }

  public Task LoadMoreOAuthGrantsAsync(CancellationToken cancellationToken = default) =>
      IsLoading ? Task.CompletedTask : FetchOAuthGrantPageAsync(replaceOAuthGrantsOnRetry, cancellationToken);

  private async Task FetchOAuthGrantPageAsync(bool replace, CancellationToken cancellationToken)
  {
    var request = oauthGrantPages.BeginNextPage();
    if (request is null) return;
    NotifyOAuthGrantPagination();
    try
    {
      var response = await settingsService.FetchOAuthGrantsAsync(request.Cursor, 25, cancellationToken).ConfigureAwait(true);
      var items = response.Results.Where(grant => !revokedGrantIds.Contains(grant.Id));
      var hasMore = response.PageInfo.HasNextPage || response.PageInfo.HasMore == true;
      var completed = replace
          ? oauthGrantPages.CompleteReplacing(request, items, response.PageInfo.EndCursor, hasMore)
          : oauthGrantPages.Complete(request, items, response.PageInfo.EndCursor, hasMore);
      if (completed && replace) replaceOAuthGrantsOnRetry = false;
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
      oauthGrantPages.Cancel(request);
    }
    catch (OperationCanceledException ex)
    {
      oauthGrantPages.Fail(request, ex.Message);
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      oauthGrantPages.Fail(request, ex.Message);
    }
    finally
    {
      NotifyOAuthGrantPagination();
    }
  }

  public async Task RevokeOAuthGrantAsync(OAuthGrant grant, CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(grant);
    if (!revokingGrantIds.Add(grant.Id)) return;
    SetOAuthGrantNotice(null);
    try
    {
      await settingsService.RevokeOAuthGrantAsync(grant.Id, cancellationToken).ConfigureAwait(true);
      ReconcileRevokedOAuthGrant(grant.Id);
    }
    catch (VouchaApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
    {
      ReconcileRevokedOAuthGrant(grant.Id);
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    catch (OperationCanceledException)
    {
      SetOAuthGrantNotice(UiMessageKey.NativeCredentialsRevokeFailed);
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      SetOAuthGrantNotice(UiMessageKey.NativeCredentialsRevokeFailed);
    }
    finally
    {
      revokingGrantIds.Remove(grant.Id);
      NotifyOAuthGrantPagination();
    }
  }

  private void ReconcileRevokedOAuthGrant(string id)
  {
    revokedGrantIds.Add(id);
    oauthGrantPages.Remove(item => item.Id == id);
    SetOAuthGrantNotice(UiMessageKey.NativeCredentialsGrantRevoked);
  }

  private void NotifyOAuthGrantPagination()
  {
    OnPropertyChanged(nameof(OAuthGrants));
    OnPropertyChanged(nameof(LocalizedOAuthGrants));
    OnPropertyChanged(nameof(HasNoOAuthGrants));
    OnPropertyChanged(nameof(HasMoreOAuthGrants));
    OnPropertyChanged(nameof(IsLoadingMoreOAuthGrants));
    OnPropertyChanged(nameof(HasOAuthGrantPaginationError));
  }

  private void SetOAuthGrantNotice(UiMessageKey? key) =>
      SetProperty(ref oauthGrantNoticeKey, key, nameof(OAuthGrantNotice));
}
