import Foundation
@testable import VouchaFeatures
import VouchaLocalization
import XCTest

@MainActor
final class NativeRouteSurfaceViewModelRoutingTests: NativeRouteSurfaceViewModelTestCase {
    func testPostsBrowseFiltersByMatchedPostType() async throws {
        try await assertPostBrowse(path: "/reviews", expectedPostType: "review")
        try await assertPostBrowse(path: "/data-points", expectedPostType: "data_point")
        try await assertPostBrowse(path: "/links", expectedPostType: "link")
    }

    func testFeedPostsUsesMatchedFeedScope() async throws {
        try await assertPostFeed(path: "/feed/posts/friends", expectedPath: "/api/v1/feeds/posts/follow_users")
        try await assertPostFeed(path: "/feed/posts/topics", expectedPath: "/api/v1/feeds/posts/follow_topics")
    }

    func testRssFeedItemsUseMatchedFeedScope() async throws {
        try await assertRssFeedScope(
            path: "/feed/news/sources",
            expectedPath: "/api/v1/feeds/rss_feed_items/follow_rss_feeds"
        )
        try await assertRssFeedScope(
            path: "/feed/podcasts/friends",
            expectedPath: "/api/v1/feeds/rss_feed_items/follow_users"
        )
        try await assertRssFeedScope(
            path: "/feed/videos/topics",
            expectedPath: "/api/v1/feeds/rss_feed_items/follow_topics"
        )
    }

    func testPublicRssFeedItemsUsePublicCollectionEndpoint() async throws {
        try await assertPublicRssFeed(path: "/news", mediaType: "article")
        try await assertPublicRssFeed(path: "/podcast-episodes", mediaType: "audio")
        try await assertPublicRssFeed(path: "/videos", mediaType: "video")
    }

