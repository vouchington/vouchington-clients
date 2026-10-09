namespace Voucha.Client.Core.Navigation;

public static partial class NativeDeepLinkResolver
{
  private static bool IsVisible(string? intentId, bool requiresAuthentication, NavigationViewer viewer)
  {
    if (intentId is null)
    {
      return !requiresAuthentication || viewer.IsAuthenticated;
    }

    if (requiresAuthentication && !viewer.IsAuthenticated)
    {
      return false;
    }

    var intent = NavigationCatalog.All.FirstOrDefault(item => item.Id == intentId);
    return intent is not null && NavigationCatalog.IsVisible(intent, viewer);
  }

  private static string? IntentIdFor(NativeRouteDestinationId destinationId, NativeRouteMatch match) =>
      destinationId switch
      {
        NativeRouteDestinationId.SignIn => null,
        NativeRouteDestinationId.FeedNews => NavigationCatalog.NewsIntentId,
        NativeRouteDestinationId.FeedPodcasts => "podcasts",
        NativeRouteDestinationId.FeedVideos => "videos",
        NativeRouteDestinationId.FediverseSearch or NativeRouteDestinationId.FediverseInstances => "fediverse",
        NativeRouteDestinationId.TopicDetail when match.Path.StartsWith("/instance/", StringComparison.Ordinal) => "fediverse",
        NativeRouteDestinationId.FeedPosts or NativeRouteDestinationId.PostsBrowse or NativeRouteDestinationId.PostDetail or
            NativeRouteDestinationId.PostCompose or NativeRouteDestinationId.StoriesBrowse => "posts",
        NativeRouteDestinationId.TopicsBrowse or NativeRouteDestinationId.TopicDetail or NativeRouteDestinationId.TopicManagement or
            NativeRouteDestinationId.TopicRecommendations or NativeRouteDestinationId.Compare => "topics",
        NativeRouteDestinationId.ImportExport => ImportExportIntentId(match.Path),
        NativeRouteDestinationId.SourcesBrowse or NativeRouteDestinationId.SourceDetail or NativeRouteDestinationId.DomainsBrowse or
            NativeRouteDestinationId.DomainDetail or NativeRouteDestinationId.UrlsBrowse or NativeRouteDestinationId.UrlDetail => "web-search",
        NativeRouteDestinationId.FeedReferralLinks or NativeRouteDestinationId.Referrals => "referral-links",
        NativeRouteDestinationId.WebSearch => "web-search",
        NativeRouteDestinationId.UsersBrowse => "friends",
        NativeRouteDestinationId.UserProfile => "friends",
        NativeRouteDestinationId.UserAdmin => "friends",
        NativeRouteDestinationId.MembershipGrants => "administration",
        NativeRouteDestinationId.EngineeringQueues or NativeRouteDestinationId.EngineeringPostgresql or
        NativeRouteDestinationId.EngineeringValkey or NativeRouteDestinationId.EngineeringAiCosts or NativeRouteDestinationId.EngineeringDynamicConfig => "engineering",
        NativeRouteDestinationId.GrowthDashboard => "growth",
        NativeRouteDestinationId.ModerationReports or NativeRouteDestinationId.ModerationAppeals or NativeRouteDestinationId.ModerationDisputes or
            NativeRouteDestinationId.ModerationReviewQueue or NativeRouteDestinationId.ModerationAdmin or NativeRouteDestinationId.ModerationIntegrity or
            NativeRouteDestinationId.ModerationCases => "moderation",
        NativeRouteDestinationId.CommunitiesBrowse or NativeRouteDestinationId.CommunityDetail or NativeRouteDestinationId.CommunityAction => "communities",
        NativeRouteDestinationId.TagManagement => "topics",
        NativeRouteDestinationId.Messages or NativeRouteDestinationId.Notifications => NavigationCatalog.MessagesIntentId,
        NativeRouteDestinationId.Chat => "chat",
        NativeRouteDestinationId.LandingPages => "landing-pages",
        NativeRouteDestinationId.Lists or NativeRouteDestinationId.Bookmarks => NavigationCatalog.ListsIntentId,
        NativeRouteDestinationId.CopyrightNotices or NativeRouteDestinationId.AccountSettings or NativeRouteDestinationId.ProfileSettings or NativeRouteDestinationId.Household or NativeRouteDestinationId.PaymentCards or NativeRouteDestinationId.PointValuations or NativeRouteDestinationId.SpendingCategories or NativeRouteDestinationId.RewardsProgramStatuses or
            NativeRouteDestinationId.AdvancedSettings => NavigationCatalog.SettingsIntentId,
        _ => null,
      };

  private static string ImportExportIntentId(string path) => path switch
  {
    "/my/topics/import-export" => "topics",
    "/my/news-sources/import-export" => NavigationCatalog.NewsIntentId,
    "/my/podcasts/import-export" => "podcasts",
    "/my/channels/import-export" => "videos",
    _ => "web-search",
  };

