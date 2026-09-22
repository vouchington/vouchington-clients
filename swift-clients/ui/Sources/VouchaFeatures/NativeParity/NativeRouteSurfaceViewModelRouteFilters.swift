import VouchaModels

extension NativeRouteSurfaceViewModel {
    var postFeedType: String {
        switch routeMatch?.path {
        case "/feed/posts/friends":
            "follow_users"
        case "/feed/posts/topics":
            "follow_topics"
        default:
            "any"
        }
    }

    var rssFeedItemFeedType: String {
        guard let path = routeMatch?.path else { return "any" }
        if path.hasSuffix("/friends") {
            return "follow_users"
        }
        if path.hasSuffix("/sources") {
            return "follow_rss_feeds"
        }
        if path.hasSuffix("/topics") {
            return "follow_topics"
        }
        return "any"
    }

    var mySourceFeedType: String? {
        switch routeMatch?.path {
        case "/my/news-sources":
            "article"
        case "/my/podcasts":
            "podcast"
        case "/my/channels":
            "video"
        default:
            nil
        }
    }

    var sourceBrowseFeedType: String? {
        guard let path = routeMatch?.path else { return nil }
        return switch path {
        case "/news-sources":
            "article"
        case "/podcasts":
            "podcast"
        case "/channels":
            "video"
        default:
            path.hasPrefix("/podcasts/") ? "podcast" : nil
        }
    }

    var sourceBrowseCategory: String? {
        guard routeMatch?.path.hasPrefix("/podcasts/") == true else { return nil }
        return routeMatch?.param("category")
    }

    func postTypesFilter(for destination: NativeRouteDestinationIdentifier) -> String? {
        if destination == .storiesBrowse {
            return PostType.story.rawValue
        }
        guard destination == .postsBrowse else { return nil }
        return switch routeMatch?.path {
        case "/reviews":
            PostType.review.rawValue
        case "/discussions":
            PostType.discussion.rawValue
        case "/articles":
            PostType.article.rawValue
        case "/blog":
            PostType.blogPost.rawValue
        case "/data-points":
            PostType.dataPoint.rawValue
        case "/links":
            PostType.link.rawValue
        default:
            nil
        }
    }

    static func composePostType(for routePath: String?) -> PostType {
        switch routePath {
        case "/reviews/create":
            .review
        case "/articles/create":
            .article
        case "/blog/create":
            .blogPost
        case "/links/create":
            .link
        case "/data-points/create":
            .dataPoint
        default:
            .discussion
        }
    }
}
