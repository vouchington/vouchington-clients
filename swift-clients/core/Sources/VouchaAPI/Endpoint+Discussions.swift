private struct EmptyBody: Encodable {}

public extension Endpoint {
    static func createStoryDiscussion(storyId: String, idempotencyKey: String) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/stories/\(pathSegment(storyId))/discussions",
            headers: ["Idempotency-Key": idempotencyKey],
            body: EmptyBody()
        )
    }

    static func createRssFeedItemDiscussion(rssFeedItemId: String, idempotencyKey: String) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/rss-feed-items/\(pathSegment(rssFeedItemId))/discussions",
            headers: ["Idempotency-Key": idempotencyKey],
            body: EmptyBody()
        )
    }
}
