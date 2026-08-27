import VouchaAPI

extension EndpointManifestCoverage {
    static let bookmarkEndpoints: [ManifestRegisteredEndpoint] = [
        ManifestRegisteredEndpoint(id: "native.bookmarks.posts.saved.default") {
            Endpoint.userPosts(userId: "user-abc", listType: "saved")
        },
        ManifestRegisteredEndpoint(id: "native.bookmarks.posts.saved.next-page") {
            Endpoint.userPosts(
                userId: "user-abc",
                listType: "saved",
                after: "eyJ0aW1lc3RhbXAiOjE3NjcyMjU2MDAwMDAwMDAsImlkIjoiMDAwMDAwMDAtMDAwMC03MDAwLTgwMDAtMDAwMDAwMDAwMDAxIiwic2NvcGUiOiJ1c2VyLXBvc3RzOjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDA5OTpzYXZlZDpjcmVhdGVkLWF0LWRlc2Mtb2JqZWN0LWlkLWRlc2MifQ"
            )
        },
        ManifestRegisteredEndpoint(id: "native.bookmarks.topics.muted.default") {
            Endpoint.userTopics(userId: "user-abc", listType: "muted")
        },
        ManifestRegisteredEndpoint(id: "native.bookmarks.topics.viewed.default") {
            Endpoint.userTopics(userId: "user-abc", listType: "viewed")
        },
        ManifestRegisteredEndpoint(id: "native.bookmarks.users.subscribed-posts.default") {
            Endpoint.userUsers(userId: "user-abc", listType: "subscribed-posts")
        },
        ManifestRegisteredEndpoint(id: "native.bookmarks.users.dismissed-recommendations.default") {
            Endpoint.userUsers(userId: "user-abc", listType: "dismissed-recommendations")
        },
        ManifestRegisteredEndpoint(id: "native.bookmarks.rss-feed-items.saved.default") {
            Endpoint.userRssFeedItems(userId: "user-abc", listType: "saved", mediaType: "article")
        },
        ManifestRegisteredEndpoint(id: "native.bookmarks.rss-feeds.muted.default") {
            Endpoint.userRssFeeds(userId: "user-abc", listType: "muted", feedType: "article", limit: 25)
        },
        ManifestRegisteredEndpoint(id: "native.bookmarks.rss-feeds.viewed.default") {
            Endpoint.userRssFeeds(userId: "user-abc", listType: "viewed", feedType: "podcast", limit: 25)
        },
        ManifestRegisteredEndpoint(id: "native.bookmarks.urls.saved.default") {
            Endpoint.userUrls(userId: "user-abc", listType: "saved")
        },
        ManifestRegisteredEndpoint(id: "native.bookmarks.domains.blocked.default") {
            Endpoint.userHostnames(userId: "user-abc", listType: "blocked")
        },
        ManifestRegisteredEndpoint(id: "native.bookmarks.domains.muted.default") {
            Endpoint.userHostnames(userId: "user-abc", listType: "muted")
        },
        ManifestRegisteredEndpoint(id: "native.bookmarks.communities.proxy-following.default") {
            Endpoint.userCommunities(userId: "user-abc", listType: "proxy-following")
        }
    ]
}
