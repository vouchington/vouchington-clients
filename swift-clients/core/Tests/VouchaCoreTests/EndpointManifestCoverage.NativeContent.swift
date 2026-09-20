import VouchaAPI

extension EndpointManifestCoverage {
    static let nativeContentEndpoints: [ManifestRegisteredEndpoint] = [
        ManifestRegisteredEndpoint(id: "native.comments.post-detail.default") {
            Endpoint.post(idOrSlug: "comment-root")
        },
        ManifestRegisteredEndpoint(id: "native.comments.descendants.default") {
            Endpoint.postDescendants(
                postId: "comment-root-post",
                after: "fixture-root-and-subtree-scoped-cursor",
                limit: 2
            )
        },
        ManifestRegisteredEndpoint(id: "native.comments.ancestors.permalink") {
            Endpoint.postAncestors(postId: "comment-b")
        },
        ManifestRegisteredEndpoint(id: "native.comments.ancestors.bounded.shallow") {
            Endpoint.postAncestors(postId: "comment-b", limit: 5)
        },
        ManifestRegisteredEndpoint(id: "native.comments.ancestors.bounded.deep-initial") {
            Endpoint.postAncestors(postId: "bounded-ancestor-comment-7", limit: 5)
        },
        ManifestRegisteredEndpoint(id: "native.comments.ancestors.bounded.deep-continuation") {
            Endpoint.postAncestors(
                postId: "bounded-ancestor-comment-7",
                after: "fixture-ancestor-deep-initial-end",
                limit: 5
            )
        },
        ManifestRegisteredEndpoint(id: "native.lists.default") {
            Endpoint.lists()
        },
        ManifestRegisteredEndpoint(id: "native.list-items.default") {
            Endpoint.listItems(listId: "list-1")
        },
        ManifestRegisteredEndpoint(id: "native.lists-containing.default") {
            Endpoint.listsContaining(itemType: "rss_feed_item", entityId: "item-1")
        },
        ManifestRegisteredEndpoint(id: "native.list-import.default") {
            Endpoint.importCommunityList(listId: "list-1", communitySlug: "test-community")
        },
        ManifestRegisteredEndpoint(id: "native.landing-pages.default") {
            Endpoint.myLandingPages
        },
        ManifestRegisteredEndpoint(id: "native.landing-page-detail.default") {
            Endpoint.myLandingPage(id: "landing-page-1")
        },
        ManifestRegisteredEndpoint(id: "native.landing-page-analytics.default") {
            Endpoint.myLandingPageAnalytics(pageId: "landing-page-1")
        },
        ManifestRegisteredEndpoint(id: "native.admin-user-landing-pages.default") {
            Endpoint.adminUserLandingPages(userId: "user-abc")
        },
        ManifestRegisteredEndpoint(id: "native.admin-landing-page-analytics.default") {
            Endpoint.adminLandingPageAnalytics(pageId: "landing-page-1")
        },
        ManifestRegisteredEndpoint(id: "native.landing-page-candidates.default") {
            Endpoint.myLandingPageCandidates
        },
        ManifestRegisteredEndpoint(id: "native.landing-page-items-mutation.default") {
            nativeLandingPageItemsMutationEndpoint()
        },
        ManifestRegisteredEndpoint(id: "native.landing-page-mutation.default") {
            Endpoint.updateMyLandingPage(
                id: "landing-page-1",
                body: LandingPageMetadataBody(title: "Updated Links", slug: "updated-links")
            )
        },
        ManifestRegisteredEndpoint(id: "native.users.profile.default") {
            Endpoint.user(idOrSlug: "alice", includeBio: true)
        },
        ManifestRegisteredEndpoint(id: "native.users.profile.restricted") {
            Endpoint.user(idOrSlug: "restricted", includeBio: true)
        },
        ManifestRegisteredEndpoint(id: "native.users.vouch-context.default") {
            Endpoint.userTrustContext(userId: "user-abc")
        },
        ManifestRegisteredEndpoint(id: "native.users.profile.posts.all") {
            Endpoint.userPosts(userId: "user-abc", postTypes: "review,discussion,comment")
        },
        ManifestRegisteredEndpoint(id: "native.users.profile.posts.reviews") {
            Endpoint.userPosts(userId: "user-abc", postTypes: "review")
        },
        ManifestRegisteredEndpoint(id: "native.users.profile.posts.discussions") {
            Endpoint.userPosts(userId: "user-abc", postTypes: "discussion")
        },
        ManifestRegisteredEndpoint(id: "native.users.profile.posts.comments") {
            Endpoint.userPosts(userId: "user-abc", postTypes: "comment")
        },
        ManifestRegisteredEndpoint(id: "native.users.profile.topics-following.first-page") {
            Endpoint.userTopicsFollowing(userId: "user-abc")
        },
        ManifestRegisteredEndpoint(id: "native.users.profile.topics-following.next-page") {
            Endpoint.userTopicsFollowing(
                userId: "user-abc",
                after: "eyJ0aW1lc3RhbXAiOjE3ODI5MjE2MDAwMDAwMDAsImlkIjoiMDAwMDAwMDAtMDAwMC03MDAwLTgwMDAtMDAwMDAwMDAwMDAxIn0"
            )
        },
        ManifestRegisteredEndpoint(id: "native.users.profile.sources-following.article") {
            Endpoint.userRssFeeds(userId: "user-abc", feedType: "article", limit: 25)
        },
        ManifestRegisteredEndpoint(id: "native.users.profile.communities-member.first-page") {
            Endpoint.userCommunitiesMember(userId: "user-abc")
        },
        ManifestRegisteredEndpoint(id: "native.users.profile.communities-member.next-page") {
            Endpoint.userCommunitiesMember(
                userId: "user-abc",
                after: "eyJpZCI6IjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDAwMiJ9"
            )
        },
        ManifestRegisteredEndpoint(id: "native.hostnames.default") {
            Endpoint.hostnames(query: "example")
        },
        ManifestRegisteredEndpoint(id: "native.hostname.default") {
            Endpoint.hostname(idOrHostname: "hostname-1")
        },
        ManifestRegisteredEndpoint(id: "native.urls.default") {
            Endpoint.urls(query: "example")
        },
        ManifestRegisteredEndpoint(id: "native.url.default") {
            Endpoint.url(urlId: "url-1")
        },
        ManifestRegisteredEndpoint(id: "native.url-crawls.default") {
            Endpoint.urlCrawls(urlId: "url-1")
        },
        ManifestRegisteredEndpoint(id: "native.paid.url-crawls.default") {
            Endpoint.urlCrawls(urlId: "url-1")
        },
        ManifestRegisteredEndpoint(id: "web.paid.rss-feed-crawls.default") {
            Endpoint.rssFeedCrawls(id: "rss-feed-1")
        },
        ManifestRegisteredEndpoint(id: "web.paid.rss-feed-crawl.default") {
            Endpoint.rssFeedCrawl(id: "rss-feed-1", crawlId: "crawl-1")
        },
        ManifestRegisteredEndpoint(id: "web.admin.rss-feed-crawl.default") {
            Endpoint.rssFeedCrawl(id: "rss-feed-1", crawlId: "crawl-1")
        },
        ManifestRegisteredEndpoint(id: "native.paid.url-crawl.default") {
            Endpoint.urlCrawl(urlId: "url-1", crawlId: "crawl-1")
        },
        ManifestRegisteredEndpoint(id: "native.url-crawl.default") {
            Endpoint.urlCrawl(urlId: "url-1", crawlId: "crawl-1")
        },
        ManifestRegisteredEndpoint(id: "native.url-crawl-trigger.default") {
            Endpoint.triggerUrlCrawl(urlId: "url-1")
        }
    ]
}
