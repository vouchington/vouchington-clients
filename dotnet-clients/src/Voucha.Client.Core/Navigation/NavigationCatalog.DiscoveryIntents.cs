using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Navigation;

public static partial class NavigationCatalog
{
  private static NavIntent PostsIntent { get; } = new(
      "posts",
      UiMessageKey.ExtractedIntentsProductPostsPostsA80811cf,
      "file-text",
      [
        new NavGroup(
            UiMessageKey.ExtractedIntentsProductPostsBrowse3227aa96,
            "sidebar-group-browse",
            [
              new NavItem(UiMessageKey.ExtractedIntentsProductPostsMyPostsFeedA1ed6e47, "/feed/posts", "sidebar-nav-your-posts", RequiresAuth: true),
              new NavItem(UiMessageKey.ExtractedIntentsProductPostsStories6d09cf57, "/stories", "sidebar-nav-stories"),
              new NavItem(UiMessageKey.ExtractedIntentsProductPostsAllPosts0fae3f21, "/posts", "sidebar-nav-all-posts"),
              new NavItem(UiMessageKey.ExtractedIntentsProductPostsDiscussions60157cfc, "/discussions", "sidebar-nav-discussions"),
              new NavItem(UiMessageKey.ExtractedIntentsProductPostsReviews84cb7871, "/reviews", "sidebar-nav-reviews"),
              new NavItem(UiMessageKey.ExtractedIntentsProductPostsDataPoints1da65e3a, "/data-points", "sidebar-nav-data-points"),
              new NavItem(UiMessageKey.ExtractedIntentsProductPostsLinks9024c197, "/links", "sidebar-nav-links"),
              new NavItem(UiMessageKey.ExtractedIntentsProductPostsArticlesB14ac78a, "/articles", "sidebar-nav-articles"),
              new NavItem(UiMessageKey.ExtractedIntentsProductPostsBlog8c6bc099, "/blog", "sidebar-nav-blog"),
            ]),
        new NavGroup(
            UiMessageKey.ExtractedIntentsProductPostsBookmarks96316f0f,
            "sidebar-group-bookmarks",
            [
              new NavItem(UiMessageKey.ExtractedIntentsProductPostsSavedPosts2e550416, "/my/posts/saved", "sidebar-nav-my-posts-saved", RequiresAuth: true),
              new NavItem(UiMessageKey.ExtractedIntentsProductPostsHiddenPostsB4330740, "/my/posts/hidden", "sidebar-nav-my-posts-hidden", RequiresAuth: true),
              new NavItem(UiMessageKey.ExtractedIntentsProductPostsFollowedPostsF39d9e4e, "/my/posts/following", "sidebar-nav-my-posts-following", RequiresAuth: true),
              new NavItem(UiMessageKey.ExtractedIntentsProductPostsSubscribedToPostsD21eed77, "/my/posts/subscribed", "sidebar-nav-my-posts-subscribed", RequiresAuth: true),
            ],
            RequiresAuth: true),
        new NavGroup(
            UiMessageKey.ExtractedIntentsProductPostsAdminC1c224b0,
            "sidebar-group-admin",
            [
              new NavItem(UiMessageKey.ExtractedIntentsProductPostsCuratedAsides8798b0ff, "/curated-asides/topics", "sidebar-nav-curated-asides"),
            ],
            Roles: ["administrator"]),
      ]);

  private static NavIntent TopicsIntent { get; } = new(
      "topics",
      UiMessageKey.ExtractedIntentsProductOtherTopicsE22820fc,
      "layers",
      [
        new NavGroup(
            UiMessageKey.ExtractedIntentsProductOtherBrowse3227aa96,
            "sidebar-group-browse",
            [
              new NavItem(UiMessageKey.ExtractedIntentsProductOtherAllTopics0f02bd30, "/topics", "sidebar-nav-all-topics", Exact: true),
              new NavItem(UiMessageKey.ExtractedIntentsProductOtherCardsA52fcbbc, "/cards", "sidebar-nav-cards"),
              new NavItem(UiMessageKey.ExtractedIntentsProductOtherRewardsProgramsCfc1c858, "/rewards-programs", "sidebar-nav-rewards-programs"),
              new NavItem(UiMessageKey.ExtractedIntentsProductOtherRewardsProgramStatusesB2a04de2, "/rewards-program-statuses", "sidebar-nav-rewards-program-statuses"),
              new NavItem(UiMessageKey.ExtractedIntentsProductOtherSpendingCategories3ed30dfb, "/spending-categories", "sidebar-nav-spending-categories"),
              new NavItem(UiMessageKey.ExtractedIntentsProductOtherRecommendNewTopics1f5064da, "/topic-recommendations", "sidebar-nav-recommendations", RequiresAuth: true),
            ]),
        new NavGroup(
            UiMessageKey.ExtractedIntentsProductOtherBookmarks96316f0f,
            "sidebar-group-bookmarks",
            [
              new NavItem(UiMessageKey.ExtractedIntentsProductOtherFollowedTopicsE60a6db0, "/my/topics/following", "sidebar-nav-my-topics-following", RequiresAuth: true),
              new NavItem(UiMessageKey.ExtractedIntentsProductOtherMutedTopicsB2caf51a, "/my/topics/muted", "sidebar-nav-my-topics-muted", RequiresAuth: true),
              new NavItem(UiMessageKey.ExtractedIntentsProductOtherBlockedTopicsE4e7ae0d, "/my/topics/blocked", "sidebar-nav-my-topics-blocked", RequiresAuth: true),
              new NavItem(UiMessageKey.ExtractedIntentsProductOtherRecentlyViewedTopicsF93ba041, "/my/topics/viewed", "sidebar-nav-my-topics-viewed", RequiresAuth: true),
              new NavItem(UiMessageKey.ExtractedIntentsProductOtherImportExport42191ef4, "/my/topics/import-export", "sidebar-nav-my-topics-import-export", RequiresAuth: true),
            ],
            RequiresAuth: true),
        new NavGroup(
            UiMessageKey.ExtractedIntentsProductOtherAdminC1c224b0,
            "sidebar-group-admin",
            [
              new NavItem(UiMessageKey.ExtractedIntentsProductOtherCreateTopic15c75a49, "/topics/create", "sidebar-nav-create-topic"),
              new NavItem(UiMessageKey.ExtractedIntentsProductOtherTopicAliases79589f83, "/topics/aliases", "sidebar-nav-topic-aliases"),
              new NavItem(UiMessageKey.ExtractedIntentsProductOtherTopicClaims105cfd52, "/admin/topic-claims", "sidebar-nav-topic-claims"),
              new NavItem(UiMessageKey.ExtractedIntentsProductOtherRssFeedCategories1c481b92, "/rss-feed-categories", "sidebar-nav-rss-feed-categories"),
            ],
            Roles: ["administrator"]),
      ]);

