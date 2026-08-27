import Foundation
@testable import VouchaFeatures
import VouchaLocalization
import XCTest

@MainActor
final class CommunityModmailPaginationTests: NativeRouteSurfaceViewModelTestCase {
    func testOlderThreadMessagesPrependDeduplicateAndPreserveCurrentRows() async throws {
        let path = "/api/v1/communities/builders/modmail/thread-1/messages"
        CannedFeedURLProtocol.handlers[path] = (Self.olderMessagesWithOverlap, 200)
        let viewModel = try makeThreadViewModel()

        await viewModel.loadMoreModmail()

        XCTAssertEqual(viewModel.summary.rows.map(\.detail), ["Oldest", "Current copy", "Newest"])
        XCTAssertNil(viewModel.modmailEndCursor)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.last?.query, "after=older-cursor&limit=25")
    }

    func testOlderThreadMessageFailurePreservesRowsAndAllowsRetry() async throws {
        let path = "/api/v1/communities/builders/modmail/thread-1/messages"
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (Data(#"{"error":"offline"}"#.utf8), 503, 0),
            (Self.olderMessages, 200, 0)
        ]
        let viewModel = try makeThreadViewModel()

        await viewModel.loadMoreModmail()
        XCTAssertEqual(viewModel.summary.rows.map(\.detail), ["Current copy", "Newest"])
        XCTAssertTrue(viewModel.canLoadMoreModmail)
        XCTAssertEqual(
            viewModel.modmailPaginationError,
            UiMessage(.nativeSwiftEmptyStateUnableToLoad)
        )

        await viewModel.loadMoreModmail()
        XCTAssertEqual(viewModel.summary.rows.map(\.detail), ["Oldest", "Current copy", "Newest"])
        XCTAssertNil(viewModel.modmailPaginationError)
    }

    func testStaleThreadPageAndFinalizerCannotAffectChangedTab() async throws {
        let path = "/api/v1/communities/builders/modmail/thread-1/messages"
        CannedFeedURLProtocol.queuedHandlers[path] = [(Self.olderMessages, 200, 0.1)]
        let viewModel = try makeThreadViewModel()

        let page = Task { await viewModel.loadMoreModmail() }
        try await Task.sleep(nanoseconds: 10_000_000)
        viewModel.selectedTab = .posts
        await page.value

        XCTAssertEqual(viewModel.summary.rows.map(\.detail), ["Current copy", "Newest"])
        XCTAssertEqual(viewModel.selectedTab, .posts)
        XCTAssertFalse(viewModel.isLoadingMoreModmail)
        XCTAssertNil(viewModel.modmailPaginationError)
    }

    private func makeThreadViewModel() throws -> CommunityDetailViewModel {
        let viewModel = try CommunityDetailViewModel(
            client: makeClient(),
            slug: "builders",
            initialTab: .modmail,
            modmailThreadId: "thread-1"
        )
        viewModel.summary.rows = [
            .init(icon: "bubble", title: .verbatim("@current"), detail: .verbatim("Current copy")),
            .init(icon: "bubble", title: .verbatim("@newer"), detail: .verbatim("Newest"))
        ]
        viewModel.modmailRowIds = ["message-current", "message-newer"]
        viewModel.modmailEndCursor = "older-cursor"
        return viewModel
    }

    private static let olderMessages = page([
        ("message-oldest", "Oldest")
    ])
    private static let olderMessagesWithOverlap = page([
        ("message-oldest", "Oldest"),
        ("message-oldest", "Duplicate incoming"),
        ("message-current", "Stale overlap")
    ])

    private static func page(_ messages: [(String, String)]) -> Data {
        let rows = messages.map { id, body in
            #"{"id":"\#(id)","conversation_id":"thread-1","body_text":"\#(body)","created_by_id":"mod-1","sender_username":"mod","created_at":"2026-01-01T00:00:00Z","updated_at":"2026-01-01T00:00:00Z","deleted_at":null}"#
        }.joined(separator: ",")
        return Data(
            #"{"results":[\#(rows)],"page_info":{"has_next_page":false,"start_cursor":"start","end_cursor":null}}"#
                .utf8
        )
    }
}
