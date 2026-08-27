using Voucha.Client.Core.Localization;

namespace Voucha.Client.Core.Navigation;

public static partial class NavigationCatalog
{
  private static NavIntent NewsIntent { get; } = new(
      "news",
      UiMessageKey.ExtractedIntentsProductMediaNewsNews69752f23,
      "newspaper",
      [
        new NavGroup(
            UiMessageKey.ExtractedIntentsProductMediaNewsBrowse3227aa96,
            "sidebar-group-browse",
            [
              new NavItem(UiMessageKey.ExtractedIntentsProductMediaNewsMyNewsFeed45cd71ae, "/feed/news", "sidebar-nav-your-news", RequiresAuth: true),
              new NavItem(UiMessageKey.ExtractedIntentsProductMediaNewsAllNews3651c3d0, "/news", "sidebar-nav-all-news"),
              new NavItem(UiMessageKey.ExtractedIntentsProductMediaNewsAllNewsSources31ea4dc6, "/news-sources", "sidebar-nav-all-news-sources"),
            ]),
        new NavGroup(
            UiMessageKey.ExtractedIntentsProductMediaNewsNewsBookmarks911304a2,
            "sidebar-group-news-bookmarks",
            [
              new NavItem(UiMessageKey.ExtractedIntentsProductMediaNewsSavedNews5b954d80, "/my/news-items/saved", "sidebar-nav-my-news-items-saved", RequiresAuth: true),
              new NavItem(UiMessageKey.ExtractedIntentsProductMediaNewsHiddenNewsCfa2e0e3, "/my/news-items/hidden", "sidebar-nav-my-news-items-hidden", RequiresAuth: true),
              new NavItem(UiMessageKey.ExtractedIntentsProductMediaNewsRecentlyViewedNews809bc5a3, "/my/news-items/viewed", "sidebar-nav-my-news-items-viewed", RequiresAuth: true),
            ],
            RequiresAuth: true),
        new NavGroup(
            UiMessageKey.ExtractedIntentsProductMediaNewsSourceBookmarks47e1b795,
            "sidebar-group-source-bookmarks",
            [
              new NavItem(UiMessageKey.ExtractedIntentsProductMediaNewsFollowedNewsSourcesDd33239b, "/my/news-sources", "sidebar-nav-my-news-sources", RequiresAuth: true, Exact: true),
              new NavItem(UiMessageKey.ExtractedIntentsProductMediaNewsMutedNewsSourcesCa1e1481, "/my/news-sources/muted", "sidebar-nav-my-news-sources-muted", RequiresAuth: true),
              new NavItem(UiMessageKey.ExtractedIntentsProductMediaNewsRecentlyViewedNewsSourcesD6a7ee60, "/my/news-sources/viewed", "sidebar-nav-my-news-sources-viewed", RequiresAuth: true),
              new NavItem(UiMessageKey.ExtractedIntentsProductMediaNewsImportExportNewsSourcesBf1ec4ca, "/my/news-sources/import-export", "sidebar-nav-news-sources-import-export", RequiresAuth: true),
            ],
            RequiresAuth: true),
      ]);

