import VouchaLocalization

public enum ContentType: Sendable {
    case news
    case video
    case podcast

    var feedType: String {
        "any"
    }

    var mediaType: String? {
        switch self {
        case .news: "article"
        case .video: "video"
        case .podcast: "audio"
        }
    }

    var emptyTitle: UiVerbatimText {
        switch self {
        case .news: .message(.nativeSwiftRssFeedListNoNews)
        case .video: .message(.nativeSwiftRssFeedListNoVideos)
        case .podcast: .message(.nativeSwiftRssFeedListNoPodcasts)
        }
    }

    var emptyMessage: UiVerbatimText {
        switch self {
        case .news: .message(.nativeSwiftRssFeedListCheckBackForNews)
        case .video: .message(.nativeSwiftRssFeedListNoVideosAvailable)
        case .podcast: .message(.nativeSwiftRssFeedListNoPodcastsAvailable)
        }
    }
}
