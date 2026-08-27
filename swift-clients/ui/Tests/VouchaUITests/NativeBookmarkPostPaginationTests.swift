import ViewInspector
@testable import VouchaAPI
@testable import VouchaCore
@testable import VouchaFeatures
import XCTest

@MainActor
final class NativeBookmarkPostPaginationTests: NativeRouteSurfaceViewModelTestCase {
    func testLoadMoreForwardsCursorAndAppendsUniqueRowsInRankOrder() async throws {
        let path = "/api/v1/users/user-1/posts/saved"
        seedIdentity()
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (postPage(ids: ["post-1", "post-2"], cursor: "cursor-1", hasNextPage: true), 200, 0),
            (postPage(ids: ["post-2", "post-3"], cursor: nil, hasNextPage: false), 200, 0)
        ]
        let viewModel = try await loadedSavedPostsViewModel()

        await viewModel.loadMoreBookmarkRows()

        XCTAssertEqual(viewModel.bookmarkRows.map(\.entityId), ["post-1", "post-2", "post-3"])
        XCTAssertEqual(viewModel.bookmarkRows.map(\.rank), [0, 1, 2])
        let continuation = try XCTUnwrap(CannedFeedURLProtocol.capturedURLs.last)
        XCTAssertEqual(queryValue("after", in: continuation), "cursor-1")
        XCTAssertEqual(queryValue("limit", in: continuation), "25")
        XCTAssertFalse(viewModel.bookmarkPagination.hasMore)
    }

    func testLoadMoreAllowsOnlyOneRequestInFlight() async throws {
        let path = "/api/v1/users/user-1/posts/saved"
        seedIdentity()
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (postPage(ids: ["post-1"], cursor: "cursor-1", hasNextPage: true), 200, 0),
            (postPage(ids: ["post-2"], cursor: nil, hasNextPage: false), 200, 0)
        ]
        let viewModel = try await loadedSavedPostsViewModel()
        CannedFeedURLProtocol.suspendResponse(path: path)

        async let first: Void = viewModel.loadMoreBookmarkRows()
        await waitForSuspendedResponse(path)
        async let second: Void = viewModel.loadMoreBookmarkRows()
        await Task.yield()
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(path), 2)
        CannedFeedURLProtocol.releaseResponse(path: path)
        _ = await (first, second)

        XCTAssertEqual(viewModel.bookmarkRows.map(\.entityId), ["post-1", "post-2"])
    }

    func testLoadMoreAllocatesRankAfterSurvivingMaximumWhenEarlierRowWasRemoved() async throws {
        let path = "/api/v1/users/user-1/posts/saved"
        seedIdentity()
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (postPage(ids: ["post-1", "post-2"], cursor: "cursor-1", hasNextPage: true), 200, 0),
            (postPage(ids: ["post-3"], cursor: nil, hasNextPage: false), 200, 0)
        ]
        let viewModel = try await loadedSavedPostsViewModel()

        viewModel.bookmarkRows.removeAll { $0.entityId == "post-1" }
        viewModel.bookmarkPagination.remove { $0.entityId == "post-1" }
        await viewModel.loadMoreBookmarkRows()

        XCTAssertEqual(viewModel.bookmarkRows.map(\.entityId), ["post-2", "post-3"])
        XCTAssertEqual(viewModel.bookmarkRows.map(\.rank), [1, 2])
    }

    func testLoadMoreWaitsForFailedHighestRankRemovalRollbackBeforeAppending() async throws {
        let pagePath = "/api/v1/users/user-1/posts/saved"
        let removalPath = "/api/v1/bookmarks/post/post-2/save"
        seedIdentity()
        CannedFeedURLProtocol.queuedHandlers[pagePath] = [
            (postPage(ids: ["post-1", "post-2"], cursor: "cursor-1", hasNextPage: true), 200, 0),
            (postPage(ids: ["post-3"], cursor: nil, hasNextPage: false), 200, 0)
        ]
        CannedFeedURLProtocol.handlers[removalPath] = (Data("{}".utf8), 500)
        CannedFeedURLProtocol.suspendResponse(path: removalPath)
        let viewModel = try await loadedSavedPostsViewModel()
        let highestRankedRow = try XCTUnwrap(viewModel.bookmarkRows.last)

        let removal = Task { await viewModel.removeBookmarkRow(highestRankedRow) }
        await waitForSuspendedResponse(removalPath)
        await viewModel.loadMoreBookmarkRows()

        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(pagePath), 1)
        let surface = NativeBookmarkRowsSurface(viewModel: viewModel) { _ in }
        let control = try surface.inspect().find(viewWithAccessibilityIdentifier: "bookmark-pagination-control")
        XCTAssertTrue(try control.find(button: "Load more").isDisabled())

        CannedFeedURLProtocol.releaseResponse(path: removalPath)
        await removal.value
        XCTAssertEqual(viewModel.bookmarkRows.map(\.entityId), ["post-1", "post-2"])
        XCTAssertEqual(viewModel.bookmarkRows.map(\.rank), [0, 1])

        await viewModel.loadMoreBookmarkRows()

        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(pagePath), 2)
        XCTAssertEqual(viewModel.bookmarkRows.map(\.entityId), ["post-1", "post-2", "post-3"])
        XCTAssertEqual(viewModel.bookmarkRows.map(\.rank), [0, 1, 2])
    }

    func testLoadMoreFailurePreservesRowsCursorAndSupportsSameCursorRetry() async throws {
        let path = "/api/v1/users/user-1/posts/saved"
        seedIdentity()
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (postPage(ids: ["post-1"], cursor: "cursor-1", hasNextPage: true), 200, 0),
            (Data("{}".utf8), 500, 0),
            (postPage(ids: ["post-2"], cursor: nil, hasNextPage: false), 200, 0)
        ]
        let viewModel = try await loadedSavedPostsViewModel()

        await viewModel.loadMoreBookmarkRows()
        XCTAssertEqual(viewModel.bookmarkRows.map(\.entityId), ["post-1"])
        XCTAssertEqual(viewModel.bookmarkPagination.endCursor, "cursor-1")
        XCTAssertNotNil(viewModel.bookmarkPagination.lastError)

        await viewModel.loadMoreBookmarkRows()
        let continuationURLs = CannedFeedURLProtocol.capturedURLs.filter { $0.path == path }.dropFirst()
        XCTAssertEqual(continuationURLs.map { queryValue("after", in: $0) }, ["cursor-1", "cursor-1"])
        XCTAssertEqual(viewModel.bookmarkRows.map(\.entityId), ["post-1", "post-2"])
        XCTAssertNil(viewModel.bookmarkPagination.lastError)
    }

    func testLoadMoreRejectsResponseFromPriorABABookmarkContext() async throws {
        let path = "/api/v1/users/user-1/posts/saved"
        seedIdentity()
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (postPage(ids: ["post-1"], cursor: "cursor-1", hasNextPage: true), 200, 0),
            (postPage(ids: ["stale-post"], cursor: nil, hasNextPage: false), 200, 0)
        ]
        let viewModel = try await loadedSavedPostsViewModel()
        let originalCollection = try XCTUnwrap(viewModel.currentBookmarkCollection)
        CannedFeedURLProtocol.suspendResponse(path: path)

        let request = Task { await viewModel.loadMoreBookmarkRows() }
        await waitForSuspendedResponse(path)
        viewModel.currentBookmarkCollection = nil
        viewModel.bookmarkPagination.reset()
        viewModel.currentBookmarkCollection = originalCollection
        let replacement = makeReplacementRow()
        viewModel.bookmarkRows = [replacement]
        viewModel.bookmarkPagination.reset(items: [replacement])
        CannedFeedURLProtocol.releaseResponse(path: path)
        await request.value

        XCTAssertEqual(viewModel.bookmarkRows.map(\.entityId), ["replacement-post"])
        XCTAssertEqual(viewModel.bookmarkPagination.items.map(\.entityId), ["replacement-post"])
        XCTAssertNil(viewModel.bookmarkPagination.endCursor)
    }

    func testBookmarkRowsSurfaceShowsLoadMoreAndRetryControls() throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/my/posts/saved"))
        let viewModel = NativeRouteSurfaceViewModel(entry: route.entry, client: nil, routeMatch: route.match)
        viewModel.currentBookmarkCollection = try XCTUnwrap(viewModel.bookmarkCollection(for: "user-1"))
        viewModel.bookmarkRows = [makeReplacementRow()]
        viewModel.bookmarkPagination.restoreContinuation(endCursor: "cursor-1", hasMore: true)
        viewModel.state = .loaded

        var surface = NativeBookmarkRowsSurface(viewModel: viewModel) { _ in }
        var control = try surface.inspect().find(viewWithAccessibilityIdentifier: "bookmark-pagination-control")
        XCTAssertNoThrow(try control.find(button: "Load more"))

        try setPaginationError(on: viewModel)
        surface = NativeBookmarkRowsSurface(viewModel: viewModel) { _ in }
        control = try surface.inspect().find(viewWithAccessibilityIdentifier: "bookmark-pagination-control")
        XCTAssertNoThrow(try control.find(button: "Try Again"))
    }

    func testEmptyBookmarkRowsSurfaceKeepsLoadMoreControlForResumablePage() throws {
        let viewModel = try emptyPaginatedBookmarkViewModel()
        let surface = NativeBookmarkRowsSurface(viewModel: viewModel) { _ in }

        let control = try surface.inspect().find(viewWithAccessibilityIdentifier: "bookmark-pagination-control")
        XCTAssertNoThrow(try control.find(button: "Load more"))
        XCTAssertThrowsError(try surface.inspect().find(text: "No bookmarks"))
    }

    func testEmptyBookmarkRowsSurfaceKeepsRetryControlAfterContinuationFailure() throws {
        let viewModel = try emptyPaginatedBookmarkViewModel()
        try setPaginationError(on: viewModel)
        let surface = NativeBookmarkRowsSurface(viewModel: viewModel) { _ in }

        let control = try surface.inspect().find(viewWithAccessibilityIdentifier: "bookmark-pagination-control")
        XCTAssertNoThrow(try control.find(button: "Try Again"))
        XCTAssertThrowsError(try surface.inspect().find(text: "No bookmarks"))
    }

    private func loadedSavedPostsViewModel() async throws -> NativeRouteSurfaceViewModel {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/my/posts/saved"))
        let viewModel = try NativeRouteSurfaceViewModel(
            entry: route.entry,
            client: makeClient(),
            routeMatch: route.match
        )
        await viewModel.load()
        guard case .loaded = viewModel.state else {
            XCTFail("Expected saved-post collection to load")
            return viewModel
        }
        return viewModel
    }

    private func emptyPaginatedBookmarkViewModel() throws -> NativeRouteSurfaceViewModel {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/my/posts/saved"))
        let viewModel = NativeRouteSurfaceViewModel(entry: route.entry, client: nil, routeMatch: route.match)
        viewModel.currentBookmarkCollection = try XCTUnwrap(viewModel.bookmarkCollection(for: "user-1"))
        viewModel.bookmarkPagination.restoreContinuation(endCursor: "cursor-1", hasMore: true)
        viewModel.state = .loaded
        return viewModel
    }

    private func setPaginationError(on viewModel: NativeRouteSurfaceViewModel) throws {
        let request = try XCTUnwrap(viewModel.bookmarkPagination.beginNextPage())
        viewModel.bookmarkPagination.fail(
            request,
            error: .api(statusCode: 503, preconditionCode: nil)
        )
    }

    private func seedIdentity() {
        CannedFeedURLProtocol.handlers["/api/v1/my/identity"] = (
            PrivateUserTestFixture.identityEnvelope(id: "user-1", username: "test-user"),
            200
        )
    }

    private func waitForSuspendedResponse(_ path: String) async {
        for _ in 0 ..< 100 where !CannedFeedURLProtocol.hasSuspendedResponse(path: path) {
            await Task.yield()
        }
        XCTAssertTrue(CannedFeedURLProtocol.hasSuspendedResponse(path: path))
    }

    private func queryValue(_ name: String, in url: URL) -> String? {
        URLComponents(url: url, resolvingAgainstBaseURL: false)?.queryItems?.first { $0.name == name }?.value
    }

    private func postPage(ids: [String], cursor: String?, hasNextPage: Bool) -> Data {
        let results = ids.map { id in
            """
            {"id":"\(id)","slug":"\(id)","post_type":"discussion","title":"\(id)",\
            "created_by_id":"user-2"}
            """
        }.joined(separator: ",")
        let encodedCursor = cursor.map { "\"\($0)\"" } ?? "null"
        return Data("""
        {"results":[\(results)],"page_info":{"has_next_page":\(hasNextPage),\
        "end_cursor":\(encodedCursor),"start_cursor":null}}
        """.utf8)
    }

    private func makeReplacementRow() -> NativeBookmarkRow {
        NativeBookmarkRow(
            entityType: "post",
            entityId: "replacement-post",
            icon: "doc.text",
            title: .userContent("Replacement"),
            detail: .userContent("Replacement detail"),
            destination: .path("/discussion/replacement-post"),
            inverseAction: nil,
            rank: 0
        )
    }
}
