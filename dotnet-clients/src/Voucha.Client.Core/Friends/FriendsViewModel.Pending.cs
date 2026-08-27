namespace Voucha.Client.Core.Friends;

public sealed partial class FriendsViewModel
{
  private bool EffectiveFollowState(string userId) =>
      localFollowStates.TryGetValue(userId, out var pendingFollowing)
          ? pendingFollowing
          : followingIds.Contains(userId);

  private bool PendingFollowState(string userId) =>
      localFollowStates.TryGetValue(userId, out var pendingFollowing) && pendingFollowing;

  private FriendRow[] ApplyPendingFollowStatesToFollowingRows(IReadOnlyList<FriendRow> rows)
  {
    var updated = rows.Select(row => row with { IsFollowing = true }).ToList();
    foreach (var (userId, following) in localFollowStates)
    {
      if (following && localFollowRows.TryGetValue(userId, out var pendingRow))
      {
        updated = AddOrReplaceFollowingRow(updated, pendingRow).ToList();
      }
      else if (!following)
      {
        updated.RemoveAll(row => StringComparer.Ordinal.Equals(row.Id, userId));
      }
    }

    return updated.ToArray();
  }
}
