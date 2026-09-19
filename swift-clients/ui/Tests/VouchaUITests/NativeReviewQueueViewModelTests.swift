import Foundation
@testable import VouchaFeatures
import VouchaLocalization
import VouchaModels
import XCTest

@MainActor
final class NativeReviewQueueViewModelTests: NativeRouteSurfaceViewModelTestCase {
    private let path = "/api/v1/posts/review-queue"

    func testLoadsFirstPageAndPreservesTypedFields() async throws {
        CannedFeedURLProtocol.handlers[path] = (queueData(posts: [post(id: "rejected"), post(
            id: "review",
            status: "in_review",
            author: nil,
            rootId: "root-1"
        )], hasNextPage: true, endCursor: "review"), 200)
        let viewModel = try NativeReviewQueueViewModel(client: makeClient(), isSignedIn: true, isAdministrator: true)

        await viewModel.load()

        XCTAssertEqual(viewModel.state, .loaded)
        XCTAssertEqual(viewModel.items.map(\.id), ["rejected", "review"])
        XCTAssertEqual(viewModel.items[1].post.rootId, "root-1")
        XCTAssertEqual(viewModel.items[1].clearanceStatus, .inReview)
        XCTAssertTrue(viewModel.hasNextPage)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.first?.query, "limit=25")
    }

    func testSignedOutAndMemberStatesDoNotRequest() async throws {
        let client = try makeClient()
        let signedOut = NativeReviewQueueViewModel(client: client, isSignedIn: false, isAdministrator: false)
        let member = NativeReviewQueueViewModel(client: client, isSignedIn: true, isAdministrator: false)
        let missingClient = NativeReviewQueueViewModel(client: nil, isSignedIn: true, isAdministrator: true)
        let administrator = NativeReviewQueueViewModel(client: client, isSignedIn: true, isAdministrator: true)
        for viewModel in [signedOut, member, missingClient, administrator] {
            viewModel.hasNextPage = true
            viewModel.endCursor = "opaque-cursor"
        }

        await signedOut.load()
        await member.load()

        XCTAssertEqual(signedOut.state, .signInRequired)
        XCTAssertEqual(member.state, .administratorRequired)
        XCTAssertFalse(signedOut.canLoadMore)
        XCTAssertFalse(member.canLoadMore)
        XCTAssertFalse(missingClient.canLoadMore)
        XCTAssertTrue(administrator.canLoadMore)
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.isEmpty)
    }

    func testPaginationForwardsCursorDeduplicatesAndGuardsDuplicateLoads() async throws {
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (queueData(posts: [post(id: "first")], hasNextPage: true, endCursor: "first"), 200, 0),
            (queueData(posts: [post(id: "first"), post(id: "second"), post(id: "second")]), 200, 0.15)
        ]
        let viewModel = try NativeReviewQueueViewModel(client: makeClient(), isSignedIn: true, isAdministrator: true)
        await viewModel.load()

        async let first: Void = viewModel.loadMore()
        async let duplicate: Void = viewModel.loadMore()
        _ = await (first, duplicate)

        XCTAssertEqual(viewModel.items.map(\.id), ["first", "second"])
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(path), 2)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.last?.query, "limit=25&after=first")
    }

    func testRefreshAndLoadMoreFailuresPreserveRows() async throws {
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (queueData(posts: [post(id: "first")], hasNextPage: true, endCursor: "first"), 200, 0),
            (Data("{}".utf8), 500, 0),
            (Data("{}".utf8), 500, 0)
        ]
        let viewModel = try NativeReviewQueueViewModel(client: makeClient(), isSignedIn: true, isAdministrator: true)
        await viewModel.load()
        await viewModel.refresh()
        XCTAssertEqual(viewModel.items.map(\.id), ["first"])
        XCTAssertEqual(viewModel.state, .loaded)
        XCTAssertNotNil(viewModel.errorMessage)

        await viewModel.loadMore()
        XCTAssertEqual(viewModel.items.map(\.id), ["first"])
        XCTAssertNotNil(viewModel.errorMessage)
    }

    func testSuccessfulRefreshReplacesRowsAndPageState() async throws {
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (queueData(posts: [post(id: "old")], hasNextPage: true, endCursor: "old"), 200, 0),
            (queueData(posts: [post(id: "new")], hasNextPage: false, endCursor: nil), 200, 0)
        ]
        let viewModel = try NativeReviewQueueViewModel(
            client: makeClient(),
            isSignedIn: true,
            isAdministrator: true
        )
        await viewModel.load()

        await viewModel.refresh()

        XCTAssertEqual(viewModel.items.map(\.id), ["new"])
        XCTAssertFalse(viewModel.hasNextPage)
        XCTAssertNil(viewModel.endCursor)
        XCTAssertEqual(viewModel.state, .loaded)
    }

    func testStaleAndCancelledListResultsAreIgnored() async throws {
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (queueData(posts: [post(id: "stale")]), 200, 0),
            (queueData(posts: [post(id: "fresh")]), 200, 0)
        ]
        let staleRequestStarted = expectation(description: "stale review-queue request started")
        CannedFeedURLProtocol.suspendResponse(path: path) { staleRequestStarted.fulfill() }
        let viewModel = try NativeReviewQueueViewModel(client: makeClient(), isSignedIn: true, isAdministrator: true)
        let staleTask = Task { await viewModel.load() }
        await fulfillment(of: [staleRequestStarted], timeout: 1)
        viewModel.cancelListOperations()
        CannedFeedURLProtocol.releaseResponse(path: path)
        await staleTask.value
        XCTAssertTrue(viewModel.items.isEmpty)
        XCTAssertEqual(viewModel.state, .idle)

        await viewModel.load()
        XCTAssertEqual(viewModel.items.map(\.id), ["fresh"])

        CannedFeedURLProtocol.queuedHandlers[path] = [(queueData(posts: [post(id: "cancelled")]), 200, 0)]
        let cancelledRequestStarted = expectation(description: "cancelled review-queue request started")
        CannedFeedURLProtocol.suspendResponse(path: path) { cancelledRequestStarted.fulfill() }
        let cancelledTask = Task { await viewModel.refresh() }
        await fulfillment(of: [cancelledRequestStarted], timeout: 1)
        cancelledTask.cancel()
        CannedFeedURLProtocol.releaseResponse(path: path)
        await cancelledTask.value
        XCTAssertEqual(viewModel.items.map(\.id), ["fresh"])
        XCTAssertFalse(viewModel.isListLoading)
    }

    func testFiltersUnsupportedStatuses() async throws {
        CannedFeedURLProtocol.handlers[path] = (queueData(posts: [
            post(id: "rejected"), post(id: "review", status: "in_review"),
            post(id: "approved", status: "approved"), post(id: "pending", status: "pending")
        ]), 200)
        let viewModel = try NativeReviewQueueViewModel(client: makeClient(), isSignedIn: true, isAdministrator: true)

        await viewModel.load()

        XCTAssertEqual(viewModel.items.map(\.id), ["rejected", "review"])
    }

    func testClearanceActionsApplyServerConfirmedStatus() async throws {
        CannedFeedURLProtocol.handlers[path] = (queueData(posts: [post(id: "one"), post(id: "two")]), 200)
        let viewModel = try NativeReviewQueueViewModel(client: makeClient(), isSignedIn: true, isAdministrator: true)
        await viewModel.load()
        CannedFeedURLProtocol.handlers["/api/v1/posts/one/clearances"] = (clearanceData("in_review"), 200)
        CannedFeedURLProtocol.handlers["/api/v1/posts/two/clearances"] = (clearanceData("approved"), 200)

        await viewModel.perform(.reReview, postId: "one")
        await viewModel.perform(.approve, postId: "two")

        XCTAssertEqual(viewModel.items.count, 1)
        XCTAssertEqual(viewModel.items[0].clearanceStatus, .inReview)
        let requestBodies = try CannedFeedURLProtocol.capturedBodies.compactMap(\.self).map {
            try XCTUnwrap(JSONSerialization.jsonObject(with: Data($0.utf8)) as? [String: String])
        }
        XCTAssertTrue(requestBodies.contains([
            "reason_code": "staff_reviewed",
            "status": "in_review"
        ]))
        XCTAssertTrue(requestBodies.contains([
            "reason_code": "staff_approved",
            "status": "approved"
        ]))
    }

    func testApprovingOnlyActionableRowKeepsNonterminalPageLoadable() async throws {
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (queueData(posts: [post(id: "first")], hasNextPage: true, endCursor: "opaque-next"), 200, 0),
            (queueData(posts: [post(id: "second")]), 200, 0)
        ]
        CannedFeedURLProtocol.handlers["/api/v1/posts/first/clearances"] = (clearanceData("approved"), 200)
        let viewModel = try NativeReviewQueueViewModel(
            client: makeClient(),
            isSignedIn: true,
            isAdministrator: true
        )
        await viewModel.load()

        await viewModel.perform(.approve, postId: "first")

        XCTAssertTrue(viewModel.items.isEmpty)
        XCTAssertEqual(viewModel.state, .loaded)
        XCTAssertTrue(viewModel.canLoadMore)
        XCTAssertEqual(viewModel.endCursor, "opaque-next")

        await viewModel.loadMore()

        XCTAssertEqual(viewModel.items.map(\.id), ["second"])
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.last?.query, "limit=25&after=opaque-next")
    }

    func testFilteredNonterminalPageRemainsLoaded() async throws {
        CannedFeedURLProtocol.handlers[path] = (
            queueData(
                posts: [post(id: "approved", status: "approved"), post(id: "pending", status: "pending")],
                hasNextPage: true,
                endCursor: "filtered-next"
            ),
            200
        )
        let viewModel = try NativeReviewQueueViewModel(
            client: makeClient(),
            isSignedIn: true,
            isAdministrator: true
        )

        await viewModel.load()

        XCTAssertTrue(viewModel.items.isEmpty)
        XCTAssertEqual(viewModel.state, .loaded)
        XCTAssertTrue(viewModel.canLoadMore)
        XCTAssertEqual(viewModel.endCursor, "filtered-next")
    }

    func testMutationFailureRecoversAndPreservesRow() async throws {
        CannedFeedURLProtocol.handlers[path] = (queueData(posts: [post(id: "one")]), 200)
        let viewModel = try NativeReviewQueueViewModel(client: makeClient(), isSignedIn: true, isAdministrator: true)
        await viewModel.load()
        CannedFeedURLProtocol.handlers["/api/v1/posts/one/clearances"] = (Data("{}".utf8), 500)

        await viewModel.perform(.reject, postId: "one")

        XCTAssertEqual(viewModel.items.map(\.id), ["one"])
        XCTAssertFalse(viewModel.isMutating(postId: "one"))
        XCTAssertNotNil(viewModel.errorMessage)

        CannedFeedURLProtocol.handlers["/api/v1/posts/one/clearances"] = (clearanceData("in_review"), 200)
        await viewModel.perform(.reReview, postId: "one")
        XCTAssertEqual(viewModel.items[0].clearanceStatus, .inReview)
        XCTAssertNil(viewModel.errorMessage)
    }

    func testSameRowMutationIsSuppressedAndDifferentRowsMayMutate() async throws {
        CannedFeedURLProtocol.handlers[path] = (queueData(posts: [post(id: "one"), post(id: "two")]), 200)
        let viewModel = try NativeReviewQueueViewModel(client: makeClient(), isSignedIn: true, isAdministrator: true)
        await viewModel.load()
        CannedFeedURLProtocol.queuedHandlers["/api/v1/posts/one/clearances"] = [(clearanceData("rejected"), 200, 0.15)]
        CannedFeedURLProtocol.queuedHandlers["/api/v1/posts/two/clearances"] = [(clearanceData("in_review"), 200, 0.15)]

        async let first: Void = viewModel.perform(.reject, postId: "one")
        async let duplicate: Void = viewModel.perform(.approve, postId: "one")
        async let other: Void = viewModel.perform(.reReview, postId: "two")
        _ = await (first, duplicate, other)

        let mutationURLs = CannedFeedURLProtocol.capturedURLs.filter { $0.path.hasSuffix("/clearances") }
        XCTAssertEqual(mutationURLs.filter { $0.path.contains("/one/") }.count, 1)
        XCTAssertEqual(mutationURLs.filter { $0.path.contains("/two/") }.count, 1)
        XCTAssertEqual(viewModel.items.first { $0.id == "two" }?.clearanceStatus, .inReview)
    }

    func testInReviewRowDoesNotRequestReReview() async throws {
        CannedFeedURLProtocol.handlers[path] = (queueData(posts: [post(id: "one", status: "in_review")]), 200)
        let viewModel = try NativeReviewQueueViewModel(
            client: makeClient(),
            isSignedIn: true,
            isAdministrator: true
        )
        await viewModel.load()

        await viewModel.perform(.reReview, postId: "one")

        XCTAssertEqual(viewModel.items[0].clearanceStatus, .inReview)
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.filter { $0.path.hasSuffix("/clearances") }.isEmpty)
    }

    func testPendingRowDoesNotRequestAnyAction() async throws {
        let viewModel = try NativeReviewQueueViewModel(
            client: makeClient(),
            isSignedIn: true,
            isAdministrator: true
        )
        viewModel.items = try [
            NativeReviewQueueItem(
                post: decodedPost(id: "pending", status: "pending"),
                clearanceStatus: .pending
            )
        ]
        viewModel.state = .loaded

        await viewModel.perform(.approve, postId: "pending")
        await viewModel.perform(.reject, postId: "pending")
        await viewModel.perform(.reReview, postId: "pending")

        XCTAssertEqual(viewModel.items[0].clearanceStatus, .pending)
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.filter { $0.path.hasSuffix("/clearances") }.isEmpty)
    }

    func testPendingMutationResponsePreservesStatusAndShowsUnsupportedError() async throws {
        CannedFeedURLProtocol.handlers[path] = (queueData(posts: [post(id: "one")]), 200)
        let viewModel = try NativeReviewQueueViewModel(
            client: makeClient(),
            isSignedIn: true,
            isAdministrator: true
        )
        await viewModel.load()
        CannedFeedURLProtocol.handlers["/api/v1/posts/one/clearances"] = (clearanceData("pending"), 200)

        await viewModel.perform(.approve, postId: "one")

        XCTAssertEqual(viewModel.items[0].clearanceStatus, .rejected)
        XCTAssertEqual(
            viewModel.errorMessage,
            .message(.nativeSwiftModerationReportsReviewQueueUnsupportedStatus)
        )
    }

    func testCancellationPreservesAuthorizationGatesAndLoadedData() async throws {
        let client = try makeClient()
        let signedOut = NativeReviewQueueViewModel(client: client, isSignedIn: false, isAdministrator: false)
        let member = NativeReviewQueueViewModel(client: client, isSignedIn: true, isAdministrator: false)
        signedOut.cancelListOperations()
        member.cancelListOperations()
        XCTAssertEqual(signedOut.state, .signInRequired)
        XCTAssertEqual(member.state, .administratorRequired)

        CannedFeedURLProtocol.handlers[path] = (
            queueData(posts: [post(id: "kept")], hasNextPage: true, endCursor: "kept"),
            200
        )
        let administrator = NativeReviewQueueViewModel(client: client, isSignedIn: true, isAdministrator: true)
        await administrator.load()
        administrator.cancelListOperations()

        XCTAssertEqual(administrator.state, .loaded)
        XCTAssertEqual(administrator.items.map(\.id), ["kept"])
        XCTAssertTrue(administrator.hasNextPage)
        XCTAssertEqual(administrator.endCursor, "kept")
    }

    private func queueData(posts: [String], hasNextPage: Bool = false, endCursor: String? = nil) -> Data {
        let cursor = endCursor.map { #""\#($0)""# } ?? "null"
        return Data(
            #"{"page_info":{"has_next_page":\#(hasNextPage),"end_cursor":\#(cursor),"start_cursor":null},"results":[\#(posts.joined(separator: ","))]}"#
                .utf8
        )
    }

    private func post(
        id: String,
        status: String = "rejected",
        author: String? = "author-1",
        rootId: String? = nil
    ) -> String {
        let authorJSON = author.map { #""\#($0)""# } ?? "null"
        let rootJSON = rootId.map { #""\#($0)""# } ?? "null"
        return #"{"id":"\#(id)","title":"Title \#(id)","declared_language":null,"lingua_rs_detected_language":null,"slug":"\#(id)","markdown_preview":"Preview \#(id)","post_type":"discussion","created_by_id":\#(authorJSON),"created_at":"2026-06-01T11:30:00.000Z","root_id":\#(rootJSON),"root_post_type":"discussion","root_slug":"root-slug","clearance_status":"\#(status)","clearance_updated_at":null,"moderation_summary":{"disposition":"review","evidence_summary":{"flagged_category_count":0,"signal_count":1},"reason_codes":["spam_signal"]},"media_reveal":{"requires_reveal":false,"images":[]}}"#
    }

    private func clearanceData(_ status: String) -> Data {
        Data(#"{"clearance_status":"\#(status)"}"#.utf8)
    }

    private func decodedPost(id: String, status: String) throws -> AdminReviewQueuePost {
        try JSONDecoder.vouchaFixtureDecoder.decode(
            AdminReviewQueuePost.self,
            from: Data(post(id: id, status: status).utf8)
        )
    }
}
