namespace Voucha.Client.Core.Moderation;

public enum MemberAppealsRoute
{
  Tracking,
  Warnings,
  Bans,
  RemovedPosts,
  Suspension,
}

public static class MemberAppealsRoutes
{
  public static bool TryResolve(string? path, out MemberAppealsRoute route)
  {
    route = path switch
    {
      "/my/appeals" => MemberAppealsRoute.Tracking,
      "/my/warnings" => MemberAppealsRoute.Warnings,
      "/my/bans" => MemberAppealsRoute.Bans,
      "/my/removed-posts" => MemberAppealsRoute.RemovedPosts,
      "/my/account-status" => MemberAppealsRoute.Suspension,
      _ => default,
    };
    return path is "/my/appeals" or "/my/warnings" or "/my/bans"
        or "/my/removed-posts" or "/my/account-status";
  }
}
