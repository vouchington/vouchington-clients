using Voucha.Client.Core.Api;
using Voucha.Client.Core.Support;

namespace Voucha.Client.Core.Friends;

public sealed partial class FriendsViewModel
{
  private async Task ToggleFollowCoreAsync(FriendRow row, CancellationToken cancellationToken)
  {
    if (row.IsSelf || currentUserId is null) return;
    if (!pendingFollowMutationIds.Add(row.Id)) return;

    _ = unchecked(++loadRequestId);
    var requestId = unchecked(++mutationRequestId);
    var nextFollowing = !row.IsFollowing;
    var mutationUserId = currentUserId;
    ApplyOptimisticFollowChange(row, nextFollowing);

    try
    {
      await friendsService.SetFollowAsync(row.Id, nextFollowing, cancellationToken).ConfigureAwait(true);
      if (requestId != mutationRequestId) return;
      State = LoadState.Loaded;
      ErrorMessage = null;
    }
    catch (OperationCanceledException)
    {
      CompleteMutationError(row, nextFollowing, mutationUserId, requestId, null);
    }
    catch (VouchaApiException ex)
    {
      CompleteMutationError(row, nextFollowing, mutationUserId, requestId, ex.Message);
    }
    catch (HttpRequestException ex)
    {
      CompleteMutationError(row, nextFollowing, mutationUserId, requestId, ex.Message);
    }
    catch (InvalidOperationException ex)
    {
      CompleteMutationError(row, nextFollowing, mutationUserId, requestId, ex.Message);
    }
    finally
    {
      if (StringComparer.Ordinal.Equals(mutationUserId, currentUserId))
      {
        pendingFollowMutationIds.Remove(row.Id);
        localFollowStates.Remove(row.Id);
        localFollowRows.Remove(row.Id);
      }
    }
  }

  private void ApplyOptimisticFollowChange(FriendRow row, bool following)
  {
    followingIds.Remove(row.Id);
    if (following)
    {
      followingIds.Add(row.Id);
    }
    localFollowStates[row.Id] = following;
    localFollowRows[row.Id] = row;

    if (tabCache.TryGetValue(FriendsTab.Following, out var followingPage))
    {
      var followingRows = following
          ? AddOrReplaceFollowingRow(followingPage.Rows, row)
          : followingPage.Rows
              .Where(item => !StringComparer.Ordinal.Equals(item.Id, row.Id))
              .ToArray();
      tabCache[FriendsTab.Following] = followingPage.WithRows(followingRows);
    }

    if (tabCache.TryGetValue(FriendsTab.Followers, out var followerPage))
    {
      var followerRows = followerPage.Rows
          .Select(item => StringComparer.Ordinal.Equals(item.Id, row.Id) ? item with { IsFollowing = following } : item)
          .ToArray();
      tabCache[FriendsTab.Followers] = followerPage.WithRows(followerRows);
    }

    SyncSelectedItems(row, following);
  }

  private FriendRow[] AddOrReplaceFollowingRow(IReadOnlyList<FriendRow> rows, FriendRow row)
  {
    var updated = rows
        .Select(item => StringComparer.Ordinal.Equals(item.Id, row.Id) ? item with { IsFollowing = true } : item)
        .ToList();
    if (updated.All(item => !StringComparer.Ordinal.Equals(item.Id, row.Id)))
    {
      return [.. updated, row with { IsFollowing = true, IsSelf = IsSelf(row.Id) }];
    }

    return updated.ToArray();
  }

  private void SyncSelectedItems(FriendRow row, bool following)
  {
    if (SelectedTab == FriendsTab.Following)
    {
      Items = following
          ? AddOrReplaceFollowingRow(Items, row)
          : Items.Where(item => !StringComparer.Ordinal.Equals(item.Id, row.Id)).ToArray();
    }
    else if (SelectedTab == FriendsTab.Followers)
    {
      Items = Items
          .Select(item => StringComparer.Ordinal.Equals(item.Id, row.Id) ? item with { IsFollowing = following } : item)
          .ToArray();
    }
  }

  private void CompleteMutationError(
      FriendRow row,
      bool attemptedFollowing,
      string? mutationUserId,
      int requestId,
      string? message)
  {
    if (StringComparer.Ordinal.Equals(mutationUserId, currentUserId))
    {
      ApplyOptimisticFollowChange(row, !attemptedFollowing);
    }
    if (requestId != mutationRequestId) return;
    ErrorMessage = message;
    State = message is null
        ? (Items.Count == 0 ? LoadState.Idle : LoadState.Loaded)
        : LoadState.Error;
  }
}
