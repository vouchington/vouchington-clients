using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Friends;

public sealed partial class FriendsViewModel
{
  private async Task LoadSelectedTabAsync(bool forceReload, CancellationToken cancellationToken)
  {
    var userId = currentUserId;
    if (userId is null)
    {
      ResetEmptyState();
      return;
    }

    var requestId = unchecked(++loadRequestId);
    State = LoadState.Loading;
    ErrorMessage = null;

    try
    {
      if (!forceReload && tabCache.TryGetValue(SelectedTab, out var cached))
      {
        ApplyTabCache(SelectedTab, cached);
        State = LoadState.Loaded;
        return;
      }

      if (!forceReload)
      {
        Items = [];
        HasMore = false;
      }
      var page = await LoadTabPageAsync(SelectedTab, userId, null, cancellationToken)
          .ConfigureAwait(true);
      if (requestId != loadRequestId) return;
      ApplyTabCache(SelectedTab, page);
      State = LoadState.Loaded;
    }
    catch (OperationCanceledException)
    {
      if (requestId != loadRequestId) return;
      State = Items.Count == 0 ? LoadState.Idle : LoadState.Loaded;
    }
    catch (VouchaApiException ex)
    {
      CompleteLoadError(requestId, ex.Message);
    }
    catch (HttpRequestException ex)
    {
      CompleteLoadError(requestId, ex.Message);
    }
    catch (InvalidOperationException ex)
    {
      CompleteLoadError(requestId, ex.Message);
    }
  }

  private async Task LoadMoreSelectedTabAsync(CancellationToken cancellationToken)
  {
    var userId = currentUserId;
    if (userId is null || State == LoadState.Loading) return;
    if (!tabCache.TryGetValue(SelectedTab, out var cached) || !cached.HasMore) return;
    if (cached.Cursor is null) return;

    var requestId = unchecked(++loadRequestId);
    State = LoadState.Loading;
    ErrorMessage = null;

    try
    {
      var page = await LoadTabPageAsync(SelectedTab, userId, cached.Cursor, cancellationToken)
          .ConfigureAwait(true);
      if (requestId != loadRequestId) return;
      ApplyTabCache(
          SelectedTab,
          page.WithRows([.. cached.Rows, .. page.Rows]));
      State = LoadState.Loaded;
    }
    catch (OperationCanceledException)
    {
      if (requestId != loadRequestId) return;
      State = Items.Count == 0 ? LoadState.Idle : LoadState.Loaded;
    }
    catch (VouchaApiException ex)
    {
      CompleteLoadError(requestId, ex.Message, preserveItems: true);
    }
    catch (HttpRequestException ex)
    {
      CompleteLoadError(requestId, ex.Message, preserveItems: true);
    }
    catch (InvalidOperationException ex)
    {
      CompleteLoadError(requestId, ex.Message, preserveItems: true);
    }
  }

  private FriendRow[] MapRows(IReadOnlyList<User> users, bool forceFollowing)
  {
    return users
        .Select(user => MapRow(user, forceFollowing || EffectiveFollowState(user.Id)))
        .ToArray();
  }

  private FriendRow[] MapFollowerRows(IReadOnlyList<User> users) =>
      users
          .Select(user => MapRow(user, EffectiveFollowState(user.Id)))
          .ToArray();

  private FriendRow MapRow(User user, bool isFollowing) =>
      new(
          user.Id,
          DisplayNameFor(user),
          string.IsNullOrWhiteSpace(user.Username) ? null : user.Username,
          user.Username,
          user.ProfileImageId,
          isFollowing,
          IsSelf(user.Id));

  private bool IsSelf(string userId) =>
      currentUserId is not null && StringComparer.Ordinal.Equals(currentUserId, userId);

  private void ApplyTabCache(FriendsTab tab, FriendsTabPage page)
  {
    var rows = page.Rows;
    var normalizedRows = (tab == FriendsTab.Followers
        ? rows.Select(row => row with { IsFollowing = EffectiveFollowState(row.Id) }).ToArray()
        : ApplyPendingFollowStatesToFollowingRows(rows))
        .Select(row => row with { Localization = localization })
        .ToArray();
    var normalizedPage = page.WithRows(normalizedRows);
    tabCache[tab] = normalizedPage;
    if (tab == FriendsTab.Following)
    {
      followingIds.Clear();
      foreach (var row in normalizedRows)
      {
        followingIds.Add(row.Id);
      }

      RefreshFollowersCacheFollowStates();
    }

    if (SelectedTab == tab)
    {
      Items = normalizedRows;
      HasMore = normalizedPage.HasMore;
    }
  }

  private void RefreshFollowersCacheFollowStates()
  {
    if (!tabCache.TryGetValue(FriendsTab.Followers, out var cached)) return;
    var refreshed = cached.Rows
        .Select(row => row with { IsFollowing = EffectiveFollowState(row.Id) })
        .ToArray();
    tabCache[FriendsTab.Followers] = cached.WithRows(refreshed);
    if (SelectedTab == FriendsTab.Followers)
    {
      Items = refreshed;
      HasMore = tabCache[FriendsTab.Followers].HasMore;
    }
  }

  private void CompleteLoadError(int requestId, string message, bool preserveItems = false)
  {
    if (requestId != loadRequestId) return;
    if (!preserveItems)
    {
      tabCache.Remove(SelectedTab);
      Items = [];
      HasMore = false;
    }
    ErrorMessage = message;
    State = LoadState.Error;
  }

  private void ResetEmptyState()
  {
    Items = [];
    ErrorMessage = null;
    State = LoadState.Loaded;
    HasMore = false;
    followingIds.Clear();
    tabCache.Clear();
    localFollowStates.Clear();
    localFollowRows.Clear();
    pendingFollowMutationIds.Clear();
  }

}
