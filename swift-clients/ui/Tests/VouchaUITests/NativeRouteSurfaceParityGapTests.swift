import Foundation
@testable import VouchaFeatures
import XCTest

@MainActor
final class NativeRouteSurfaceParityGapTests: NativeRouteSurfaceViewModelTestCase {
    func testRetryAfterErrorReloadsRemoteSurface() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/feeds/posts/any"] = (Data("{}".utf8), 500)
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .feedPosts), client: makeClient())

        await viewModel.load()
        CannedFeedURLProtocol.handlers["/api/v1/feeds/posts/any"] = (postFeedData, 200)
        await viewModel.load()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.path), [
            "/api/v1/feeds/posts/any",
            "/api/v1/feeds/posts/any"
        ])
        XCTAssertEqual(viewModel.rows.first?.title, "Native post")
    }

    func testPublicLandingPageLoadsPublicUserEndpoint() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/users/alice/landing-pages/bonus"] = (landingPageData, 200)
        let match = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/landing/alice/bonus")?.match)
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .landingPages),
            client: makeClient(),
            routeMatch: match
        )

        await viewModel.load()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.path, "/api/v1/users/alice/landing-pages/bonus")
        XCTAssertEqual(viewModel.rows.first, verbatimRow(icon: "doc.text", title: "Bonus", detail: "bonus · Public"))
    }

    func testTopicBrowseHydratesTopicsSidecar() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/topics"] = (
            Data(
                #"{"results":[{"id":"topic-1"}],"topics":{"topic-1":{"id":"topic-1","name":"Swift","slug":"swift","description":"Native topic"}}}"#
                    .utf8
            ),
            200
        )
        let viewModel = try NativeRouteSurfaceViewModel(entry: entry(for: .topicsBrowse), client: makeClient())

        await viewModel.load()

        XCTAssertEqual(viewModel.rows.first, verbatimRow(icon: "tag", title: "Swift", detail: "Native topic"))
    }

    func testModerationCollectionRoutesUseDedicatedMyEndpoints() async throws {
        try await assertModerationCollection(
            path: "/my/warnings",
            endpointPath: "/api/v1/my/warnings",
            title: "Warnings"
        )
        try await assertModerationCollection(path: "/my/bans", endpointPath: "/api/v1/my/bans", title: "Bans")
        try await assertModerationCollection(
            path: "/my/removed-posts",
            endpointPath: "/api/v1/my/removed-posts",
            title: "Removed posts"
        )
    }

    private func assertModerationCollection(path: String, endpointPath: String, title: String) async throws {
        CannedFeedURLProtocol.handlers = [endpointPath: (Data(#"{"results":[{"id":"item-1"}]}"#.utf8), 200)]
        CannedFeedURLProtocol.capturedURLs = []
        let match = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: path)?.match)
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: entry(for: .moderationCases),
            client: makeClient(),
            routeMatch: match
        )

        await viewModel.load()

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.path, endpointPath)
        XCTAssertEqual(viewModel.rows.first?.title, title)
        XCTAssertEqual(viewModel.rows.first?.detail, "1 item")
    }

    private var postFeedData: Data {
        Data("""
        {
          "results": [{ "id": "result-1", "entity_id": "post-1", "post_type": "discussion" }],
          "posts": {
            "post-1": {
              "id": "post-1",
              "slug": "native-post",
              "post_type": "discussion",
              "title": "Native post",
              "markdown": "Body",
              "created_by_id": "user-1",
              "created_at": "2026-01-01T00:00:00Z"
            }
          }
        }
        """.utf8)
    }

    private var landingPageData: Data {
        Data(
            #"{"landing_page":{"id":"page-1","title":"Bonus","subtitle":"Public","slug":"bonus","is_default":false}}"#
                .utf8
        )
    }

    private var conversationMessagesData: Data {
        Data("""
        {
          "results": [
            {
              "id": "message-1",
              "conversation_id": "conversation-1",
              "content": {
                "role": "alice",
                "content": "Hello native"
              },
              "created_at": "2026-01-01T00:00:00Z",
              "updated_at": "2026-01-01T00:00:00Z",
              "deleted_at": null
            }
          ],
          "page_info": { "has_next_page": false, "end_cursor": null, "start_cursor": null }
        }
        """.utf8)
    }
}
