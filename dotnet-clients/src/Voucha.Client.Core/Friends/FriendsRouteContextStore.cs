namespace Voucha.Client.Core.Friends;

public sealed class FriendsRouteContextStore
{
  private object? tab;

  public void Set(FriendsTab next) =>
      Interlocked.Exchange(ref tab, next);

  public void Clear() =>
      Interlocked.Exchange(ref tab, null);

  public FriendsTab? Consume() =>
      (FriendsTab?)Interlocked.Exchange(ref tab, null);
}