  private static bool RequiresAuthentication(NativeRouteDestinationId destinationId, NativeRouteMatch match)
  {
    if (destinationId == NativeRouteDestinationId.UserProfile &&
        NativeUserProfileRoutePaths.IsSupportedMatch(match))
    {
      return false;
    }

    if (match.Path == "/feed" ||
        match.Path.StartsWith("/feed/", StringComparison.Ordinal) ||
        match.Path.StartsWith("/my/", StringComparison.Ordinal) ||
        match.Path.StartsWith("/topic-claims/", StringComparison.Ordinal))
    {
      return true;
    }

    if (match.Path.StartsWith("/communities/", StringComparison.Ordinal) &&
        (match.Path.EndsWith("/apply", StringComparison.Ordinal) ||
            match.Path.Contains("/invite/", StringComparison.Ordinal) ||
            match.Path.Contains("/applications", StringComparison.Ordinal) ||
            match.Path.Contains("/invites", StringComparison.Ordinal) ||
            match.Path.Contains("/bans", StringComparison.Ordinal) ||
            match.Path.Contains("/restrictions", StringComparison.Ordinal) ||
            match.Path.Contains("/modlog", StringComparison.Ordinal) ||
            match.Path.Contains("/modmail", StringComparison.Ordinal) ||
            match.Path.Contains("/moderator-stats", StringComparison.Ordinal) ||
            match.Path.Contains("/moderator-vacation", StringComparison.Ordinal) ||
            match.Path.Contains("/moderation-", StringComparison.Ordinal) ||
            match.Path.Contains("/ai-agents", StringComparison.Ordinal) ||
            match.Path.Contains("/agent-prompts", StringComparison.Ordinal) ||
            match.Path.Contains("/automod/", StringComparison.Ordinal)))
    {
      return true;
    }

    if (RequiredRoles(destinationId, match) is not null)
    {
      return true;
    }

    return destinationId is NativeRouteDestinationId.UrlsBrowse or NativeRouteDestinationId.UrlDetail or NativeRouteDestinationId.UsersBrowse or
        NativeRouteDestinationId.PostCompose or NativeRouteDestinationId.Messages or NativeRouteDestinationId.Chat or
        NativeRouteDestinationId.Notifications or NativeRouteDestinationId.AccountSettings or NativeRouteDestinationId.ProfileSettings or NativeRouteDestinationId.Household or NativeRouteDestinationId.PaymentCards or NativeRouteDestinationId.PointValuations or NativeRouteDestinationId.SpendingCategories or NativeRouteDestinationId.RewardsProgramStatuses or
        NativeRouteDestinationId.CopyrightNotices or NativeRouteDestinationId.AdvancedSettings or NativeRouteDestinationId.Referrals or
        NativeRouteDestinationId.Bookmarks or NativeRouteDestinationId.TopicRecommendations or NativeRouteDestinationId.TagManagement or
        NativeRouteDestinationId.ModerationCases or NativeRouteDestinationId.UserAdmin or NativeRouteDestinationId.MembershipGrants or
        NativeRouteDestinationId.EngineeringQueues or NativeRouteDestinationId.EngineeringPostgresql or NativeRouteDestinationId.EngineeringValkey or
        NativeRouteDestinationId.EngineeringAiCosts or NativeRouteDestinationId.EngineeringDynamicConfig or NativeRouteDestinationId.GrowthDashboard or
        NativeRouteDestinationId.ModerationReports or NativeRouteDestinationId.ModerationAppeals or NativeRouteDestinationId.ModerationDisputes or
        NativeRouteDestinationId.ModerationReviewQueue or NativeRouteDestinationId.ModerationAdmin or NativeRouteDestinationId.ModerationIntegrity or
        NativeRouteDestinationId.CommunityAction;
  }

  private static bool IsRouteVisible(NativeRouteDestinationId destinationId, NativeRouteMatch match, NavigationViewer viewer)
  {
    var roles = RequiredRoles(destinationId, match);
    return roles is null || roles.Any(role => viewer.Roles.Contains(role, StringComparer.Ordinal));
  }

  private static IReadOnlyList<string>? RequiredRoles(NativeRouteDestinationId destinationId, NativeRouteMatch match)
  {
    if (destinationId is NativeRouteDestinationId.UserAdmin)
    {
      return ["administrator"];
    }

    if (destinationId is NativeRouteDestinationId.MembershipGrants or
        NativeRouteDestinationId.EngineeringQueues or NativeRouteDestinationId.EngineeringPostgresql or NativeRouteDestinationId.EngineeringValkey or
        NativeRouteDestinationId.EngineeringAiCosts or NativeRouteDestinationId.ModerationReviewQueue or
        NativeRouteDestinationId.ModerationAdmin or NativeRouteDestinationId.ModerationIntegrity)
    {
      return ["administrator"];
    }

    if (destinationId == NativeRouteDestinationId.EngineeringDynamicConfig)
    {
      return ["administrator", "moderator", "developer", "investor"];
    }

    if (destinationId is NativeRouteDestinationId.ModerationAppeals or NativeRouteDestinationId.ModerationDisputes)
    {
      return ["administrator", "moderator"];
    }

    if (destinationId == NativeRouteDestinationId.UrlDetail && match.Path.StartsWith("/crawler/", StringComparison.Ordinal))
    {
      return ["administrator"];
    }

    if (destinationId == NativeRouteDestinationId.GrowthDashboard)
    {
      return ["administrator", "investor"];
    }

    if (destinationId == NativeRouteDestinationId.TopicManagement)
    {
      return ["administrator"];
    }

    if (destinationId == NativeRouteDestinationId.PostCompose &&
        match.Path is "/articles/create" or "/blog/create")
    {
      return ["administrator"];
    }

    if (destinationId == NativeRouteDestinationId.TagManagement)
    {
      return null;
    }

    if ((destinationId is NativeRouteDestinationId.PostsBrowse or NativeRouteDestinationId.TopicsBrowse) &&
        (match.Path.StartsWith("/admin/", StringComparison.Ordinal) ||
            match.Path.StartsWith("/curated-asides/", StringComparison.Ordinal) ||
            match.Path is "/rss-feed-categories"))
    {
      return ["administrator"];
    }

    return null;
  }
}
