namespace Voucha.Client.Core.Navigation;

public static partial class NativeRouteCatalog
{
  private static NativeRouteCatalogEntry[] AccountEntries() =>
  [
    Included("Copyright notices", NativeRouteDestinationId.CopyrightNotices,
        "/copyright/notices", ["/copyright/notices", "/copyright/notices/:id"]),
    Included("Sign in", NativeRouteDestinationId.SignIn, "/login", ["/login"]),
    Included(
        "Account settings",
        NativeRouteDestinationId.AccountSettings,
        "/my/identity",
        ["/my/identity", "/my/privacy", "/my/membership", "/my/identity-verification"]),
    Included("Payment cards", NativeRouteDestinationId.PaymentCards, "/my/cards", ["/my/cards"]),
    Included("Point valuations", NativeRouteDestinationId.PointValuations,
        "/my/rewards-program-point-valuations", ["/my/rewards-program-point-valuations"]),
    Included("Spending categories", NativeRouteDestinationId.SpendingCategories,
        "/my/spending-categories", ["/my/spending-categories"]),
    Included("Rewards program statuses", NativeRouteDestinationId.RewardsProgramStatuses,
        "/my/rewards-program-statuses", ["/my/rewards-program-statuses"]),
    Included("Profile settings", NativeRouteDestinationId.ProfileSettings,
        "/my/profile", ["/my/profile"]),
    Included("Household", NativeRouteDestinationId.Household, "/my/household", ["/my/household"]),
    Included(
        "Advanced settings",
        NativeRouteDestinationId.AdvancedSettings,
        "/my/preferences",
        [
          "/my/preferences", "/my/api-keys", "/my/friend-recommendations", "/my/data",
          "/my/language", "/my/news-preferences", "/my/notification-settings",
        ]),
    Included("Referrals", NativeRouteDestinationId.Referrals, "/my/referrals", ["/my/referrals", "/my/referral-links"]),
    Included(
        "Landing pages",
        NativeRouteDestinationId.LandingPages,
        "/my/landing-pages",
        [
          "/my/landing-pages", "/my/landing-page/:slug", "/my/landing-page/:slug/analytics",
          "/landing/:idOrUsername", "/landing/:idOrUsername/:slug", "/user/:idOrUsername/landing",
          "/@:username", "/@:username/:slug",
        ]),
    Included("Lists", NativeRouteDestinationId.Lists, "/my/lists", ["/my/lists", "/list/:id"]),
    Included("Post bookmarks", NativeRouteDestinationId.Bookmarks, "/my/posts/saved",
        ["/my/posts/saved", "/my/posts/hidden", "/my/posts/following", "/my/posts/subscribed"]),
    Included("Topic bookmarks", NativeRouteDestinationId.Bookmarks, "/my/topics/following",
        ["/my/topics/following", "/my/topics/muted", "/my/topics/blocked", "/my/topics/dismissed-recommendations", "/my/topics/viewed"]),
    Included("User bookmarks", NativeRouteDestinationId.Bookmarks, "/my/users/following",
        [
          "/my/users/following", "/my/users/followers", "/my/users/subscribed-posts", "/my/users/muted", "/my/users/blocked",
          "/my/friend-recommendations/dismissed",
        ]),
    Included("News bookmarks", NativeRouteDestinationId.Bookmarks, "/my/news-items/saved",
        [
          "/my/news-items/saved", "/my/news-items/hidden", "/my/news-items/viewed", "/my/news-sources/muted",
          "/my/news-sources/viewed", "/my/rss-feed-items/saved", "/my/rss-feed-items/viewed", "/my/rss-feed-items/hidden",
        ]),
    Included("Podcast bookmarks", NativeRouteDestinationId.Bookmarks, "/my/podcast-episodes/saved",
        ["/my/podcast-episodes/saved", "/my/podcast-episodes/hidden", "/my/podcast-episodes/viewed", "/my/podcasts/muted", "/my/podcasts/viewed"]),
    Included("Video bookmarks", NativeRouteDestinationId.Bookmarks, "/my/videos/saved",
        ["/my/videos/saved", "/my/videos/hidden", "/my/videos/viewed", "/my/channels/muted", "/my/channels/viewed"]),
    Included("Web bookmarks", NativeRouteDestinationId.Bookmarks, "/my/urls/saved",
        ["/my/urls/saved", "/my/domains/muted", "/my/domains/blocked"]),
    Included("Community bookmarks", NativeRouteDestinationId.Bookmarks, "/my/communities/saved",
        ["/my/communities/saved", "/my/communities/proxy-following", "/my/communities/proxy-muted"]),
    Included("Plans", NativeRouteDestinationId.Plans, "/plans", ["/plans"]),
    Included("Topic recommendations", NativeRouteDestinationId.TopicRecommendations, "/topic-recommendations",
        ["/topic-recommendations", "/topic-recommendations/create", "/topic-recommendations/:id", "/topic-recommendations/:id/edit"]),
    Included("Moderation cases", NativeRouteDestinationId.ModerationCases, "/my/appeals",
        ["/my/appeals", "/my/disputes", "/my/warnings", "/my/bans", "/my/removed-posts", "/my/account-status"]),
    Included("Moderation transparency", NativeRouteDestinationId.ModerationCases,
        "/moderation-transparency", ["/moderation-transparency"]),
    Included("Compare", NativeRouteDestinationId.Compare, "/compare", ["/compare", "/compare/:slugA-vs-:slugB"]),
  ];
}
