import Foundation
@testable import VouchaFeatures
import XCTest

@MainActor
final class NativeTagManagementViewModelTagLimitTests: NativeRouteSurfaceViewModelTestCase {
    func testAddSelectedResultSetsTagLimitReachedOnPreconditionCode() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/posts/post-1"] = (postDetailData, 200)
        CannedFeedURLProtocol.queuedHandlers["/api/v1/entity-relations/post/post-1/category/topic"] = [
            (emptyRelationsData, 200, 0),
            (Data(#"{"code":"TAG_LIMIT_REACHED","message":"Tag limit reached"}"#.utf8), 403, 0)
        ]

        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/discussion/post-1/tags/topic"))
        let viewModel = try NativeTagManagementViewModel(
            client: makeClient(),
            routeMatch: route.match,
            subjectKind: .post,
            subjectId: "post-1",
            subjectTitle: "Native Post"
        )

        await viewModel.load()
        await viewModel.addSelectedResult(id: "topic-2")

        XCTAssertTrue(viewModel.tagLimitReached)
        if case .error = viewModel.state {
            XCTFail("Expected tagLimitReached branch, not a generic error state")
        }
    }

    func testActiveTabChangeResetsTagLimitReached() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/posts/post-1"] = (postDetailData, 200)
        CannedFeedURLProtocol.queuedHandlers["/api/v1/entity-relations/post/post-1/category/topic"] = [
            (emptyRelationsData, 200, 0),
            (Data(#"{"code":"TAG_LIMIT_REACHED","message":"Tag limit reached"}"#.utf8), 403, 0)
        ]

        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/discussion/post-1/tags/topic"))
        let viewModel = try NativeTagManagementViewModel(
            client: makeClient(),
            routeMatch: route.match,
            subjectKind: .post,
            subjectId: "post-1",
            subjectTitle: "Native Post"
        )

        await viewModel.load()
        await viewModel.addSelectedResult(id: "topic-2")
        XCTAssertTrue(viewModel.tagLimitReached)

        // Each tab has its own budget -- switching away from the capped "topic" tab must not keep
        // hiding the add-tag form on the unrelated "post" tab.
        viewModel.activeTab = "post"

        XCTAssertFalse(viewModel.tagLimitReached)
    }

    func testLoadResetsTagLimitReached() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/posts/post-1"] = (postDetailData, 200)
        CannedFeedURLProtocol.queuedHandlers["/api/v1/entity-relations/post/post-1/category/topic"] = [
            (emptyRelationsData, 200, 0),
            (Data(#"{"code":"TAG_LIMIT_REACHED","message":"Tag limit reached"}"#.utf8), 403, 0),
            (emptyRelationsData, 200, 0)
        ]

        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/discussion/post-1/tags/topic"))
        let viewModel = try NativeTagManagementViewModel(
            client: makeClient(),
            routeMatch: route.match,
            subjectKind: .post,
            subjectId: "post-1",
            subjectTitle: "Native Post"
        )

        await viewModel.load()
        await viewModel.addSelectedResult(id: "topic-2")
        XCTAssertTrue(viewModel.tagLimitReached)

        // A full reload (e.g. returning to this page after upgrading) must re-derive the flag from
        // a fresh mutation, not keep hiding the add-tag form behind a cap reached on a prior visit.
        await viewModel.load()

        XCTAssertFalse(viewModel.tagLimitReached)
    }

    func testAddSelectedResultSurfacesGenericErrorWhenNotTagLimitReached() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/posts/post-1"] = (postDetailData, 200)
        CannedFeedURLProtocol.queuedHandlers["/api/v1/entity-relations/post/post-1/category/topic"] = [
            (emptyRelationsData, 200, 0),
            (Data("{}".utf8), 500, 0)
        ]

        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/discussion/post-1/tags/topic"))
        let viewModel = try NativeTagManagementViewModel(
            client: makeClient(),
            routeMatch: route.match,
            subjectKind: .post,
            subjectId: "post-1",
            subjectTitle: "Native Post"
        )

        await viewModel.load()
        await viewModel.addSelectedResult(id: "topic-2")

        XCTAssertFalse(viewModel.tagLimitReached)
        if case .error = viewModel.state {
        } else {
            XCTFail("Expected error state for a non-tag-limit VouchaError")
        }
    }

    private var postDetailData: Data {
        Data("""
        {
          "post": {
            "id": "post-1",
            "slug": "native-post",
            "post_type": "discussion",
            "title": "Native Post",
            "markdown": "Body",
            "created_by_id": "user-1",
            "created_at": "2026-01-01T00:00:00Z",
            "privacy": "public",
            "is_anonymous": false
          }
        }
        """.utf8)
    }

    private var emptyRelationsData: Data {
        Data("""
        {
          "results": [],
          "page_info": { "has_next_page": false, "end_cursor": null, "start_cursor": null },
          "entity_relations": {}
        }
        """.utf8)
    }
}
