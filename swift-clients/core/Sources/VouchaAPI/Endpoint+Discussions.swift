import VouchaModels

private struct StoryPostBody: Encodable {
    let postType = PostType.story
    let storyId: String
}

private struct RssFeedItemPostBody: Encodable {
    let postType = PostType.link
    let rssFeedItemId: String
}

public extension Endpoint {
    /// Starts a story discussion through `POST /api/v1/posts` with `post_type` `story` and `story_id`.
    static func createStoryDiscussion(storyId: String, idempotencyKey: String) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/posts",
            headers: ["Idempotency-Key": idempotencyKey],
            body: StoryPostBody(storyId: storyId)
        )
    }

    /// Starts an RSS item discussion through `POST /api/v1/posts` with `post_type` `link` and `rss_feed_item_id`.
    static func createRssFeedItemDiscussion(rssFeedItemId: String, idempotencyKey: String) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/posts",
            headers: ["Idempotency-Key": idempotencyKey],
            body: RssFeedItemPostBody(rssFeedItemId: rssFeedItemId)
        )
    }

    /// Creates a topic recommendation through `POST /api/v1/posts`; `body` carries `post_type` and the fields.
    static func createTopicRecommendation(body: some Encodable & Sendable, idempotencyKey: String?) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/posts",
            headers: idempotencyKey.map { ["Idempotency-Key": $0] } ?? [:],
            body: body
        )
    }
}
