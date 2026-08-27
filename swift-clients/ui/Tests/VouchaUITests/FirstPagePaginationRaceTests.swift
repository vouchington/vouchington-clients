import Foundation
import VouchaAPI
import VouchaCore
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class FirstPagePaginationRaceTests: NativeRouteSurfaceViewModelTestCase {
    func testCommunityTabSwitchRejectsSuspendedModerationPage() async throws {
        let reportsPath = "/api/v1/communities/builders/reports/pending"
        CannedFeedURLProtocol.handlers["/api/v1/communities/builders/posts/pending"] = (
            Data(#"{"results":[],"posts":{},"page_info":{"has_next_page":false,"end_cursor":null}}"#.utf8),
            200
        )
        CannedFeedURLProtocol.handlers[reportsPath] = (
            ApiFixtureLoader.data("native.community.pending-reports.paginated"),
            200
        )
        CannedFeedURLProtocol.suspendResponse(path: reportsPath)
        let viewModel = try CommunityDetailViewModel(
            client: makeClient(),
            slug: "builders",
            initialTab: .moderation
        )
        let revision = viewModel.communityLoadRevision
        let client = try makeClient()

        let staleLoad = Task {
            try await viewModel.loadModerationRows(client: client, tab: .moderation, revision: revision)
        }
        await waitForSuspendedResponse(path: reportsPath)
        viewModel.selectedTab = .posts
        CannedFeedURLProtocol.releaseResponse(path: reportsPath)
        _ = try? await staleLoad.value

        XCTAssertEqual(viewModel.selectedTab, .posts)
        XCTAssertFalse(viewModel.pendingReportPagination.hasLoadedPage)
        XCTAssertTrue(viewModel.pendingReportPagination.items.isEmpty)
    }

    func testSuspendedSettingsPageCannotRestoreRevokedApiKey() async throws {
        let path = "/api/v1/my/api-keys"
        let pageData = ApiFixtureLoader.data("native.my.api-keys.paginated")
        let page = try settingsDecoder.decode(SettingsListResponse<ApiKey>.self, from: pageData)
        let client = try makeClient()
        let viewModel = SettingsViewModel(client: client)
        viewModel.replaceApiKeyPage(page)
        let keyId = try XCTUnwrap(viewModel.apiKeys.first?.id)
        let stalePage = try terminalPage(from: pageData)
        CannedFeedURLProtocol.handlers[path] = (stalePage, 200)
        CannedFeedURLProtocol.suspendResponse(path: path)

        let staleLoad = Task { await viewModel.loadMoreApiKeys() }
        await waitForSuspendedResponse(path: path)
        viewModel.reconcileRevokedApiKey(id: keyId)
        CannedFeedURLProtocol.releaseResponse(path: path)
        await staleLoad.value

        XCTAssertTrue(viewModel.apiKeys.isEmpty)
        XCTAssertNil(viewModel.apiKeyPagination.lastError)
        XCTAssertFalse(viewModel.apiKeyPagination.isLoading)
        XCTAssertTrue(viewModel.apiKeyPagination.hasMore)
        XCTAssertEqual(viewModel.apiKeyPagination.endCursor, "fixture-end-cursor")
    }

    func testLateInitialThreadFailureCannotReplaceNewerSuccessfulThread() async throws {
        let detailPath = "/api/v1/posts/comment-root-post"
        let descendantsPath = "\(detailPath)/descendants"
        let detail = ApiFixtureLoader.data("native.comments.post-detail.default")
        let descendants = ApiFixtureLoader.data("native.comments.descendants.default")
        CannedFeedURLProtocol.queuedHandlers[detailPath] = [(detail, 200, 0.1), (detail, 200, 0)]
        CannedFeedURLProtocol.queuedHandlers[descendantsPath] = [
            (Data(#"{"message":"stale"}"#.utf8), 500, 0.1),
            (descendants, 200, 0)
        ]
        let viewModel = try makeCommentViewModel()

        let staleLoad = Task { await viewModel.loadThread() }
        await waitForCapturedRequest(path: descendantsPath)
        await viewModel.loadThread()
        await staleLoad.value

        XCTAssertEqual(viewModel.rootPost?.id, "comment-root-post")
        XCTAssertTrue(isLoaded(viewModel.threadState))
        XCTAssertEqual(viewModel.descendantPosts.count, 4)
        XCTAssertNil(viewModel.descendantPagination.lastError)
    }

    func testLatePermalinkFailureCannotReplaceNewerPermalink() async throws {
        let detailPath = "/api/v1/posts/comment-root-post"
        let descendantsPath = "\(detailPath)/descendants"
        let detail = ApiFixtureLoader.data("native.comments.post-detail.default")
        let descendants = ApiFixtureLoader.data("native.comments.descendants.default")
        CannedFeedURLProtocol.queuedHandlers[detailPath] = [(detail, 200, 0.1), (detail, 200, 0)]
        CannedFeedURLProtocol.queuedHandlers[descendantsPath] = [
            (descendants, 200, 0.1),
            (descendants, 200, 0)
        ]
        CannedFeedURLProtocol.queuedHandlers["/api/v1/posts/comment-a/ancestors"] = [
            (Data(#"{"message":"stale"}"#.utf8), 500, 0.1)
        ]
        CannedFeedURLProtocol.handlers["/api/v1/posts/comment-b/ancestors"] = (
            ApiFixtureLoader.data("native.comments.ancestors.permalink"),
            200
        )
        let viewModel = try makeCommentViewModel()

        let staleLoad = Task { await viewModel.loadPermalink(targetCommentId: "comment-a") }
        await waitForCapturedRequest(path: "/api/v1/posts/comment-a/ancestors")
        await viewModel.loadPermalink(targetCommentId: "comment-b")
        await staleLoad.value

        XCTAssertEqual(viewModel.focusedCommentId, "comment-b")
        XCTAssertTrue(isLoaded(viewModel.threadState))
        XCTAssertTrue(isLoaded(viewModel.ancestorState))
        XCTAssertTrue(isLoaded(viewModel.focusedState))
    }

    private var settingsDecoder: JSONDecoder {
        let decoder = JSONDecoder()
        decoder.dateDecodingStrategy = .iso8601
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        return decoder
    }

    private func terminalPage(from data: Data) throws -> Data {
        var object = try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: Any])
        var pageInfo = try XCTUnwrap(object["page_info"] as? [String: Any])
        pageInfo["has_next_page"] = false
        pageInfo["end_cursor"] = "stale-end-cursor"
        object["page_info"] = pageInfo
        return try JSONSerialization.data(withJSONObject: object)
    }

    private func makeCommentViewModel() throws -> NativeCommentThreadViewModel {
        let client = try makeClient()
        return NativeCommentThreadViewModel(
            client: client,
            rootPostId: "comment-root-post",
            currentUserId: "user-1"
        )
    }

    private func waitForSuspendedResponse(path: String) async {
        for _ in 0 ..< 200 where !CannedFeedURLProtocol.hasSuspendedResponse(path: path) {
            try? await Task.sleep(for: .milliseconds(1))
        }
        XCTAssertTrue(CannedFeedURLProtocol.hasSuspendedResponse(path: path))
    }

    private func waitForCapturedRequest(path: String) async {
        for _ in 0 ..< 200 where !CannedFeedURLProtocol.hasCapturedRequest(path: path) {
            try? await Task.sleep(for: .milliseconds(1))
        }
        XCTAssertTrue(CannedFeedURLProtocol.hasCapturedRequest(path: path))
    }

    private func isLoaded(_ state: LoadState) -> Bool {
        if case .loaded = state {
            return true
        }
        return false
    }
}
