import Foundation
import VouchaAPI
import VouchaLocalization
import VouchaModels

extension NativeRouteSurfaceViewModel {
    var supportsHackerNewsFilter: Bool {
        switch destination {
        case .postsBrowse, .storiesBrowse, .feedNews, .feedPodcasts, .feedVideos: true
        default: false
        }
    }

    func hackerNewsDetail(_ detail: UiVerbatimText, references: HackerNewsReferences?) -> UiVerbatimText {
        guard let references else { return detail }
        var parts = [detail]
        if references.storyCount > 0 {
            parts.append(.app(UiMessage(
                .sharedHackerNewsSeenOnCount,
                numberParameters: ["count": Double(references.storyCount)]
            )))
        }
        if references.commentCount > 0 {
            parts.append(.app(UiMessage(
                references.storyCount > 0 ? .sharedHackerNewsMentionedCount : .sharedHackerNewsMentionedOnCount,
                numberParameters: ["count": Double(references.commentCount)]
            )))
        }
        return .composition(parts, separator: " · ")
    }

    func hackerNewsPostFeedEndpoint(after: String?) -> Endpoint {
        let postTypes = postTypesFilter(for: destination ?? .feedPosts)
        let endpoint: Endpoint
        if destination == .postsBrowse || destination == .storiesBrowse {
            var query = [
                URLQueryItem(name: "limit", value: "10"),
                URLQueryItem(name: "sort", value: "hot")
            ]
            if let postTypes {
                query.append(.init(name: "post_types", value: postTypes))
            }
            if hasHackerNewsReferences { query.append(.init(name: "has_hacker_news_references", value: "true")) }
            if let after {
                query.append(.init(name: "after", value: after))
            }
            endpoint = Endpoint(.GET, path: "/api/v1/posts", queryItems: query)
        } else {
            endpoint = .posts(
                feedType: postFeedType,
                after: after,
                limit: 10,
                postTypes: postTypes,
                timeRange: PostTimeRange.lastWeek.rawValue
            )
        }
        return endpoint
    }

}
