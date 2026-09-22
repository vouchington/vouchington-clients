import Foundation
@testable import VouchaFeatures
import VouchaLocalization
import VouchaModels
import XCTest

@MainActor
final class NativeBookmarkCollectionActionTests: NativeRouteSurfaceViewModelTestCase {
    func testCanonicalPostAndRssItemDestinations() {
        XCTAssertEqual(NativeBookmarkRow.postPath(type: .story, id: "story-1", slug: nil), "/story/story-1")
        XCTAssertEqual(
            NativeBookmarkRow.postPath(type: .topicRecommendation, id: "rec-1", slug: "recommended"),
            "/topic-recommendations/rec-1"
        )
        XCTAssertEqual(NativeBookmarkRow.postPath(type: .dataPoint, id: "point-1", slug: nil), "/data-point/point-1")
        XCTAssertNil(NativeBookmarkRow.postPath(type: .comment, id: "comment-1", slug: nil))
        XCTAssertEqual(NativeBookmarkRow.rssItemPath(id: "item 1", mediaType: "article"), "/news?rss_item=item%201")
        XCTAssertEqual(
            NativeBookmarkRow.rssItemPath(id: "audio-1", mediaType: "audio"),
            "/podcast-episodes?rss_item=audio-1"
        )
        XCTAssertEqual(NativeBookmarkRow.rssItemPath(id: "video-1", mediaType: "video"), "/videos?rss_item=video-1")
        XCTAssertEqual(
            NativeBookmarkRow.rssItemPath(id: "item&filter=x+y#z", mediaType: "article"),
            "/news?rss_item=item%26filter%3Dx%2By%23z"
        )
    }

    func testTopicRecommendationDetailRouteIsNativeAndUsesEntityId() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/topic-recommendations/rec-1"))

