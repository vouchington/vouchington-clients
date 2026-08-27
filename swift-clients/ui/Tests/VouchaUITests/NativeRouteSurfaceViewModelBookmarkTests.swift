import Foundation
@testable import VouchaFeatures
import VouchaLocalization
import XCTest

@MainActor
final class NativeRouteSurfaceViewModelBookmarkTests: NativeRouteSurfaceViewModelTestCase {
    func testBookmarksLoadMatchedCollectionRoute() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (identityData, 200)
        CannedFeedURLProtocol.handlers["/api/v1/users/user-1/posts/hidden"] = (
            Data("""
            {
              "results": [
                {
                  "id": "post-1",
                  "slug": "native-post-1",
                  "post_type": "discussion",
                  "title": "Native Post 1",
                  "markdown": "Body",
                  "created_by_id": "user-2",
                  "created_at": "2026-01-01T00:00:00Z",
                  "privacy": "public",
                  "is_anonymous": false
                },
                {
                  "id": "post-2",
                  "slug": "native-post-2",
                  "post_type": "review",
                  "title": "Native Post 2",
                  "markdown": "Body",
                  "created_by_id": "user-2",
                  "created_at": "2026-01-01T00:00:00Z",
                  "privacy": "public",
                  "is_anonymous": false
                }
              ],
              "page_info": {
                "has_next_page": false,
                "end_cursor": null,
                "start_cursor": null
              }
            }
            """.utf8),
            200
        )
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/my/posts/hidden"))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match
        )

        await viewModel.load()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.path), [
            "/api/v1/my/identity",
            "/api/v1/users/user-1/posts/hidden"
        ])
        XCTAssertEqual(localized(viewModel.bookmarkRows.first?.title), "Native Post 1")
        XCTAssertEqual(viewModel.bookmarkRows.first?.destination, .path("/discussion/native-post-1"))
        XCTAssertEqual(localized(viewModel.bookmarkRows.last?.title), "Native Post 2")
        XCTAssertEqual(viewModel.bookmarkRows.last?.destination, .path("/review/native-post-2"))
    }

    func testBookmarksPreserveMatchedMediaAndFeedTypeFilters() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (identityData, 200)
        CannedFeedURLProtocol.handlers["/api/v1/users/user-1/rss-feed-items/viewed"] = (
            Data("""
            {
              "results": [
                {
                  "id": "item-1",
                  "title": "Viewed Item",
                  "media_type": "video",
                  "published_at": "2026-01-01T00:00:00Z",
                  "rss_feed": {
                    "id": "feed-1",
                    "title": "Example Feed",
                    "feed_type": "video",
                    "rss_feed_url": { "url": "https://example.com/rss" }
                  }
                }
              ]
            }
            """.utf8),
            200
        )
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/my/videos/viewed"))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match
        )

        await viewModel.load()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.last?.path, "/api/v1/users/user-1/rss-feed-items/viewed")
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.last?.query, "media_type=video")
        XCTAssertEqual(localized(viewModel.bookmarkRows.first?.title), "Viewed Item")
        XCTAssertNil(viewModel.bookmarkRows.first?.inverseAction)
    }

    func testBookmarksMapDismissedFriendRecommendationsRoute() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (identityData, 200)
        CannedFeedURLProtocol.handlers["/api/v1/users/user-1/users/dismissed-recommendations"] = (
            Data("""
            {
              "results": [
                { "id": "user-2", "username": "friend-2" }
              ],
              "users": {
                "user-2": {
                  "id": "user-2",
                  "username": "friend-2",
                  "name": "Friend Two",
                  "slug": "friend-two"
                }
              }
            }
            """.utf8),
            200
        )
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/my/friend-recommendations/dismissed"))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match
        )

        await viewModel.load()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.path), [
            "/api/v1/my/identity",
            "/api/v1/users/user-1/users/dismissed-recommendations"
        ])
        XCTAssertEqual(localized(viewModel.bookmarkRows.first?.title), "Friend Two")
        XCTAssertEqual(viewModel.bookmarkRows.first?.destination, .path("/user/friend-2"))
        XCTAssertEqual(viewModel.bookmarkRows.first?.inverseAction?.predicate, "dismiss_recommendation")
    }

    func testBookmarksLoadBlockedUsersCollectionRoute() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (identityData, 200)
        CannedFeedURLProtocol.handlers["/api/v1/users/user-1/users/blocked"] = (
            Data(#"{"results":[{"id":"user-2"},{"id":"user-3"}]}"#.utf8),
            200
        )
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/my/users/blocked"))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match
        )

        await viewModel.load()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.last?.path, "/api/v1/users/user-1/users/blocked")
        XCTAssertEqual(localized(viewModel.bookmarkRows.first?.title), "User")
        XCTAssertEqual(viewModel.bookmarkRows.first?.destination, .path("/user/user-2"))
    }

    func testSourceDetailLoadsBookmarkRelationState() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/rss-feeds"] = (
            Data(
                #"{"results":[{"id":"feed-1","title":"Example Source","feed_type":"article","rss_feed_url":{"url":"https://example.com/feed.xml"},"topic":{"slug":"example"}}]}"#
                    .utf8
            ),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/bookmarks/rss_feed/feed-1"] = (
            Data(#"{"bookmarks":{"follow":true,"mute":true}}"#.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/rss-feeds/feed-1"] = (
            Data(#"{"can_view_latest_crawl":false}"#.utf8),
            200
        )
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/source/example"))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match
        )

        await viewModel.load()

        XCTAssertEqual(viewModel.detailRelationEntityType, "rss_feed")
        XCTAssertEqual(viewModel.detailRelationEntityId, "feed-1")
        XCTAssertTrue(viewModel.isDetailRelationActive("follow"))
        XCTAssertTrue(viewModel.isDetailRelationActive("mute"))
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.path), [
            "/api/v1/rss-feeds",
            "/api/v1/bookmarks/rss_feed/feed-1",
            "/api/v1/rss-feeds/feed-1"
        ])
        XCTAssertEqual(
            CannedFeedURLProtocol.capturedURLs.first.flatMap {
                URLComponents(url: $0, resolvingAgainstBaseURL: false)
            }?.queryItems,
            [.init(name: "topic", value: "example")]
        )
    }

    func testTopicDetailLoadsBookmarkRelationStateWithCanonicalId() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/topics/hello-world"] = (
            Data(#"{"topic":{"id":"topic-1","name":"Hello World","slug":"hello-world","topic_type":"topic"}}"#.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/bookmarks/topic/topic-1"] = (
            Data(#"{"bookmarks":{"follow":true,"mute":false}}"#.utf8),
            200
        )
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/topic/hello-world"))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match
        )

        await viewModel.load()

        XCTAssertEqual(viewModel.detailRelationEntityType, "topic")
        XCTAssertEqual(viewModel.detailRelationEntityId, "topic-1")
        XCTAssertTrue(viewModel.isDetailRelationActive("follow"))
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.path), [
            "/api/v1/topics/hello-world",
            "/api/v1/bookmarks/topic/topic-1"
        ])
    }

    func testDetailMuteClearsFollowStateAndRollsBackOnFailure() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/topics/hello-world"] = (
            Data(#"{"topic":{"id":"topic-1","name":"Hello World","slug":"hello-world","topic_type":"topic"}}"#.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/bookmarks/topic/topic-1"] = (
            Data(#"{"bookmarks":{"follow":true,"mute":false}}"#.utf8),
            200
        )
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/topic/hello-world"))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match
        )
        await viewModel.load()

        CannedFeedURLProtocol.handlers["/api/v1/bookmarks/topic/topic-1/mute"] = (Data("{}".utf8), 500)
        await viewModel.toggleDetailRelation(predicate: "mute")

        XCTAssertTrue(viewModel.isDetailRelationActive("follow"))
        XCTAssertFalse(viewModel.isDetailRelationActive("mute"))
    }

    func testUserProfileSelfGuardSuppressesRelationToggle() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (identityData, 200)
        CannedFeedURLProtocol.handlers["/api/v1/users/user-1"] = (
            Data(#"{"user":{"id":"user-1","username":"alice","markdown":"Bio"},"profile_links":[]}"#.utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/bookmarks/user/user-1"] = (
            Data(#"{"bookmarks":{"follow":true,"mute":false,"block":false}}"#.utf8),
            200
        )
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/user/user-1"))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match
        )

        await viewModel.load()
        await viewModel.toggleDetailRelation(predicate: "mute")

        XCTAssertTrue(viewModel.detailRelationIsSelfProfile)
        XCTAssertEqual(
            CannedFeedURLProtocol.capturedURLs.filter { $0.path.contains("/bookmarks/user/user-1") }.count,
            1
        )
    }

    func testBookmarkCollectionMappingsCoverCollectionKindsAndQueryItems() throws {
        let cases: [(
            path: String,
            endpointPath: String,
            kind: NativeBookmarkCollectionKind,
            icon: String,
            queryItems: [URLQueryItem]
        )] = [
            (
                "/my/posts/saved",
                "/api/v1/users/user-1/posts/saved",
                .posts,
                "doc.text",
                [URLQueryItem(name: "limit", value: "25")]
            ),
            ("/my/topics/muted", "/api/v1/users/user-1/topics/muted", .generic, "tag", []),
            ("/my/users/followers", "/api/v1/users/user-1/users/followers", .generic, "person", []),
            (
                "/my/friend-recommendations/dismissed",
                "/api/v1/users/user-1/users/dismissed-recommendations",
                .users,
                "person",
                []
            ),
            (
                "/my/news-items/viewed",
                "/api/v1/users/user-1/rss-feed-items/viewed",
                .rssFeedItems,
                "newspaper",
                [URLQueryItem(name: "media_type", value: "article")]
            ),
            (
                "/my/rss-feed-items/saved",
                "/api/v1/users/user-1/rss-feed-items/saved",
                .rssFeedItems,
                "newspaper",
                [URLQueryItem(name: "media_type", value: "article")]
            ),
            (
                "/my/podcast-episodes/hidden",
                "/api/v1/users/user-1/rss-feed-items/hidden",
                .rssFeedItems,
                "newspaper",
                [URLQueryItem(name: "media_type", value: "audio")]
            ),
            (
                "/my/news-sources/viewed",
                "/api/v1/users/user-1/rss-feeds/viewed",
                .rssFeeds,
                "list.bullet.rectangle",
                [URLQueryItem(name: "feed_type", value: "article")]
            ),
            (
                "/my/podcasts/viewed",
                "/api/v1/users/user-1/rss-feeds/viewed",
                .rssFeeds,
                "list.bullet.rectangle",
                [URLQueryItem(name: "feed_type", value: "podcast")]
            ),
            (
                "/my/communities/proxy-following",
                "/api/v1/users/user-1/communities/proxy-following",
                .generic,
                "person.3",
                []
            )
        ]

        for testCase in cases {
            let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: testCase.path))
            let viewModel = try NativeRouteSurfaceViewModel(
                entry: entry(for: .bookmarks),
                client: nil,
                routeMatch: route.match
            )
            let collection = try XCTUnwrap(viewModel.bookmarkCollection(for: "user-1"))

            XCTAssertEqual(collection.endpoint.path, testCase.endpointPath)
            XCTAssertEqual(String(describing: collection.kind), String(describing: testCase.kind))
            XCTAssertEqual(collection.icon, testCase.icon)
            XCTAssertEqual(collection.endpoint.queryItems, testCase.queryItems)
        }
    }

    func testBookmarksLoadRssFeedCollectionsWithFeedTypeQuery() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (identityData, 200)
        CannedFeedURLProtocol.handlers["/api/v1/users/user-1/rss-feeds/viewed"] = (
            Data("""
            {
              "results": [
                {
                  "id": "feed-1",
                  "title": "Native Podcast",
                  "feed_type": "podcast",
                  "rss_feed_url": { "url": "https://example.com/podcast.xml" },
                  "topic": { "slug": "native-podcast" }
                }
              ]
            }
            """.utf8),
            200
        )
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/my/podcasts/viewed"))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .bookmarks),
            client: makeClient(),
            routeMatch: route.match
        )

        await viewModel.load()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.path), [
            "/api/v1/my/identity",
            "/api/v1/users/user-1/rss-feeds/viewed"
        ])
        let query = CannedFeedURLProtocol.capturedURLs.last.flatMap { URLComponents(
            url: $0,
            resolvingAgainstBaseURL: false
        ) }
        XCTAssertEqual(query?.queryItems?.first(where: { $0.name == "feed_type" })?.value, "podcast")
        XCTAssertEqual(localized(viewModel.bookmarkRows.first?.title), "Native Podcast")
        XCTAssertEqual(viewModel.bookmarkRows.first?.destination, .path("/source/native-podcast"))
    }

    func testFixtureBackedBookmarkRowsCoverEveryEntityKind() async throws {
        let cases: [(path: String, fixture: String, endpoint: String, destination: NativeBookmarkRow.Destination)] = [
            (
                "/my/posts/saved",
                "native.bookmarks.posts.saved.default",
                "/api/v1/users/user-1/posts/saved",
                .path("/discussion/fixture-post-1")
            ),
            (
                "/my/topics/muted",
                "native.bookmarks.topics.muted.default",
                "/api/v1/users/user-1/topics/muted",
                .path("/topic/topic-1")
            ),
            (
                "/my/users/subscribed-posts",
                "native.bookmarks.users.subscribed-posts.default",
                "/api/v1/users/user-1/users/subscribed-posts",
                .path("/user/user-3")
            ),
            (
                "/my/rss-feed-items/saved",
                "native.bookmarks.rss-feed-items.saved.default",
                "/api/v1/users/user-1/rss-feed-items/saved",
                .path("/news?rss_item=item-1")
            ),
            (
                "/my/news-sources/muted",
                "native.bookmarks.rss-feeds.muted.default",
                "/api/v1/users/user-1/rss-feeds/muted",
                .path("/source/test-topic")
            ),
            (
                "/my/urls/saved",
                "native.bookmarks.urls.saved.default",
                "/api/v1/users/user-1/urls/saved",
                .path("/url/url-1")
            ),
            (
                "/my/domains/blocked",
                "native.bookmarks.domains.blocked.default",
                "/api/v1/users/user-1/domains/blocked",
                .path("/domain/domain-1.example.com")
            ),
            (
                "/my/communities/proxy-following",
                "native.bookmarks.communities.proxy-following.default",
                "/api/v1/users/user-1/communities/proxy-following",
                .path("/communities/test-community")
            )
        ]
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (identityData, 200)
        for testCase in cases {
            CannedFeedURLProtocol.handlers[testCase.endpoint] = (ApiFixtureLoader.data(testCase.fixture), 200)
        }

        for testCase in cases {
            let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: testCase.path))
            let viewModel = try NativeRouteSurfaceViewModel(
                entry: entry(for: .bookmarks),
                client: makeClient(),
                routeMatch: route.match
            )

            await viewModel.load()

            let row = try XCTUnwrap(viewModel.bookmarkRows.first, testCase.path)
            XCTAssertEqual(row.destination, testCase.destination, testCase.path)
            XCTAssertNotEqual(localized(row.title), row.entityId, testCase.path)
            XCTAssertNotNil(row.inverseAction, testCase.path)
        }
    }

    private var identityData: Data {
        PrivateUserTestFixture.identityEnvelope()
    }

    private func localized(_ text: UiVerbatimText?) -> String? {
        text.map { UiMessages.string($0, locale: .english) }
    }
}