  private static NavIntent ReferralLinksIntent { get; } = new(
      "referral-links",
      UiMessageKey.ExtractedIntentsProductOtherReferralLinks4348d2ad,
      "gift",
      [
        new NavGroup(
            UiMessageKey.ExtractedIntentsProductOtherBrowse3227aa96,
            "sidebar-group-browse",
            [
              new NavItem(UiMessageKey.ExtractedIntentsProductOtherMyReferralLinkFeedD8f7c5a4, "/feed/referral-links", "sidebar-nav-my-referral-link-feed", RequiresAuth: true),
              new NavItem(UiMessageKey.ExtractedIntentsProductOtherMyReferralLinksCe5b4e52, "/my/referral-links", "sidebar-nav-my-referral-links", RequiresAuth: true),
              new NavItem(UiMessageKey.ExtractedIntentsProductOtherReferralProgramsCeb8b9ad, "/referral-programs", "sidebar-nav-referral-programs"),
            ]),
        new NavGroup(
            UiMessageKey.ExtractedIntentsProductOtherVouchaReferralProgram1f532bac,
            "sidebar-group-voucha-referral-program",
            [
              new NavItem(UiMessageKey.ExtractedIntentsProductOtherMyReferrals679e6b61, "/my/referrals", "sidebar-nav-my-referrals", RequiresAuth: true),
            ],
            RequiresAuth: true),
      ]);

  private static NavIntent WebSearchIntent { get; } = new(
      "web-search",
      UiMessageKey.ExtractedIntentsProductSearchWebSearchD04fc7d7,
      "globe",
      [
        new NavGroup(
            UiMessageKey.ExtractedIntentsProductSearchBrowse3227aa96,
            "sidebar-group-browse",
            [
              new NavItem(UiMessageKey.ExtractedIntentsProductSearchSearch49c266ba, "/web-search", "sidebar-nav-search"),
              new NavItem(UiMessageKey.ExtractedIntentsProductSearchDomainsCed67718, "/domains", "sidebar-nav-domains"),
              new NavItem(UiMessageKey.ExtractedIntentsProductSearchSourcesCaf85b08, "/sources", "sidebar-nav-sources"),
              new NavItem(UiMessageKey.ExtractedIntentsProductSearchUrls1240054e, "/urls", "sidebar-nav-urls", RequiresAuth: true),
              new NavItem(UiMessageKey.ExtractedIntentsProductSearchImportExportSources7b7ffe10, "/my/sources/import-export", "sidebar-nav-sources-import-export", RequiresAuth: true),
            ]),
        new NavGroup(
            UiMessageKey.ExtractedIntentsProductSearchBookmarks96316f0f,
            "sidebar-group-bookmarks",
            [
              new NavItem(UiMessageKey.ExtractedIntentsProductSearchSavedLinks527bc63c, "/my/urls/saved", "sidebar-nav-my-urls-saved", RequiresAuth: true),
              new NavItem(UiMessageKey.ExtractedIntentsProductSearchMutedDomains94510a32, "/my/domains/muted", "sidebar-nav-my-domains-muted", RequiresAuth: true),
              new NavItem(UiMessageKey.ExtractedIntentsProductSearchBlockedDomains01999296, "/my/domains/blocked", "sidebar-nav-my-domains-blocked", RequiresAuth: true),
            ],
            RequiresAuth: true),
      ]);

  private static NavIntent FediverseIntent { get; } = new(
      "fediverse",
      UiMessageKey.ExtractedIntentsProductFediverseFediverse5b02ab9d,
      "network",
      [
        new NavGroup(
            UiMessageKey.ExtractedIntentsProductFediverseBrowse3227aa96,
            "sidebar-group-browse",
            [
              new NavItem(UiMessageKey.ExtractedIntentsProductFediverseFediverseSearchA4896269, "/fediverse", "sidebar-nav-fediverse-search", Exact: true),
              new NavItem(UiMessageKey.ExtractedIntentsProductFediverseInstancesF6e1a7e2, "/instances", "sidebar-nav-fediverse-instances"),
            ]),
      ],
      FeatureFlag: "fediverse");
}
