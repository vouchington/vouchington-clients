using Voucha.Client.Core.Api;

namespace Voucha.Client.Core.Friends;

public sealed partial class FriendsViewModel
{
  private Task<FriendsTabPage> LoadTabPageAsync(
      FriendsTab tab,
      string userId,
      string? after,
      CancellationToken cancellationToken) =>
      tab == FriendsTab.Following
          ? LoadFollowingPageAsync(userId, after, cancellationToken)
          : LoadFollowersPageAsync(userId, after, cancellationToken);

  private async Task<FriendsTabPage> LoadFollowingPageAsync(
      string userId,
      string? after,
      CancellationToken cancellationToken)
  {
    var page = await friendsService
        .FetchFollowingAsync(userId, after, cancellationToken)
        .ConfigureAwait(true);
    return ToTabPage(page, MapRows(page.Results, forceFollowing: true));
  }

  private async Task<FriendsTabPage> LoadFollowersPageAsync(
      string userId,
      string? after,
      CancellationToken cancellationToken)
  {
    var page = await friendsService
        .FetchFollowersAsync(userId, after: after, cancellationToken: cancellationToken)
        .ConfigureAwait(true);
    return ToTabPage(page, MapFollowerRows(page.Results));
  }

  private static FriendsTabPage ToTabPage<TPage>(TPage page, IReadOnlyList<FriendRow> rows)
      where TPage : IPageOfUsers
  =>
      new(
          rows,
          page.PageInfo.EndCursor,
          page.PageInfo.HasNextPage || page.PageInfo.HasMore == true);
}