        XCTAssertEqual(route.entry.destinationIdentifier, .topicRecommendations)
        XCTAssertEqual(route.match.param("id"), "rec-1")
    }

    func testRssFeedTopicUsesCanonicalSourcePath() throws {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        let entity = try decoder.decode(
            NativeGenericEntity.self,
            from: Data(#"{"id":"feed-1","slug":"daily-news","topic_type":"rss_feed"}"#.utf8)
        )
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .bookmarks), client: nil)

        XCTAssertEqual(viewModel.genericBookmarkPath(entity, entityType: "topic"), "/source/daily-news")
    }

    func testGenericTopicSubtypePathsAreCanonicalAndNative() throws {
        let cases = [
            (
                json: #"{"id":"rec-1","slug":"ignored","topic_type":"topic_recommendation"}"#,
                path: "/topic-recommendations/rec-1",
                destination: NativeRouteDestinationIdentifier.topicRecommendations
            ),
            (
                json: #"{"id":"instance-1","slug":"example.social","topic_type":"fediverse_instance"}"#,
                path: "/instance/example.social",
                destination: .topicDetail
            )
        ]
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .bookmarks), client: nil)

        for testCase in cases {
            let entity = try decoder.decode(NativeGenericEntity.self, from: Data(testCase.json.utf8))
            let path = viewModel.genericBookmarkPath(entity, entityType: "topic")

            XCTAssertEqual(path, testCase.path)
            XCTAssertEqual(
                NativeRouteCatalog.matchingRoute(for: path)?.entry.destinationIdentifier,
                testCase.destination
            )
        }
    }

    func testGenericBookmarkIconUsesHydratedEntityCollection() throws {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        let response = try decoder.decode(
            NativeGenericListResponse.self,
            from: Data(#"""
            {
              "results": [
                { "id": "topic-1" },
                { "id": "community-1" },
                { "id": "user-1" },
                { "id": "hostname-1" }
              ],
              "topics": { "topic-1": { "id": "topic-1" } },
              "communities": { "community-1": { "id": "community-1" } },
              "users": { "user-1": { "id": "user-1" } },
              "hostnames": { "hostname-1": { "id": "hostname-1" } }
            }
            """#.utf8)
        )
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .bookmarks), client: nil)
        let expectedIcons = [
            (id: "topic-1", icon: "tag"),
            (id: "community-1", icon: "person.3"),
            (id: "user-1", icon: "person"),
            (id: "hostname-1", icon: "globe")
        ]

        for expected in expectedIcons {
            let result = try XCTUnwrap(response.results.first { $0.id == expected.id })
            let hydratedEntity = response.hydratedEntity(for: result)

            XCTAssertEqual(
                viewModel.genericBookmarkIcon(hydratedEntity, response: response, fallback: "bookmark"),
                expected.icon,
                expected.id
            )
        }
    }

    func testRootSourceRoutesUseFollowingCollections() throws {
        let cases = [
            (path: "/my/news-sources", feedType: "article"),
            (path: "/my/podcasts", feedType: "podcast"),
            (path: "/my/channels", feedType: "video")
        ]

        for testCase in cases {
            let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: testCase.path))
            let viewModel = NativeRouteSurfaceViewModel(
                entry: route.entry,
                client: nil,
                routeMatch: route.match
            )
            let collection = try XCTUnwrap(viewModel.bookmarkCollection(for: "user-1"))

            XCTAssertEqual(collection.endpoint.path, "/api/v1/users/user-1/rss-feeds/following")
            XCTAssertEqual(collection.endpoint.queryItems, [.init(name: "feed_type", value: testCase.feedType)])
            XCTAssertEqual(collection.entityType, "rss_feed")
            XCTAssertEqual(
                collection.inverseAction,
                .init(
                    entityType: "rss_feed",
                    predicate: "follow",
                    label: .nativeSwiftHouseholdsBookmarksUnfollow
                )
            )
        }
    }

    func testDomainInverseActionPreservesNativeIdentityAndUsesBackendEntityType() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/my/domains/muted"))
        let viewModel = NativeRouteSurfaceViewModel(entry: route.entry, client: nil, routeMatch: route.match)
        let collection = try XCTUnwrap(viewModel.bookmarkCollection(for: "user-1"))

        XCTAssertEqual(collection.entityType, "hostname")
        XCTAssertEqual(collection.inverseAction?.entityType, "url_hostname")
    }

    func testDomainRemovalUsesBackendEntityType() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/bookmarks/url_hostname/domain-1/mute"] = (Data("{}".utf8), 200)
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .bookmarks), client: makeClient())
        let row = NativeBookmarkRow(
            entityType: "hostname",
            entityId: "domain-1",
            icon: "globe",
            title: .verbatim("example.com"),
            detail: .verbatim("Domain"),
            destination: .path("/domain/example.com"),
            inverseAction: .init(
                entityType: "url_hostname",
                predicate: "mute",
                label: .nativeSwiftHouseholdsBookmarksUnmute
            ),
            rank: 0
        )
        viewModel.bookmarkRows = [row]

        await viewModel.removeBookmarkRow(row)

        XCTAssertTrue(viewModel.bookmarkRows.isEmpty)
        XCTAssertEqual(
            CannedFeedURLProtocol.capturedURLs.map(\.path),
            ["/api/v1/bookmarks/url_hostname/domain-1/mute"]
        )
    }

    func testPassiveFollowersAndViewedRowsHaveNoInverseAction() throws {
        for path in ["/my/users/followers", "/my/topics/viewed", "/my/videos/viewed"] {
            let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: path))
            let viewModel = NativeRouteSurfaceViewModel(
                entry: route.entry,
                client: nil,
                routeMatch: route.match
            )
            XCTAssertNil(viewModel.bookmarkCollection(for: "user-1")?.inverseAction, path)
        }
    }

    func testOptimisticRemovalSendsDeleteAndIgnoresSecondSubmission() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/bookmarks/post/post-1/save"] = [
            (Data("{}".utf8), 200, 0.1)
        ]
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .bookmarks), client: makeClient())
        let row = makeRow(id: "post-1", rank: 0)
        viewModel.bookmarkRows = [row]

        async let first: Void = viewModel.removeBookmarkRow(row)
        await Task.yield()
        async let second: Void = viewModel.removeBookmarkRow(row)
        _ = await (first, second)

        XCTAssertTrue(viewModel.bookmarkRows.isEmpty)
        XCTAssertNil(viewModel.bookmarkMutationErrorMessage)
        XCTAssertEqual(CannedFeedURLProtocol.capturedMethods, ["DELETE"])
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.path), ["/api/v1/bookmarks/post/post-1/save"])
    }

    func testFailedRemovalDoesNotRollBackIntoAReplacementCollection() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/bookmarks/post/post-1/save"] = [
            (Data("{}".utf8), 500, 0.1)
        ]
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .bookmarks), client: makeClient())
        let removed = makeRow(id: "post-1", rank: 0)
        let replacement = makeRow(id: "post-2", rank: 0)
        viewModel.bookmarkRows = [removed]

        let removal = Task { await viewModel.removeBookmarkRow(removed) }
        await Task.yield()
        viewModel.bookmarkContextId = UUID()
        viewModel.bookmarkRows = [replacement]
        await removal.value

        XCTAssertEqual(viewModel.bookmarkRows, [replacement])
        XCTAssertNil(viewModel.bookmarkMutationErrorMessage)
    }

    func testFailedRemovalRestoresStableOrderAndPreservesOtherRows() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/bookmarks/post/post-2/save"] = (Data("{}".utf8), 500)
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .bookmarks), client: makeClient())
        let first = makeRow(id: "post-1", rank: 0)
        let removed = makeRow(id: "post-2", rank: 1)
        let last = makeRow(id: "post-3", rank: 2)
        viewModel.bookmarkRows = [first, removed, last]

        await viewModel.removeBookmarkRow(removed)

        XCTAssertEqual(viewModel.bookmarkRows.map(\.entityId), ["post-1", "post-2", "post-3"])
        XCTAssertEqual(
            viewModel.bookmarkMutationErrorMessage,
            "Couldn’t update this collection. The item was restored."
        )
    }

    func testFriendlyFallbackTitlesDoNotExposeRawPostIds() {
        XCTAssertEqual(NativeBookmarkRow.postTitle(type: .discussion, title: nil), "Untitled discussion")
        XCTAssertEqual(NativeBookmarkRow.postTitle(type: .comment, title: nil), "Untitled comment")
        XCTAssertNotEqual(NativeBookmarkRow.postTitle(type: .review, title: nil), "post-1")
    }

    func testCommentDestinationLazilyResolvesRootAndCachesPath() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/posts/comment-1"] = (postEnvelope(
            id: "comment-1",
            type: "comment",
            rootId: "root-1"
        ), 200)
        CannedFeedURLProtocol.handlers["/api/v1/posts/root-1"] = (postEnvelope(
            id: "root-1",
            type: "discussion",
            slug: "root-slug"
        ), 200)
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .bookmarks), client: makeClient())
        let row = commentRow()
        viewModel.bookmarkRows = [row]

        let first = await viewModel.bookmarkDestinationPath(for: row)
        let second = await viewModel.bookmarkDestinationPath(for: row)

        XCTAssertEqual(first, "/discussion/root-slug/comment/comment-1")
        XCTAssertEqual(second, first)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.path), [
            "/api/v1/posts/comment-1", "/api/v1/posts/root-1"
        ])
    }

    func testConcurrentCommentDestinationsCoalesceAndOnlyOwnerNavigates() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/posts/comment-1"] = [
            (postEnvelope(id: "comment-1", type: "comment", rootId: "root-1"), 200, 0.1)
        ]
        CannedFeedURLProtocol.handlers["/api/v1/posts/root-1"] = (postEnvelope(
            id: "root-1",
            type: "discussion",
            slug: "root-slug"
        ), 200)
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .bookmarks), client: makeClient())
        let row = commentRow()
        viewModel.bookmarkRows = [row]

        async let first = viewModel.bookmarkDestinationPath(for: row)
        await Task.yield()
        async let second = viewModel.bookmarkDestinationPath(for: row)
        let paths = await [first, second]
        let cached = await viewModel.bookmarkDestinationPath(for: row)

        XCTAssertEqual(paths.compactMap { $0 }, ["/discussion/root-slug/comment/comment-1"])
        XCTAssertEqual(cached, "/discussion/root-slug/comment/comment-1")
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.path), [
            "/api/v1/posts/comment-1", "/api/v1/posts/root-1"
        ])
    }

    func testCommentDestinationContextReplacementCancelsStaleResolutionAndFetchesFresh() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/posts/comment-1"] = [
            (postEnvelope(id: "comment-1", type: "comment", rootId: "stale-root"), 200, 0.2),
            (postEnvelope(id: "comment-1", type: "comment", rootId: "fresh-root"), 200, 0)
        ]
        CannedFeedURLProtocol.handlers["/api/v1/posts/fresh-root"] = (postEnvelope(
            id: "fresh-root",
            type: "discussion",
            slug: "fresh-slug"
        ), 200)
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .bookmarks), client: makeClient())
        let row = commentRow()
        viewModel.bookmarkRows = [row]

        let stale = Task { await viewModel.bookmarkDestinationPath(for: row) }
        try await waitForCapturedPath("/api/v1/posts/comment-1", count: 1)
        viewModel.bookmarkRows = []
        viewModel.resetBookmarkDestinationResolution()
        let freshRow = commentRow()
        viewModel.bookmarkRows = [freshRow]
        let stalePath = await stale.value
        let freshPath = await viewModel.bookmarkDestinationPath(for: freshRow)

        XCTAssertNil(stalePath)
        XCTAssertNil(viewModel.bookmarkMutationErrorMessage)
        XCTAssertEqual(freshPath, "/discussion/fresh-slug/comment/comment-1")
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.path), [
            "/api/v1/posts/comment-1", "/api/v1/posts/comment-1", "/api/v1/posts/fresh-root"
        ])
    }

    func testCommentDestinationFailureIsUserVisible() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/posts/comment-1"] = (Data("{}".utf8), 500)
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .bookmarks), client: makeClient())
        let row = commentRow()
        viewModel.bookmarkRows = [row]

        let path = await viewModel.bookmarkDestinationPath(for: row)

        XCTAssertNil(path)
        XCTAssertEqual(viewModel.bookmarkMutationErrorMessage, "Couldn’t open this comment. Try again.")
    }

    func testCommentResolutionCannotNavigateOrCacheAfterOptimisticRemoval() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/posts/comment-1"] = [
            (postEnvelope(id: "comment-1", type: "comment", rootId: "root-1"), 200, 0.2)
        ]
        CannedFeedURLProtocol.handlers["/api/v1/posts/root-1"] = (postEnvelope(
            id: "root-1",
            type: "discussion",
            slug: "root-slug"
        ), 200)
        CannedFeedURLProtocol.handlers["/api/v1/bookmarks/post/comment-1/save"] = (Data("{}".utf8), 200)
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .bookmarks), client: makeClient())
        let row = commentRow()
        viewModel.bookmarkRows = [row]

        let resolution = Task { await viewModel.bookmarkDestinationPath(for: row) }
        try await waitForCapturedPath("/api/v1/posts/comment-1", count: 1)
        await viewModel.removeBookmarkRow(row)
        let path = await resolution.value

        XCTAssertNil(path)
        XCTAssertTrue(viewModel.bookmarkRows.isEmpty)
        XCTAssertTrue(viewModel.bookmarkDestinationCache.isEmpty)
        XCTAssertTrue(viewModel.pendingBookmarkDestinations.isEmpty)
        XCTAssertNil(viewModel.bookmarkMutationErrorMessage)
    }

    func testStaleDirectRowAfterContextReplacementReturnsNil() async throws {
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .bookmarks), client: nil)
        let staleRow = directRow(id: "post-1", path: "/discussion/stale")
        viewModel.bookmarkRows = [staleRow]

        viewModel.resetBookmarkDestinationResolution()
        viewModel.bookmarkRows = [directRow(id: "post-2", path: "/discussion/current")]

        let destination = await viewModel.bookmarkDestinationPath(for: staleRow)
        XCTAssertNil(destination)
        XCTAssertTrue(viewModel.bookmarkDestinationCache.isEmpty)
    }

    func testCurrentDirectRowCanonicalizesCallerByIdentity() async throws {
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .bookmarks), client: nil)
        let callerRow = directRow(id: "post-1", path: "/discussion/stale")
        viewModel.bookmarkRows = [directRow(id: "post-1", path: "/discussion/current")]

        let destination = await viewModel.bookmarkDestinationPath(for: callerRow)
        XCTAssertEqual(destination, "/discussion/current")
    }

    private func makeRow(id: String, rank: Int) -> NativeBookmarkRow {
        directRow(id: id, path: "/discussion/\(id)", rank: rank)
    }

    private func directRow(id: String, path: String, rank: Int = 0) -> NativeBookmarkRow {
        NativeBookmarkRow(
            entityType: "post",
            entityId: id,
            icon: "doc.text",
            title: .verbatim("Post \(id)"),
            detail: .verbatim("Discussion"),
            destination: .path(path),
            inverseAction: .init(
                entityType: "post",
                predicate: "save",
                label: .nativeSwiftHouseholdsBookmarksUnsave
            ),
            rank: rank
        )
    }

    private func commentRow() -> NativeBookmarkRow {
        NativeBookmarkRow(
            entityType: "post",
            entityId: "comment-1",
            icon: "text.bubble",
            title: .verbatim("Comment"),
            detail: .verbatim("Comment"),
            destination: .comment(id: "comment-1", rootId: nil),
            inverseAction: .init(
                entityType: "post",
                predicate: "save",
                label: .nativeSwiftHouseholdsBookmarksUnsave
            ),
            rank: 0
        )
    }

    private func postEnvelope(id: String, type: String, rootId: String? = nil, slug: String? = nil) -> Data {
        let root = rootId.map { #""\#($0)""# } ?? "null"
        let slugValue = slug.map { #""\#($0)""# } ?? "null"
        return Data(#"""
        {"post":{
          "id":"\#(id)","slug":\#(slugValue),"post_type":"\#(type)","title":"Post","markdown":"","html":"",
          "root_id":\#(root),"created_by_id":"user-1","created_at":"2026-01-01T00:00:00Z",
          "broadcast":"everyone","privacy":"public","is_anonymous":false,"community_id":null,
          "clearance_status":"pending","deleted_at":null,"deleted_by_id":null,"locked_at":null,"locked_by_id":null,
          "archived_at":null,"archived_by_id":null,"clearance_reason":null,"clearance_updated_at":null,
          "spam_detection_created_at":null,"spam_detection_flagged":null,"spam_detection_results":null,
          "spam_detection_score":null,"updated_at":"2026-01-01T00:00:00Z","updated_by_id":null,
          "ai_summary_markdown":""
        }}
        """#.utf8)
    }

    private func waitForCapturedPath(_ path: String, count: Int) async throws {
        for _ in 0 ..< 250 {
            if CannedFeedURLProtocol.capturedPathCount(path) >= count {
                return
            }
            try await Task.sleep(nanoseconds: 10_000_000)
        }
        XCTFail("Timed out waiting for captured path \(path)")
    }
}
