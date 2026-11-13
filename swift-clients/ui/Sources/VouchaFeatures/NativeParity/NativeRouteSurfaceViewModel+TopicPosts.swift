import Foundation
import VouchaAPI

extension NativeRouteSurfaceViewModel {
    /// Only existing topic post-collection subroutes use the post list. The topic root remains detail.
    var topicPostCollection: String? {
        guard destination == .topicDetail || destination == .sourceDetail,
              let path = routeMatch?.path,
              path.split(separator: "/").count == 3,
              let collection = path.split(separator: "/").last.map(String.init),
              ["posts", "discussions", "reviews", "data-points"].contains(collection)
        else { return nil }
        return collection
    }

    var topicPostSort: String {
        guard let collection = topicPostCollection else { return "hot" }
        return collection == "data-points" || !isSignedIn ? "new" : "following_new"
    }

    var topicPostType: String? {
        switch topicPostCollection {
        case "reviews": "review"
        case "data-points": "data_point"
        default: nil
        }
    }

    func topicPostQueryItems(client: APIClient) async throws -> [URLQueryItem] {
        guard let collection = topicPostCollection else { return [] }
        if topicDetailId == nil {
            let response: NativeTopicDetailResponse = try await client.send(.topic(id: routeEntityId))
            topicDetailId = response.topic.id
        }
        let key = switch collection {
        case "reviews": "review_topic"
        case "data-points": "data_point_topic"
        default: "topics"
        }
        return [.init(name: key, value: topicDetailId)]
    }

    func postListQueryItems(client: APIClient, after: String?, postTypes: String?) async throws -> [URLQueryItem] {
        var query = [
            URLQueryItem(name: "limit", value: "10"),
            URLQueryItem(name: "sort", value: topicPostSort),
            URLQueryItem(name: "time_range", value: postTimeRangeQueryValue)
        ]
        try await query.append(contentsOf: topicPostQueryItems(client: client))
        if hasHackerNewsReferences, isHackerNewsFilterVisible {
            query.append(.init(name: "has_hacker_news_references", value: "true"))
        }
        if let postTypes {
            query.append(.init(name: "post_types", value: postTypes))
        }
        if let after {
            query.append(.init(name: "after", value: after))
        }
        return query
    }
}
