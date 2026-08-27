import Foundation
@testable import VouchaFeatures
import XCTest

@MainActor
final class NativeRouteSurfaceViewModelTests: NativeRouteSurfaceViewModelTestCase {
    override func setUp() {
        super.setUp()
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.queuedHandlers = [:]
        CannedFeedURLProtocol.capturedURLs = []
        CannedFeedURLProtocol.capturedMethods = []
    }

    func testSearchCallsCombinedSearchEndpointAndRendersGroupedResults() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/search"] = (
            Data("""
            {
              "topics": [{ "id": "topic-1", "name": "Native Swift", "slug": "native-swift", "topic_type": "topic" }],
              "posts": [{ "id": "post-1", "post_type": "discussion", "title": "Native post" }],
              "news": [{ "id": "news-1", "url": "https://example.com/native", "title": "Native result", "feed_title": "Example Feed" }],
              "domains": [],
              "communities": [
                { "id": "community-1", "name": "Open Club", "slug": "open-club", "bookmarked": false },
                { "id": "community-2", "name": "Saved Club", "slug": "saved-club", "bookmarked": true },
                { "id": "community-3", "name": "Alpha Club", "slug": "alpha-club", "bookmarked": true }
              ]
            }
            """.utf8),
            200
        )
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .webSearch), client: makeClient())

        await viewModel.search(query: "native swift")

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.path, "/api/v1/search")
        let components = CannedFeedURLProtocol.capturedURLs.first.flatMap {
            URLComponents(url: $0, resolvingAgainstBaseURL: false)
        }
        XCTAssertEqual(components?.queryItems?.first(where: { $0.name == "q" })?.value, "native swift")
        XCTAssertEqual(
            viewModel.searchSections.map { uiEnglish($0.title) },
            ["Topics", "Posts", "News", "Communities"]
        )
        XCTAssertEqual(viewModel.rows.first, verbatimRow(icon: "tag", title: "Topic: Native Swift", detail: "topic"))
        XCTAssertEqual(
            viewModel.searchSections.last?.rows.map(\.title),
            ["Community: Saved Club", "Community: Alpha Club", "Community: Open Club"]
        )
    }

    func testFeedPostsLoadsNativePostFeed() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/feeds/posts/any"] = (
            Data("""
            {
              "results": [
                {
                  "id": "result-1",
                  "entity_id": "post-1",
                  "post_type": "discussion",
                  "delivery_type": "share",
                  "shared_by_user_id": "user-2"
                }
              ],
              "posts": {
                "post-1": {
                  "id": "post-1",
                  "slug": "native-post",
                  "post_type": "discussion",
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
            """.utf8),
            200
        )
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .feedPosts), client: makeClient())

        await viewModel.load()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.path, "/api/v1/feeds/posts/any")
        let components = CannedFeedURLProtocol.capturedURLs.first.flatMap {
            URLComponents(url: $0, resolvingAgainstBaseURL: false)
        }
        XCTAssertEqual(components?.queryItems?.first(where: { $0.name == "sort" })?.value, "hot")
        XCTAssertEqual(
            viewModel.rows.first,
            verbatimRow(icon: "doc.text", title: "Native post", detail: "Shared by user-2")
        )
    }

    func testFeedNewsLoadsNativeRssItems() async throws {
        try await assertRssFeedItemLoad(
            destination: .feedNews,
            mediaType: "article",
            title: "Native article",
            detail: "Example Feed"
        )
    }

    func testFeedPodcastsLoadsNativeRssItems() async throws {
        try await assertRssFeedItemLoad(
            destination: .feedPodcasts,
            mediaType: "audio",
            title: "Native episode",
            detail: "Podcast Feed"
        )
    }

    func testFeedVideosLoadsNativeRssItems() async throws {
        try await assertRssFeedItemLoad(
            destination: .feedVideos,
            mediaType: "video",
            title: "Native video",
            detail: "Video Feed"
        )
    }

    func testSourcesBrowseLoadsNativeSourceList() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/rss-feeds/trending"] = (
            Data("""
            {
              "results": [{ "id": "source-1" }],
              "rss_feeds": {
                "source-1": {
                  "id": "source-1",
                  "title": "Native Source",
                  "feed_type": "article",
                  "rss_feed_url": { "url": "https://example.com/rss" }
                }
              }
            }
            """.utf8),
            200
        )
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .sourcesBrowse), client: makeClient())

        await viewModel.load()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.path, "/api/v1/rss-feeds/trending")
        XCTAssertEqual(
            viewModel.rows.first,
            NativeRouteDestinationRow(
                icon: "newspaper",
                title: .verbatim("Native Source"),
                detail: .message(.nativeSwiftRouteSurfaceArticle)
            )
        )
    }

    func testPostDetailLoadsMatchedPost() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/posts/native-review"] = (
            Data("""
            {
              "post": {
                "id": "native-review",
                "slug": "native-review",
                "post_type": "review",
                "title": "Native review",
                "markdown": "Specific review body",
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
            """.utf8),
            200
        )
        let match = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/review/native-review")?.match)
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .postDetail),
            client: makeClient(),
            routeMatch: match
        )

        await viewModel.load()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.path, "/api/v1/posts/native-review")
        XCTAssertEqual(
            viewModel.rows.first,
            verbatimRow(icon: "doc.text", title: "Native review", detail: "Specific review body")
        )
    }

    func testArticleCommunityGuidelinesLoadsAsPostDetail() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/posts/community-guidelines"] = (
            Data("""
            {
              "post": {
                "id": "community-guidelines",
                "slug": "community-guidelines",
                "post_type": "article",
                "title": "Community Guidelines",
                "markdown": "Native community guidelines",
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
            """.utf8),
            200
        )
        let match = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/article/community-guidelines")?.match)
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .postDetail),
            client: makeClient(),
            routeMatch: match
        )

        await viewModel.load()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.path, "/api/v1/posts/community-guidelines")
        XCTAssertEqual(
            viewModel.rows.first,
            verbatimRow(icon: "newspaper", title: "Community Guidelines", detail: "Native community guidelines")
        )
    }

    func testTopicDetailLoadsMatchedTopic() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/topics/swift"] = (
            Data("""
            {
              "topic": {
                "id": "topic-1",
                "slug": "swift",
                "name": "Swift",
                "description": "Native topic"
              }
            }
            """.utf8),
            200
        )
        let match = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/topic/swift/posts")?.match)
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .topicDetail),
            client: makeClient(),
            routeMatch: match
        )

        await viewModel.load()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.path, "/api/v1/topics/swift")
        XCTAssertEqual(viewModel.rows.first, verbatimRow(icon: "tag", title: "Swift", detail: "Native topic"))
    }

    func testUsersBrowseDoesNotLoadWithoutQuery() async throws {
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .usersBrowse), client: makeClient())

        await viewModel.load()

        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.isEmpty)
        XCTAssertTrue(viewModel.rows.isEmpty)
    }

    func testNoClientKeepsStaticRowsAndDoesNotLoadRemoteSurface() async throws {
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .plans), client: nil)

        await viewModel.load()

        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.isEmpty)
        XCTAssertEqual(viewModel.rows.first?.title, "Plans")
    }

    private func assertRssFeedItemLoad(
        destination: NativeRouteDestinationIdentifier,
        mediaType: String,
        title: String,
        detail: String
    ) async throws {
        CannedFeedURLProtocol.handlers["/api/v1/feeds/rss_feed_items/any"] = (
            Data("""
            {
              "results": [{ "id": "item-1", "entity_id": "item-1", "delivery_type": "share", "shared_by_user_id": null, "published_at": "2026-01-01T00:00:00Z" }],
              "rss_feed_items": {
                "item-1": {
                  "id": "item-1",
                  "title": "\(title)",
                  "link": "https://example.com/item-1",
                  "media_type": "\(mediaType)",
                  "published_at": "2026-01-01T00:00:00Z",
                  "rss_feed": {
                    "id": "feed-1",
                    "title": "\(detail)",
                    "feed_type": "article",
                    "rss_feed_url": { "url": "https://example.com/rss" }
                  }
                }
              }
            }
            """.utf8),
            200
        )
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: destination), client: makeClient())

        await viewModel.load()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.path, "/api/v1/feeds/rss_feed_items/any")
        let components = CannedFeedURLProtocol.capturedURLs.first.flatMap {
            URLComponents(url: $0, resolvingAgainstBaseURL: false)
        }
        XCTAssertEqual(components?.queryItems?.first(where: { $0.name == "media_type" })?.value, mediaType)
        XCTAssertEqual(
            viewModel.rows.first,
            verbatimRow(icon: expectedIcon(for: mediaType), title: title, detail: detail)
        )
    }

    private func expectedIcon(for mediaType: String) -> String {
        switch mediaType {
        case "audio":
            "headphones"
        case "video":
            "play.rectangle"
        default:
            "newspaper"
        }
    }

}