    func testSourceDetailLoadsMatchedRssFeed() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/rss-feeds"] = (
            rssFeedsPage(title: "Native Source", feedType: "article"),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/bookmarks/rss_feed/source-1"] = (
            Data(#"{"bookmarks":{}}"#.utf8),
            200
        )
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/source/native-source/latest"))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .sourceDetail),
            client: makeClient(),
            routeMatch: route.match
        )

        await viewModel.load()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.path), [
            "/api/v1/rss-feeds", "/api/v1/bookmarks/rss_feed/source-1", "/api/v1/rss-feeds/source-1"
        ])
        XCTAssertEqual(
            CannedFeedURLProtocol.capturedURLs.first.flatMap {
                URLComponents(url: $0, resolvingAgainstBaseURL: false)
            }?.queryItems,
            [.init(name: "topic", value: "native-source")]
        )
        XCTAssertEqual(
            viewModel.rows.first,
            .init(icon: "newspaper", title: .verbatim("Native Source"), detail: .verbatim("Article"))
        )
    }

    func testSourceDetailRejectsMissingOwningFeed() async throws {
        try await assertSourceDetailRejectsResults([])
    }

    func testSourceDetailRejectsAmbiguousOwningFeeds() async throws {
        try await assertSourceDetailRejectsResults(["source-1", "source-2"])
    }

    func testMySourceRoutesLoadCurrentUserFollowedFeeds() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (identityEnvelope(userId: "user-1"), 200)
        CannedFeedURLProtocol.handlers["/api/v1/users/user-1/rss-feeds/following"] = (
            rssFeedsPage(title: "Followed Podcast", feedType: "audio"),
            200
        )
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/my/podcasts"))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .sourcesBrowse),
            client: makeClient(),
            routeMatch: route.match
        )

        await viewModel.load()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.path), [
            "/api/v1/my/identity",
            "/api/v1/users/user-1/rss-feeds/following"
        ])
        let components = CannedFeedURLProtocol.capturedURLs.last.flatMap {
            URLComponents(url: $0, resolvingAgainstBaseURL: false)
        }
        XCTAssertEqual(components?.queryItems?.first(where: { $0.name == "feed_type" })?.value, "podcast")
        let row = try XCTUnwrap(viewModel.bookmarkRows.first)
        XCTAssertEqual(row.entityType, "rss_feed")
        XCTAssertEqual(row.entityId, "source-1")
        XCTAssertEqual(UiMessages.string(row.title, locale: .english), "Followed Podcast")
        XCTAssertEqual(UiMessages.string(row.detail, locale: .english), "Audio")
        XCTAssertEqual(row.destination, .path("/source/source-1"))
        XCTAssertEqual(
            row.inverseAction,
            .init(
                entityType: "rss_feed",
                predicate: "follow",
                label: .nativeSwiftHouseholdsBookmarksUnfollow
            )
        )
    }

    func testPublicSourceDirectoriesUseFilteredRssFeedEndpoint() async throws {
        try await assertPublicSourceDirectory(path: "/news-sources", feedType: "article")
        try await assertPublicSourceDirectory(path: "/podcasts/business", feedType: "podcast")
        try await assertPublicSourceDirectory(path: "/channels", feedType: "video")
    }

    func testRouteFilterHelpersDeriveExpectedScopesAndPostTypes() throws {
        let postFeed = try NativeRouteSurfaceViewModel(
            entry: entry(for: .feedPosts),
            client: nil,
            routeMatch: NativeRouteCatalog.matchingRoute(for: "/feed/posts/friends")?.match
        )
        XCTAssertEqual(postFeed.postFeedType, "follow_users")

        let rssFeedItems = try NativeRouteSurfaceViewModel(
            entry: entry(for: .feedNews),
            client: nil,
            routeMatch: NativeRouteCatalog.matchingRoute(for: "/feed/news/sources")?.match
        )
        XCTAssertEqual(rssFeedItems.rssFeedItemFeedType, "follow_rss_feeds")

        let mySources = try NativeRouteSurfaceViewModel(
            entry: entry(for: .sourcesBrowse),
            client: nil,
            routeMatch: NativeRouteCatalog.matchingRoute(for: "/my/channels")?.match
        )
        XCTAssertEqual(mySources.mySourceFeedType, "video")

        let browseSources = try NativeRouteSurfaceViewModel(
            entry: entry(for: .sourcesBrowse),
            client: nil,
            routeMatch: NativeRouteCatalog.matchingRoute(for: "/podcasts/business")?.match
        )
        XCTAssertEqual(browseSources.sourceBrowseFeedType, "podcast")
        XCTAssertEqual(browseSources.sourceBrowseCategory, "business")

        let browsePosts = try NativeRouteSurfaceViewModel(
            entry: entry(for: .postsBrowse),
            client: nil,
            routeMatch: NativeRouteCatalog.matchingRoute(for: "/reviews")?.match
        )
        XCTAssertEqual(browsePosts.postTypesFilter(for: .postsBrowse), "review")
        XCTAssertEqual(browsePosts.postTypesFilter(for: .storiesBrowse), "story")
        XCTAssertEqual(NativeRouteSurfaceViewModel.composePostType(for: "/links/create"), .link)
        XCTAssertEqual(NativeRouteSurfaceViewModel.composePostType(for: nil), .discussion)
    }

    private func assertPostBrowse(path: String, expectedPostType: String) async throws {
        CannedFeedURLProtocol.handlers = ["/api/v1/posts": (postFeed(postType: expectedPostType), 200)]
        CannedFeedURLProtocol.capturedURLs = []
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: path))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .postsBrowse),
            client: makeClient(),
            routeMatch: route.match
        )

        await viewModel.load()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.path, "/api/v1/posts")
        let components = CannedFeedURLProtocol.capturedURLs.first.flatMap {
            URLComponents(url: $0, resolvingAgainstBaseURL: false)
        }
        XCTAssertEqual(components?.queryItems?.first(where: { $0.name == "post_types" })?.value, expectedPostType)
    }

    private func assertPostFeed(path: String, expectedPath: String) async throws {
        CannedFeedURLProtocol.handlers = [expectedPath: (postFeed(postType: "discussion"), 200)]
        CannedFeedURLProtocol.capturedURLs = []
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: path))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .feedPosts),
            client: makeClient(),
            routeMatch: route.match
        )

        await viewModel.load()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.path, expectedPath)
    }

    private func assertPublicSourceDirectory(path: String, feedType: String) async throws {
        CannedFeedURLProtocol.handlers = [
            "/api/v1/rss-feeds": (rssFeedsPage(title: "Filtered Source", feedType: feedType), 200)
        ]
        CannedFeedURLProtocol.capturedURLs = []
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: path))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .sourcesBrowse),
            client: makeClient(),
            routeMatch: route.match
        )

        await viewModel.load()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.path, "/api/v1/rss-feeds")
        let components = CannedFeedURLProtocol.capturedURLs.first.flatMap {
            URLComponents(url: $0, resolvingAgainstBaseURL: false)
        }
        XCTAssertEqual(components?.queryItems?.first(where: { $0.name == "feed_type" })?.value, feedType)
        if path.hasPrefix("/podcasts/") {
            XCTAssertEqual(components?.queryItems?.first(where: { $0.name == "category" })?.value, "business")
        }
    }

    private func assertPublicRssFeed(path: String, mediaType: String) async throws {
        CannedFeedURLProtocol.handlers = ["/api/v1/rss-feed-items": (rssFeedItems(), 200)]
        CannedFeedURLProtocol.capturedURLs = []
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: path))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: route.entry.destinationIdentifier ?? .feedNews),
            client: makeClient(),
            routeMatch: route.match
        )

        await viewModel.load()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.path, "/api/v1/rss-feed-items")
        let components = CannedFeedURLProtocol.capturedURLs.first.flatMap {
            URLComponents(url: $0, resolvingAgainstBaseURL: false)
        }
        XCTAssertEqual(components?.queryItems?.first(where: { $0.name == "media_type" })?.value, mediaType)
    }

    private func assertRssFeedScope(path: String, expectedPath: String) async throws {
        CannedFeedURLProtocol.handlers = [expectedPath: (rssFeedItems(), 200)]
        CannedFeedURLProtocol.capturedURLs = []
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: path))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: route.entry.destinationIdentifier ?? .feedNews),
            client: makeClient(),
            routeMatch: route.match
        )

        await viewModel.load()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.path, expectedPath)
    }

    private func postFeed(postType: String) -> Data {
        Data("""
        {
          "results": [{ "id": "result-1", "entity_id": "post-1", "post_type": "\(
              postType
          )", "delivery_type": null, "shared_by_user_id": null }],
          "posts": {
            "post-1": {
              "id": "post-1",
              "slug": "native-post",
              "post_type": "\(postType)",
              "title": "Native post",
              "markdown": "Body",
              "html": null,
              "parent_id": null,
              "root_id": null,
              "created_by_id": "user-1",
              "created_at": "2026-01-01T00:00:00Z",
              "broadcast": "everyone",
              "privacy": "public",
              "is_anonymous": false,
              "community_id": null,
              "clearance_status": null
            }
          }
        }
        """.utf8)
    }

    private func rssFeedItems() -> Data {
        Data("""
        {
          "results": [{ "id": "item-1", "entity_id": "item-1", "delivery_type": null, "shared_by_user_id": null, "published_at": "2026-01-01T00:00:00Z" }],
          "rss_feed_items": {
            "item-1": {
              "id": "item-1",
              "data": {
                "title": "Native item",
                "link": "https://example.com/item"
              },
              "media_type": "article",
              "published_at": "2026-01-01T00:00:00Z",
              "rss_feed": {
                "id": "feed-1",
                "title": "Example Feed",
                "feed_type": "article",
                "rss_feed_url": { "url": "https://example.com/rss" }
              }
            }
          }
        }
        """.utf8)
    }

    private func assertSourceDetailRejectsResults(_ ids: [String]) async throws {
        CannedFeedURLProtocol.handlers["/api/v1/rss-feeds"] = (
            rssFeedsPage(title: "Native Source", feedType: "article", ids: ids),
            200
        )
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/source/native-source"))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .sourceDetail),
            client: makeClient(),
            routeMatch: route.match
        )

        await viewModel.load()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.path), ["/api/v1/rss-feeds"])
        XCTAssertNil(viewModel.detailRelationEntityId)
        guard case .error = viewModel.state else {
            return XCTFail("Expected an ambiguous or missing source result to fail loading")
        }
    }

    private func rssFeedsPage(title: String, feedType: String, ids: [String] = ["source-1"]) -> Data {
        let results = ids.map { id in
            """
            {
              "id": "\(id)",
              "title": "\(title)",
              "feed_type": "\(feedType)",
              "rss_feed_url": { "url": "https://example.com/rss" }
            }
            """
        }.joined(separator: ",")
        return Data("""
        {
          "results": [\(results)]
        }
        """.utf8)
    }

    private func identityEnvelope(userId: String) -> Data {
        PrivateUserTestFixture.identityEnvelope(
            id: userId,
            membershipPlan: nil,
            verificationStatus: nil
        )
    }
}
