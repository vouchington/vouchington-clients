private struct EmptyBody: Encodable {}

public extension Endpoint {
    static func createStoryDiscussion(storyId: String) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/stories/\(pathSegment(storyId))/discussions",
            body: EmptyBody()
        )
    }

    static func createRssFeedItemDiscussion(rssFeedItemId: String) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/rss-feed-items/\(pathSegment(rssFeedItemId))/discussions",
            body: EmptyBody()
        )
    }
}
