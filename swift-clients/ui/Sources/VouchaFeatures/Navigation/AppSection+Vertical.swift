import VouchaLocalization

public extension AppSection {
    /// True for sections that have per-subsection feed and source pages.
    var isVertical: Bool {
        switch self {
        case .news, .videos, .podcasts, .posts: true
        default: false
        }
    }

    /// Ordered sub-options for this section. Empty for non-vertical sections.
    var subsections: [VerticalSubsection] {
        switch self {
        case .news, .videos, .podcasts:
            [.feed(.your), .feed(.all), .sources(.your), .sources(.all)]
        case .posts:
            [.feed(.your), .feed(.all)]
        default:
            []
        }
    }

    /// Section-specific display label for a sub-option.
    func subsectionTitle(_ subsection: VerticalSubsection) -> UiVerbatimText {
        switch self {
        case .news: newsTitle(subsection)
        case .podcasts: podcastsTitle(subsection)
        case .videos: videosTitle(subsection)
        case .posts: postsTitle(subsection)
        default: .verbatim(subsection.id)
        }
    }

    private func newsTitle(_ subsection: VerticalSubsection) -> UiVerbatimText {
        switch subsection {
        case .feed(.your): .message(.nativeSwiftVerticalNavigationYourNewsFeed)
        case .feed(.all): .message(.nativeSwiftVerticalNavigationAllNews)
        case .sources(.your): .message(.nativeSwiftVerticalNavigationYourSources)
        case .sources(.all): .message(.nativeSwiftVerticalNavigationAllSources)
        }
    }

    private func podcastsTitle(_ subsection: VerticalSubsection) -> UiVerbatimText {
        switch subsection {
        case .feed(.your): .message(.nativeSwiftVerticalNavigationYourEpisodes)
        case .feed(.all): .message(.nativeSwiftVerticalNavigationAllEpisodes)
        case .sources(.your): .message(.nativeSwiftVerticalNavigationYourPodcasts)
        case .sources(.all): .message(.nativeSwiftVerticalNavigationAllPodcasts)
        }
    }

    private func videosTitle(_ subsection: VerticalSubsection) -> UiVerbatimText {
        switch subsection {
        case .feed(.your): .message(.nativeSwiftVerticalNavigationYourVideoFeed)
        case .feed(.all): .message(.nativeSwiftVerticalNavigationAllVideos)
        case .sources(.your): .message(.nativeSwiftVerticalNavigationYourChannels)
        case .sources(.all): .message(.nativeSwiftVerticalNavigationAllChannels)
        }
    }

    private func postsTitle(_ subsection: VerticalSubsection) -> UiVerbatimText {
        switch subsection {
        case .feed(.your): .message(.nativeSwiftVerticalNavigationYourPosts)
        case .feed(.all): .message(.nativeSwiftVerticalNavigationAllPosts)
        default: .verbatim(subsection.id)
        }
    }

    /// The `media_type` query param for feed-item requests in this vertical.
    ///
    /// **Asymmetry**: podcast sources use `feed_type = "podcast"` but podcast items
    /// use `media_type = "audio"`. This is the single source of truth for that mapping.
    var mediaType: String? {
        switch self {
        case .news: "article"
        case .videos: "video"
        case .podcasts: "audio" // podcast source feed_type='podcast' but item media_type='audio'
        default: nil
        }
    }

    /// The `ContentType` for feed-item view models in this vertical.
    /// Nil for non-vertical sections (Notifications, Friends, Profile) and Posts
    /// (which uses PostsListViewModel, not RSSFeedListViewModel).
    var contentType: ContentType? {
        switch self {
        case .news: .news
        case .videos: .video
        case .podcasts: .podcast
        default: nil
        }
    }

    /// The `feed_type` filter for source listing endpoints (e.g. `/api/v1/rss-feeds?feed_type=`).
    ///
    /// **Asymmetry**: podcast items have `media_type = "audio"` but podcast sources have
    /// `feed_type = "podcast"`. Never use this value as a `media_type` param.
    var sourceFeedType: String? {
        switch self {
        case .news: "article"
        case .videos: "video"
        case .podcasts: "podcast" // NOT "audio" — feed_type uses "podcast", not "audio"
        default: nil
        }
    }
}
