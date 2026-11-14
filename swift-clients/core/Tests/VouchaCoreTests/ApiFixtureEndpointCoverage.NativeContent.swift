@testable import VouchaAPI

private let threadRootId = "70000000-0000-7000-8000-000000005400"
private let commentThreadId = "70000000-0000-7000-8000-000000005430"
private let nestedCommentId = "70000000-0000-7000-8000-000000005402"
private let rssFeedId = "70000000-0000-7000-8000-000000003680"
private let rssImportId = "70000000-0000-7000-8000-000000003681"
private let mergeSourceId = "70000000-0000-7000-8000-000000005170"
private let mergeDestinationId = "70000000-0000-7000-8000-000000005171"
private let mergeOperationId = "70000000-0000-7000-8000-000000005172"

private func fixtureCursor(_ id: String) -> String {
    ApiFixtureLoader.queryValue(id, "after")
}

/// Endpoints for the native content-browsing, RSS lifecycle and topic-merge fixtures.
let nativeContentFixtureEndpoints: [String: Endpoint] = nativeCommentFixtureEndpoints
    .merging(nativeFeedFixtureEndpoints) { _, replacement in replacement }
    .merging(nativeLifecycleFixtureEndpoints) { _, replacement in replacement }

private let nativeCommentFixtureEndpoints: [String: Endpoint] = [
    "native.comments.descendants.default": Endpoint.postDescendants(postId: threadRootId, limit: 3),
    "native.comments.descendants.short-continuation": Endpoint.postDescendants(postId: commentThreadId, limit: 3),
    "native.comments.descendants.empty-continuation": Endpoint.postDescendants(
        postId: commentThreadId,
        after: fixtureCursor("native.comments.descendants.empty-continuation"),
        limit: 1
    ),
    "native.comments.descendants.after-filtered-head": Endpoint.postDescendants(
        postId: commentThreadId,
        after: fixtureCursor("native.comments.descendants.after-filtered-head"),
        limit: 3
    ),
    "native.comments.descendants.expand-preview-child": Endpoint.postDescendants(
        postId: "70000000-0000-7000-8000-000000005434",
        limit: 3
    ),
    "native.comments.ancestors.permalink": Endpoint.postAncestors(postId: nestedCommentId),
    "native.comments.ancestors.bounded.shallow": Endpoint.postAncestors(postId: nestedCommentId, limit: 5),
    "native.comments.ancestors.bounded.deep-initial": Endpoint.postAncestors(
        postId: "bounded-ancestor-comment-7",
        limit: 5
    ),
    "native.comments.ancestors.bounded.deep-continuation": Endpoint.postAncestors(
        postId: "bounded-ancestor-comment-7",
        after: "fixture-ancestor-deep-initial-end",
        limit: 5
    )
]

private let nativeFeedFixtureEndpoints: [String: Endpoint] = [
    "native.posts.hacker-news.detail": Endpoint.post(idOrSlug: "comment-root"),
    "native.posts.hacker-news.filtered": Endpoint.postsFeed(
        postTypes: "review",
        timeRange: "1y",
        hasHackerNewsReferences: true
    ),
    "native.posts.time-range.all": Endpoint.postsFeed(timeRange: "all", sort: "new"),
    "native.posts.time-range.explicit-week": Endpoint.postsFeed(timeRange: "1w", sort: "new"),
    "native.rss-feed-items.hacker-news.detail": Endpoint.rssFeedItem(id: "00000000-0000-7000-8000-000000007886"),
    "native.rss-feed-items.hacker-news.list": Endpoint.allRssFeedItems(),
    "native.podcast-episodes.hacker-news.list": Endpoint.rssFeedItems(mediaType: "audio"),
    "native.rss-personal.one-day": Endpoint.personalRssFeedItems(timeRange: "1d"),
    "native.rss-personal.seven-day.1m": Endpoint.personalRssFeedItems(timeRange: "1m"),
    "native.rss-personal.seven-day.1y": Endpoint.personalRssFeedItems(timeRange: "1y"),
    "native.rss-personal.seven-day.all": Endpoint.personalRssFeedItems(timeRange: "all"),
    "native.rss-personal.union.first-page": Endpoint.personalRssFeedItems(timeRange: "all", limit: 2),
    "native.rss-personal.union.next-page": Endpoint.personalRssFeedItems(
        timeRange: "all",
        after: fixtureCursor("native.rss-personal.union.next-page"),
        limit: 2
    )
]

private let nativeLifecycleFixtureEndpoints: [String: Endpoint] = [
    "native.rss-feeds.create.accepted": Endpoint.createSource(
        rssFeedURL: "https://example.test/feed.xml",
        follow: true
    ),
    "native.rss-feeds.create.status.pending": Endpoint.rssFeedImportStatus(importId: rssImportId),
    "native.rss-feeds.create.status.completed": Endpoint.rssFeedImportStatus(importId: rssImportId),
    "native.rss-feeds.create.status.failed": Endpoint.rssFeedImportStatus(importId: rssImportId),
    "native.rss-feeds.delete.accepted": Endpoint.deleteRssFeed(rssFeedId: rssFeedId),
    "native.rss-feeds.delete.retained-import-conflict": Endpoint.deleteRssFeed(rssFeedId: rssFeedId),
    "native.topics.merge.accepted": Endpoint.mergeTopicAliases(
        sourceTopicId: mergeSourceId,
        destinationIdOrSlug: mergeDestinationId
    ),
    "native.topics.merge.status.queued": Endpoint.topicMergeStatus(
        sourceTopicId: mergeSourceId,
        operationId: mergeOperationId
    ),
    "native.topics.merge.status.running": Endpoint.topicMergeStatus(
        sourceTopicId: mergeSourceId,
        operationId: mergeOperationId
    ),
    "native.topics.merge.status.completed": Endpoint.topicMergeStatus(
        sourceTopicId: mergeSourceId,
        operationId: mergeOperationId
    ),
    "native.topics.merge.status.failed": Endpoint.topicMergeStatus(
        sourceTopicId: mergeSourceId,
        operationId: mergeOperationId
    )
]
