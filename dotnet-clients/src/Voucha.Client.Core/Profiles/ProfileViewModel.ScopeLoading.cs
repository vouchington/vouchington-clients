using Voucha.Client.Core.Api;
using Voucha.Client.Core.Communities;
using Voucha.Client.Core.Friends;
using Voucha.Client.Core.Support;
using Voucha.Client.Core.Topics;

namespace Voucha.Client.Core.Profiles;

public sealed partial class ProfileViewModel
{
  public async Task LoadPublicScopeAsync(
      string idOrUsername,
      NativeUserProfileScope scope,
      CancellationToken cancellationToken = default)
  {
    ArgumentException.ThrowIfNullOrWhiteSpace(idOrUsername);
    ArgumentNullException.ThrowIfNull(scope);
    CanEdit = false;
    Identity = null;
    ResetUserTags();
    State = LoadState.Loading;
    ErrorMessage = null;
    try
    {
      loadedIdOrUsername = idOrUsername;
      var response = await settingsService.FetchUserAsync(idOrUsername, true, cancellationToken).ConfigureAwait(true);
      ApplyProfile(response);
      ApplyScope(scope);
      await LoadProfileSafetyAsync(cancellationToken).ConfigureAwait(true);
      await LoadUserTrustContextAsync(cancellationToken).ConfigureAwait(true);
      await LoadScopeFirstPageAsync(cancellationToken).ConfigureAwait(true);
      State = LoadState.Loaded;
    }
    catch (OperationCanceledException)
    {
      State = LoadState.Idle;
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      ErrorMessage = ex.Message;
      State = LoadState.Error;
    }
  }

  public async Task SelectScopeAsync(
      NativeUserProfileCollectionKind collection,
      CancellationToken cancellationToken = default)
  {
    var requestedScope = ProfileScope;
    try
    {
      var section = SectionFor(collection);
      requestedScope = section == NativeUserProfileSection.Overview &&
          collection != NativeUserProfileCollectionKind.None
          ? NativeUserProfileScope.Overview
          : new NativeUserProfileScope(section, collection);
      if (requestedScope != ProfileScope) ApplyScope(requestedScope);
      CollectionErrorMessage = null;
      await LoadScopeFirstPageAsync(cancellationToken).ConfigureAwait(true);
    }
    catch (OperationCanceledException)
    {
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      if (requestedScope == ProfileScope) CollectionErrorMessage = ex.Message;
    }
  }

  public async Task LoadMoreAsync(CancellationToken cancellationToken = default)
  {
    if (!CanLoadMore || System.Threading.Interlocked.CompareExchange(ref loadMoreGuard, 1, 0) != 0) return;
    IsLoadingMore = true;
    OnPropertyChanged(nameof(CanLoadMore));
    try
    {
      if (IsPostsScope && User?.Id is { } userId)
      {
        var loadVersion = NextHistoryLoadVersion();
        CollectionErrorMessage = null;
        try
        {
          await LoadHistoryAsync(userId, SelectedHistoryTab, loadVersion, cancellationToken, historyPageInfo?.EndCursor, true).ConfigureAwait(true);
        }
        catch (Exception ex) when (
            loadVersion == historyLoadVersion &&
            IsPostsScope &&
            ex is VouchaApiException or HttpRequestException or InvalidOperationException)
        {
          CollectionErrorMessage = ex.Message;
        }
      }
      else
      {
        await LoadCollectionAsync(collectionPageInfo?.EndCursor, true, cancellationToken).ConfigureAwait(true);
      }
    }
    finally
    {
      System.Threading.Interlocked.Exchange(ref loadMoreGuard, 0);
      IsLoadingMore = false;
      OnPropertyChanged(nameof(CanLoadMore));
    }
  }

  private async Task LoadScopeFirstPageAsync(CancellationToken cancellationToken)
  {
    if (ProfileScope.IsOverview || ProfileScope.Section == NativeUserProfileSection.Posts)
    {
      SelectedHistoryTab = HistoryTabFor(ProfileScope.Collection);
      HistoryTabs = BuildTabs(userMetrics, SelectedHistoryTab);
      if (User?.Id is { } id)
      {
        await LoadHistoryAsync(id, SelectedHistoryTab, NextHistoryLoadVersion(), cancellationToken).ConfigureAwait(true);
      }
      return;
    }
    await LoadCollectionAsync(null, false, cancellationToken).ConfigureAwait(true);
  }

  private async Task LoadCollectionAsync(string? after, bool append, CancellationToken cancellationToken)
  {
    if (profileCollectionsService is null || User?.Id is not { } userId) return;
    var version = System.Threading.Interlocked.Increment(ref collectionLoadVersion);
    var scope = ProfileScope;
    CollectionErrorMessage = null;
    if (!append)
    {
      collectionPageInfo = null;
      OnPropertyChanged(nameof(CanLoadMore));
    }
    try
    {
      await FetchAndApplyCollectionAsync(userId, scope, after, append, version, cancellationToken).ConfigureAwait(true);
    }
    catch (OperationCanceledException)
    {
    }
    catch (Exception ex) when (ex is VouchaApiException or HttpRequestException or InvalidOperationException)
    {
      if (version == collectionLoadVersion && scope == ProfileScope) CollectionErrorMessage = ex.Message;
    }
  }
}
