@testable import VouchaAPI

let followerDistributionFixtureEndpoints: [String: Endpoint] = [
    "native.users.followers.search": Endpoint.userFollowers(
        userId: "user-abc",
        query: "al",
        limit: 25
    ),
    "native.posts.followers.share": Endpoint.sharePostWithFollowers(postId: "post-abc"),
    "native.posts.followers.send-selected": Endpoint.sendPostToFollowers(
        postId: "post-abc",
        request: try! .init(selectedRecipientIds: ["01900000-0000-7000-8000-000000000502"])
    ),
    "native.rss-feed-items.followers.share": Endpoint.shareRssFeedItemWithFollowers(
        rssFeedItemId: "01900000-0000-7000-8000-000000000503"
    ),
    "native.rss-feed-items.followers.send-selected": Endpoint.sendRssFeedItemToFollowers(
        rssFeedItemId: "01900000-0000-7000-8000-000000000503",
        request: try! .init(selectedRecipientIds: ["01900000-0000-7000-8000-000000000502"])
    )
]
