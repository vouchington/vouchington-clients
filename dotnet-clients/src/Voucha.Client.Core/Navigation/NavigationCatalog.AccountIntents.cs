using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Navigation;

public static partial class NavigationCatalog
{
  private static NavIntent ChatIntent { get; } = new(
      "chat",
      UiMessageKey.ExtractedIntentsProductCommunicationChat460b3a7d,
      "message-square",
      [
        new NavGroup(
            UiMessageKey.ExtractedIntentsProductCommunicationChat460b3a7d,
            "sidebar-group-chat",
            [
              new NavItem(UiMessageKey.ExtractedIntentsProductCommunicationNewChat0d332351, "/chat", "sidebar-nav-new-chat", RequiresAuth: true, Exact: true),
              new NavItem(UiMessageKey.ExtractedIntentsProductCommunicationSupportBe91940b, "/chat/support", "sidebar-nav-chat-support", RequiresAuth: true),
            ],
            RequiresAuth: true),
      ],
      RequiresAuth: true);

  private static NavIntent MessagesIntent { get; } = new(
      "messages",
      UiMessageKey.ExtractedIntentsProductCommunicationMessagesNotifications6dfecd8b,
      "bell",
      [
        new NavGroup(
            UiMessageKey.ExtractedIntentsProductCommunicationNotifications78801183,
            "sidebar-group-notifications",
            [
              new NavItem(UiMessageKey.ExtractedIntentsProductCommunicationNotifications78801183, "/my/notifications", "sidebar-nav-notifications", RequiresAuth: true),
            ],
            RequiresAuth: true),
        new NavGroup(
            UiMessageKey.ExtractedIntentsProductCommunicationMessages04d7b483,
            "sidebar-group-messages",
            [
              new NavItem(UiMessageKey.ExtractedIntentsProductCommunicationAllMessages020dc04d, "/messages", "sidebar-nav-messages", RequiresAuth: true, Exact: true),
            ],
            RequiresAuth: true),
      ],
      RequiresAuth: true);

  private static NavIntent LandingPagesIntent { get; } = new(
      "landing-pages",
      UiMessageKey.ExtractedIntentsProductCommunicationLandingPages6e8d0e5d,
      "layout-template",
      [
        new NavGroup(
            UiMessageKey.ExtractedIntentsProductCommunicationBrowse3227aa96,
            "sidebar-group-browse",
            [
              new NavItem(UiMessageKey.ExtractedIntentsProductCommunicationMyLandingPages68f82dad, "/my/landing-pages", "sidebar-nav-my-landing-pages", RequiresAuth: true),
            ]),
      ],
      RequiresAuth: true);

  private static NavIntent CommunitiesIntent { get; } = new(
      "communities",
      UiMessageKey.ExtractedIntentsProductSocialCommunitiesC864f329,
      "users",
      [
        new NavGroup(
            UiMessageKey.ExtractedIntentsProductSocialBrowse3227aa96,
            "sidebar-group-browse",
            [
              new NavItem(UiMessageKey.ExtractedIntentsProductSocialExplore3b73900b, "/communities", "sidebar-nav-explore"),
            ]),
        new NavGroup(
            UiMessageKey.ExtractedIntentsProductSocialBookmarks96316f0f,
            "sidebar-group-bookmarks",
            [
              new NavItem(UiMessageKey.ExtractedIntentsProductSocialSavedCommunities0e48d499, "/my/communities/saved", "sidebar-nav-my-communities-saved", RequiresAuth: true),
              new NavItem(UiMessageKey.ExtractedIntentsProductSocialProxyFollowedCommunities5d8b8c00, "/my/communities/proxy-following", "sidebar-nav-my-communities-proxy-following", RequiresAuth: true),
              new NavItem(UiMessageKey.ExtractedIntentsProductSocialProxyMutedCommunities241f8699, "/my/communities/proxy-muted", "sidebar-nav-my-communities-proxy-muted", RequiresAuth: true),
            ],
            RequiresAuth: true),
      ]);

  private static NavIntent FriendsIntent { get; } = new(
      "friends",
      UiMessageKey.ExtractedIntentsProductSocialUsersFriends5291e860,
      "user-plus",
      [
        new NavGroup(
            UiMessageKey.ExtractedIntentsProductSocialBrowse3227aa96,
            "sidebar-group-browse",
            [
              new NavItem(UiMessageKey.ExtractedIntentsProductSocialUsers6b0cc904, "/users", "sidebar-nav-users"),
              new NavItem(UiMessageKey.ExtractedIntentsProductSocialFindFriendsD4864039, "/my/friend-recommendations", "sidebar-nav-find-friends"),
            ]),
        new NavGroup(
            UiMessageKey.ExtractedIntentsProductSocialBookmarks96316f0f,
            "sidebar-group-bookmarks",
            [
              new NavItem(UiMessageKey.ExtractedIntentsProductSocialFollowing344b4271, "/my/users/following", "sidebar-nav-my-users-following", RequiresAuth: true),
              new NavItem(UiMessageKey.ExtractedIntentsProductSocialFollowersA145ab34, "/my/users/followers", "sidebar-nav-my-users-followers", RequiresAuth: true),
              new NavItem(UiMessageKey.ExtractedIntentsProductSocialSubscribedToPostsD21eed77, "/my/users/subscribed-posts", "sidebar-nav-my-users-subscribed-posts", RequiresAuth: true),
              new NavItem(UiMessageKey.ExtractedIntentsProductSocialMuted2346f214, "/my/users/muted", "sidebar-nav-my-users-muted", RequiresAuth: true),
              new NavItem(UiMessageKey.ExtractedIntentsProductSocialBlocked18f2a094, "/my/users/blocked", "sidebar-nav-my-users-blocked", RequiresAuth: true),
            ],
            RequiresAuth: true),
      ],
      RequiresAuth: true);

  private static NavIntent ListsIntent { get; } = new(
      "lists",
      UiMessageKey.ExtractedIntentsProductListsLists308d5a09,
      "book-marked",
      [
        new NavGroup(
            UiMessageKey.ExtractedIntentsProductListsMyLists94de1d96,
            "sidebar-group-my-lists",
            [
              new NavItem(UiMessageKey.ExtractedIntentsProductListsMyLists94de1d96, "/my/lists", "sidebar-nav-my-lists", RequiresAuth: true),
            ],
            RequiresAuth: true),
      ],
      RequiresAuth: true);
}
