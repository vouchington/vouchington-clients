namespace Voucha.Client.Core.Friends;

internal sealed record FriendsTabPage(
    IReadOnlyList<FriendRow> Rows,
    string? Cursor,
    bool HasMore)
{
  public FriendsTabPage WithRows(IReadOnlyList<FriendRow> rows) =>
      this with { Rows = rows };
}