  private static NavIntent PodcastsIntent { get; } = new(
      "podcasts",
      UiMessageKey.ExtractedIntentsProductMediaPodcastsPodcasts6ac749b3,
      "headphones",
      [
        new NavGroup(
            UiMessageKey.ExtractedIntentsProductMediaPodcastsBrowse3227aa96,
            "sidebar-group-browse",
            [
              new NavItem(UiMessageKey.ExtractedIntentsProductMediaPodcastsMyPodcastEpisodesFeedA0c523e2, "/feed/podcasts", "sidebar-nav-your-podcasts-feed", RequiresAuth: true),
              new NavItem(UiMessageKey.ExtractedIntentsProductMediaPodcastsAllPodcastEpisodes3dcc70fc, "/podcast-episodes", "sidebar-nav-podcast-episodes"),
              new NavItem(UiMessageKey.ExtractedIntentsProductMediaPodcastsAllPodcasts54007cce, "/podcasts", "sidebar-nav-all-podcasts"),
            ]),
        new NavGroup(
            UiMessageKey.ExtractedIntentsProductMediaPodcastsEpisodeBookmarksFb7f7f9d,
            "sidebar-group-episode-bookmarks",
            [
              new NavItem(UiMessageKey.ExtractedIntentsProductMediaPodcastsSavedEpisodesA8d3fb46, "/my/podcast-episodes/saved", "sidebar-nav-my-podcast-episodes-saved", RequiresAuth: true),
              new NavItem(UiMessageKey.ExtractedIntentsProductMediaPodcastsHiddenEpisodesBe7de6e2, "/my/podcast-episodes/hidden", "sidebar-nav-my-podcast-episodes-hidden", RequiresAuth: true),
              new NavItem(UiMessageKey.ExtractedIntentsProductMediaPodcastsRecentlyViewedEpisodes7bdbff30, "/my/podcast-episodes/viewed", "sidebar-nav-my-podcast-episodes-viewed", RequiresAuth: true),
            ],
            RequiresAuth: true),
        new NavGroup(
            UiMessageKey.ExtractedIntentsProductMediaPodcastsSourceBookmarks47e1b795,
            "sidebar-group-source-bookmarks",
            [
              new NavItem(UiMessageKey.ExtractedIntentsProductMediaPodcastsFollowedPodcasts7ff90cde, "/my/podcasts", "sidebar-nav-my-podcasts", RequiresAuth: true, Exact: true),
              new NavItem(UiMessageKey.ExtractedIntentsProductMediaPodcastsMutedPodcasts131446ce, "/my/podcasts/muted", "sidebar-nav-my-podcasts-muted", RequiresAuth: true),
              new NavItem(UiMessageKey.ExtractedIntentsProductMediaPodcastsRecentlyViewedPodcastsBec02586, "/my/podcasts/viewed", "sidebar-nav-my-podcasts-viewed", RequiresAuth: true),
              new NavItem(UiMessageKey.ExtractedIntentsProductMediaPodcastsImportExportPodcasts6aff6305, "/my/podcasts/import-export", "sidebar-nav-podcasts-import-export", RequiresAuth: true),
            ],
            RequiresAuth: true),
      ]);

  private static NavIntent VideosIntent { get; } = new(
      "videos",
      UiMessageKey.ExtractedIntentsProductMediaVideosVideosC9a96394,
      "play",
      [
        new NavGroup(
            UiMessageKey.ExtractedIntentsProductMediaVideosBrowse3227aa96,
            "sidebar-group-browse",
            [
              new NavItem(UiMessageKey.ExtractedIntentsProductMediaVideosMyVideoFeedD4cade0f, "/feed/videos", "sidebar-nav-your-videos-feed", RequiresAuth: true),
              new NavItem(UiMessageKey.ExtractedIntentsProductMediaVideosAllVideos1c86e601, "/videos", "sidebar-nav-all-videos"),
              new NavItem(UiMessageKey.ExtractedIntentsProductMediaVideosAllChannels8a422ea5, "/channels", "sidebar-nav-all-channels"),
            ]),
        new NavGroup(
            UiMessageKey.ExtractedIntentsProductMediaVideosVideoBookmarks2fe25b69,
            "sidebar-group-video-bookmarks",
            [
              new NavItem(UiMessageKey.ExtractedIntentsProductMediaVideosSavedVideosDf6630ae, "/my/videos/saved", "sidebar-nav-my-videos-saved", RequiresAuth: true),
              new NavItem(UiMessageKey.ExtractedIntentsProductMediaVideosHiddenVideosC4f904fc, "/my/videos/hidden", "sidebar-nav-my-videos-hidden", RequiresAuth: true),
              new NavItem(UiMessageKey.ExtractedIntentsProductMediaVideosRecentlyViewedVideos22155e8d, "/my/videos/viewed", "sidebar-nav-my-videos-viewed", RequiresAuth: true),
            ],
            RequiresAuth: true),
        new NavGroup(
            UiMessageKey.ExtractedIntentsProductMediaVideosSourceBookmarks47e1b795,
            "sidebar-group-source-bookmarks",
            [
              new NavItem(UiMessageKey.ExtractedIntentsProductMediaVideosFollowedChannels65e5681c, "/my/channels", "sidebar-nav-my-channels", RequiresAuth: true, Exact: true),
              new NavItem(UiMessageKey.ExtractedIntentsProductMediaVideosMutedChannels7a774310, "/my/channels/muted", "sidebar-nav-my-channels-muted", RequiresAuth: true),
              new NavItem(UiMessageKey.ExtractedIntentsProductMediaVideosRecentlyViewedChannels8b96ec27, "/my/channels/viewed", "sidebar-nav-my-channels-viewed", RequiresAuth: true),
              new NavItem(UiMessageKey.ExtractedIntentsProductMediaVideosImportExportChannels8ab0e50a, "/my/channels/import-export", "sidebar-nav-channels-import-export", RequiresAuth: true),
            ],
            RequiresAuth: true),
      ]);
}
