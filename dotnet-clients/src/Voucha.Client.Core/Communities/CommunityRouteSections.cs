namespace Voucha.Client.Core.Communities;

public static class CommunityRouteSections
{
  public static CommunityDetailSurfaceSection ForPath(string path)
  {
    ArgumentNullException.ThrowIfNull(path);

    var sectionPath = CommunitySectionPath(path);

    return sectionPath switch
    {
      var value when value.EndsWith("/members", StringComparison.OrdinalIgnoreCase) => CommunityDetailSurfaceSection.Members,
      var value when value.EndsWith("/posts/pending", StringComparison.OrdinalIgnoreCase) => CommunityDetailSurfaceSection.Moderation,
      var value when value.EndsWith("/posts", StringComparison.OrdinalIgnoreCase) => CommunityDetailSurfaceSection.Posts,
      var value when value.EndsWith("/news", StringComparison.OrdinalIgnoreCase) ||
          value.Contains("/news/", StringComparison.OrdinalIgnoreCase) => CommunityDetailSurfaceSection.News,
      var value when value.EndsWith("/lists", StringComparison.OrdinalIgnoreCase) ||
          value.Contains("/lists/", StringComparison.OrdinalIgnoreCase) => CommunityDetailSurfaceSection.Lists,
      var value when value.EndsWith("/settings", StringComparison.OrdinalIgnoreCase) => CommunityDetailSurfaceSection.Settings,
      var value when value.EndsWith("/settings/pinned-posts", StringComparison.OrdinalIgnoreCase) => CommunityDetailSurfaceSection.PinnedPosts,
      var value when value.EndsWith("/settings/applications", StringComparison.OrdinalIgnoreCase) => CommunityDetailSurfaceSection.Applications,
      var value when value.EndsWith("/settings/invites", StringComparison.OrdinalIgnoreCase) => CommunityDetailSurfaceSection.Invites,
      var value when value.EndsWith("/settings/modlog", StringComparison.OrdinalIgnoreCase) ||
          value.EndsWith("/modlog", StringComparison.OrdinalIgnoreCase) => CommunityDetailSurfaceSection.Modlog,
      var value when value.EndsWith("/settings/moderation/modmail", StringComparison.OrdinalIgnoreCase) ||
          value.Contains("/settings/moderation/modmail/", StringComparison.OrdinalIgnoreCase) ||
          value.EndsWith("/modmail", StringComparison.OrdinalIgnoreCase) ||
          value.Contains("/modmail/", StringComparison.OrdinalIgnoreCase) => CommunityDetailSurfaceSection.Modmail,
      var value when value.EndsWith("/settings/moderation/analytics", StringComparison.OrdinalIgnoreCase) ||
          value.EndsWith("/moderation-analytics", StringComparison.OrdinalIgnoreCase) => CommunityDetailSurfaceSection.ModerationAnalytics,
      var value when value.EndsWith("/settings/moderation", StringComparison.OrdinalIgnoreCase) => CommunityDetailSurfaceSection.Moderation,
      var value when value.EndsWith("/pinned-posts", StringComparison.OrdinalIgnoreCase) => CommunityDetailSurfaceSection.PinnedPosts,
      var value when value.EndsWith("/applications", StringComparison.OrdinalIgnoreCase) => CommunityDetailSurfaceSection.Applications,
      var value when value.EndsWith("/invites", StringComparison.OrdinalIgnoreCase) => CommunityDetailSurfaceSection.Invites,
      var value when value.EndsWith("/bans", StringComparison.OrdinalIgnoreCase) => CommunityDetailSurfaceSection.Bans,
      var value when value.EndsWith("/restrictions", StringComparison.OrdinalIgnoreCase) => CommunityDetailSurfaceSection.Restrictions,
      var value when value.EndsWith("/moderator-vacation", StringComparison.OrdinalIgnoreCase) => CommunityDetailSurfaceSection.ModeratorVacation,
      var value when value.EndsWith("/ai-agents", StringComparison.OrdinalIgnoreCase) => CommunityDetailSurfaceSection.AiAgents,
      var value when value.EndsWith("/agent-prompts", StringComparison.OrdinalIgnoreCase) => CommunityDetailSurfaceSection.AgentPrompts,
      var value when value.Contains("/moderation-", StringComparison.OrdinalIgnoreCase) ||
          value.EndsWith("/moderator-stats", StringComparison.OrdinalIgnoreCase) ||
          value.Contains("/automod/", StringComparison.OrdinalIgnoreCase) => CommunityDetailSurfaceSection.Moderation,
      _ => CommunityDetailSurfaceSection.Overview,
    };
  }

  private static string CommunitySectionPath(string path)
  {
    var routePath = path.Split(['?', '#'], 2)[0].TrimEnd('/');
    var prefix = "/communities/";
    var prefixIndex = routePath.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
    if (prefixIndex < 0)
    {
      return routePath;
    }

    var afterPrefix = routePath[(prefixIndex + prefix.Length)..];
    var nextSlashIndex = afterPrefix.IndexOf('/', StringComparison.Ordinal);
    return nextSlashIndex < 0 ? "" : afterPrefix[nextSlashIndex..];
  }
}
