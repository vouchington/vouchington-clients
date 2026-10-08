import Foundation
import VouchaAPI
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class CommunityAutomodWorkspaceTests: NativeRouteSurfaceViewModelTestCase {
    private let queuePath = "/api/v1/communities/test-community/moderation-queue"

    func testFlagPagesAppendAndForwardTheServerCursor() async throws {
        let first = fixture("page-1")
        CannedFeedURLProtocol.handlers[queuePath] = (first, 200)
        let model = try makeModel()
        await model.loadNextPage()
        let firstEntry = try XCTUnwrap(model.pagination.items.first)
        let cursor = try XCTUnwrap(model.pagination.endCursor)
        XCTAssertNil(firstEntry.postId)
        XCTAssertEqual(firstEntry.automodFlagPostId, firstEntry.entityId)
        CannedFeedURLProtocol.handlers[queuePath] = (fixture("page-2"), 200)
        await model.loadNextPage()
        XCTAssertEqual(model.pagination.items.map(\.targetLabel), ["Flagged post", "Next flagged post"])
        let url = try XCTUnwrap(CannedFeedURLProtocol.capturedURLs.last)
        let query = try XCTUnwrap(URLComponents(url: url, resolvingAgainstBaseURL: false)?.queryItems)
        XCTAssertEqual(query.first { $0.name == "source" }?.value, "automod_flag")
        XCTAssertEqual(query.first { $0.name == "after" }?.value, cursor)
        XCTAssertFalse(model.pagination.hasMore)
    }

    func testMemberResponseClearsModeratorRowsAndStopsPagination() async throws {
        CannedFeedURLProtocol.handlers[queuePath] = (fixture("page-1"), 200)
        let model = try makeModel()
        await model.loadNextPage()
        XCTAssertEqual(model.pagination.items.count, 1)
        CannedFeedURLProtocol.handlers[queuePath] = (fixture("member"), 200)
        await model.refresh()
        XCTAssertFalse(model.canModerate)
        XCTAssertTrue(model.pagination.items.isEmpty)
        let count = CannedFeedURLProtocol.capturedURLs.count
        await model.loadNextPage()
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.count, count)
    }

    func testDismissalStaysPendingAndRetainsRowOnFailure() async throws {
        CannedFeedURLProtocol.handlers[queuePath] = (fixture("page-1"), 200)
        let model = try makeModel()
        await model.loadNextPage()
        let entry = try XCTUnwrap(model.pagination.items.first)
        let postId = try XCTUnwrap(entry.automodFlagPostId)
        let path = "/api/v1/communities/test-community/posts/\(postId)/automod-flag/dismissal"
        CannedFeedURLProtocol.handlers[path] = (Data(#"{"error":"unavailable"}"#.utf8), 500)
        CannedFeedURLProtocol.suspendResponse(path: path)
        let barrier = CannedFeedURLProtocol.requestBarrier(path: path, method: "POST")
        let dismissal = Task { await model.dismiss(entry) }
        defer {
            CannedFeedURLProtocol.releaseResponse(path: path)
            dismissal.cancel()
        }
        _ = try await barrier.wait()
        XCTAssertTrue(model.dismissingPostIds.contains(postId))
        XCTAssertEqual(model.pagination.items.map(\.id), [entry.id])
        await model.dismiss(entry)
        XCTAssertEqual(CannedFeedURLProtocol.capturedRequests.filter { $0.url.path == path }.count, 1)
        CannedFeedURLProtocol.releaseResponse(path: path)
        await dismissal.value
        XCTAssertFalse(model.dismissingPostIds.contains(postId))
        XCTAssertEqual(model.pagination.items.map(\.id), [entry.id])
        XCTAssertNotNil(model.mutationError)
        XCTAssertNil(model.notice)
    }

    func testSuccessfulDismissalRemovesRowAndRefreshesFlags() async throws {
        CannedFeedURLProtocol.handlers[queuePath] = (fixture("page-1"), 200)
        let model = try makeModel()
        await model.loadNextPage()
        let entry = try XCTUnwrap(model.pagination.items.first)
        let postId = try XCTUnwrap(entry.automodFlagPostId)
        let path = "/api/v1/communities/test-community/posts/\(postId)/automod-flag/dismissal"
        CannedFeedURLProtocol.handlers[path] = (Data(), 204)
        CannedFeedURLProtocol.handlers[queuePath] = (fixture("page-2"), 200)
        await model.dismiss(entry)
        XCTAssertEqual(model.pagination.items.map(\.targetLabel), ["Next flagged post"])
        XCTAssertNil(model.mutationError)
        XCTAssertNotNil(model.notice)
        let request = try XCTUnwrap(CannedFeedURLProtocol.capturedRequests.first { $0.url.path == path })
        XCTAssertEqual(request.method, "POST")
        XCTAssertNil(request.body)
    }

    func testAlreadyDismissedFlagRefreshesWithoutSuccessNotice() async throws {
        CannedFeedURLProtocol.handlers[queuePath] = (fixture("page-1"), 200)
        let model = try makeModel()
        await model.loadNextPage()
        let entry = try XCTUnwrap(model.pagination.items.first)
        let postId = try XCTUnwrap(entry.automodFlagPostId)
        let path = "/api/v1/communities/test-community/posts/\(postId)/automod-flag/dismissal"
        CannedFeedURLProtocol.handlers[path] = (Data(#"{"error":"gone"}"#.utf8), 404)
        CannedFeedURLProtocol.handlers[queuePath] = (fixture("page-2"), 200)
        await model.dismiss(entry)
        XCTAssertEqual(model.pagination.items.map(\.targetLabel), ["Next flagged post"])
        XCTAssertNil(model.mutationError)
        XCTAssertNil(model.notice)
    }

    func testUnknownSourceIsFilteredWithoutDroppingTheContinuationCursor() async throws {
        var payload = try XCTUnwrap(JSONSerialization.jsonObject(with: fixture("page-1")) as? [String: Any])
        var entries = try XCTUnwrap(payload["entries"] as? [[String: Any]])
        entries[0]["queue_source"] = "future_review_source"
        payload["entries"] = entries
        CannedFeedURLProtocol.handlers[queuePath] = try (JSONSerialization.data(withJSONObject: payload), 200)
        let model = try makeModel()
        await model.loadNextPage()
        XCTAssertTrue(model.pagination.items.isEmpty)
        XCTAssertTrue(model.pagination.hasMore)
        let cursor = try XCTUnwrap(model.pagination.endCursor)
        CannedFeedURLProtocol.handlers[queuePath] = (fixture("page-2"), 200)
        await model.loadNextPage()
        XCTAssertEqual(model.pagination.items.map(\.targetLabel), ["Next flagged post"])
        let url = try XCTUnwrap(CannedFeedURLProtocol.capturedURLs.last)
        let query = try XCTUnwrap(URLComponents(url: url, resolvingAgainstBaseURL: false)?.queryItems)
        XCTAssertEqual(query.first { $0.name == "after" }?.value, cursor)
    }

    func testKnownMemberDoesNotRequestModeratorQueue() async throws {
        let model = try CommunityAutomodWorkspaceViewModel(
            client: makeClient(), slug: "test-community", action: .reviewQueue, canModerate: false
        )
        await model.loadNextPage()
        await model.saveAction()
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.isEmpty)
        XCTAssertTrue(model.pagination.items.isEmpty)
    }

    private func makeModel() throws -> CommunityAutomodWorkspaceViewModel {
        try CommunityAutomodWorkspaceViewModel(
            client: makeClient(), slug: "test-community", action: .reviewQueue, canModerate: true
        )
    }

    private func fixture(_ suffix: String) -> Data {
        ApiFixtureLoader.data("native.communities.moderation-queue.automod-flag.\(suffix)")
    }
}
