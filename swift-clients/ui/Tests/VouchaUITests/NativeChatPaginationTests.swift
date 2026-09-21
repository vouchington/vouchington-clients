import Foundation
@testable import VouchaFeatures
@testable import VouchaModels
import XCTest

@MainActor
final class NativeChatPaginationTests: NativeRouteSurfaceViewModelTestCase {
    func testConversationContinuationDeduplicatesIncomingAndPreservesCurrentOverlap() async throws {
        let path = "/api/v1/my/conversations"
        CannedFeedURLProtocol.handlers[path] = (Self.conversationPageWithDuplicates, 200)
        let viewModel = try makeViewModel()
        viewModel.listPageInfo = .init(hasNextPage: true, endCursor: "older-conversations")

        await viewModel.loadMoreConversations()

        XCTAssertEqual(viewModel.conversations.map(\.id), ["conversation-1", "conversation-2"])
        XCTAssertEqual(viewModel.conversations[0].title, "Paged chat")
        XCTAssertEqual(viewModel.conversations[1].title, "Older conversation")
        XCTAssertFalse(viewModel.canLoadMoreConversations)
        XCTAssertEqual(
            CannedFeedURLProtocol.capturedURLs.last?.query,
            "limit=50&after=older-conversations"
        )
    }

    func testStaleConversationPageFinalizerCannotClearNewGenerationRequest() async throws {
        let path = "/api/v1/my/conversations"
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (Self.conversationPage([("conversation-stale", "Stale")]), 200, 0.1),
            (Self.conversationPage([("conversation-1", "Refreshed")], endCursor: "refresh-cursor"), 200, 0),
            (Self.conversationPage([("conversation-2", "Older conversation")]), 200, 0.2)
        ]
        let viewModel = try makeViewModel()
        viewModel.listPageInfo = .init(hasNextPage: true, endCursor: "stale-cursor")

        let stalePage = Task { await viewModel.loadMoreConversations() }
        try await Task.sleep(nanoseconds: 10_000_000)
        await viewModel.loadConversations(reset: true)
        let currentPage = Task { await viewModel.loadMoreConversations() }
        try await Task.sleep(nanoseconds: 120_000_000)

        XCTAssertTrue(viewModel.isLoadingMoreConversations)
        XCTAssertFalse(viewModel.canLoadMoreConversations)
        await stalePage.value
        await currentPage.value
        XCTAssertEqual(viewModel.conversations.map(\.id), ["conversation-1", "conversation-2"])
        XCTAssertFalse(viewModel.isLoadingMoreConversations)
    }

    func testOlderMessagesPrependOnceDeduplicateIncomingAndPreserveCurrentOverlap() async throws {
        let path = "/api/v1/my/conversations/conversation-1/messages"
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (Self.pageOne, 200, 0),
            (Self.pageTwoWithDuplicates, 200, 0.1)
        ]
        let viewModel = try makeViewModel()
        await viewModel.selectConversation(id: "conversation-1")

        let first = Task { await viewModel.loadOlderMessages() }
        let second = Task { await viewModel.loadOlderMessages() }
        await first.value
        await second.value

        XCTAssertEqual(viewModel.messages.map(\.id), ["message-1", "message-2", "message-3"])
        XCTAssertEqual(viewModel.messages.first(where: { $0.id == "message-2" })?.content, "Middle current")
        XCTAssertFalse(viewModel.canLoadOlderMessages)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.filter { $0.path == path }.count, 2)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.last?.query, "limit=50&after=older-cursor")
    }

    func testOlderMessagesFailurePreservesCurrentPageAndCanRetry() async throws {
        let path = "/api/v1/my/conversations/conversation-1/messages"
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (Self.pageOne, 200, 0),
            (Data(#"{"error":"offline"}"#.utf8), 503, 0),
            (Self.pageTwo, 200, 0)
        ]
        let viewModel = try makeViewModel()
        await viewModel.selectConversation(id: "conversation-1")

        await viewModel.loadOlderMessages()
        XCTAssertEqual(viewModel.messages.map(\.id), ["message-2", "message-3"])
        XCTAssertNotNil(viewModel.detailErrorMessage)
        XCTAssertTrue(viewModel.canLoadOlderMessages)

        await viewModel.loadOlderMessages()
        XCTAssertEqual(viewModel.messages.map(\.id), ["message-1", "message-2", "message-3"])
        XCTAssertNil(viewModel.detailErrorMessage)
    }

    func testOlderMessagesResponseAndFinalizerAreIgnoredAfterSelectionChanges() async throws {
        let path = "/api/v1/my/conversations/conversation-1/messages"
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (Self.pageOne, 200, 0),
            (Self.pageTwo, 200, 0.1)
        ]
        let viewModel = try makeViewModel()
        await viewModel.selectConversation(id: "conversation-1")

        let older = Task { await viewModel.loadOlderMessages() }
        try await Task.sleep(nanoseconds: 10_000_000)
        await viewModel.selectConversation(id: nil)
        await older.value

        XCTAssertNil(viewModel.selectedConversationId)
        XCTAssertTrue(viewModel.messages.isEmpty)
        XCTAssertFalse(viewModel.isLoadingOlderMessages)
        XCTAssertNil(viewModel.detailErrorMessage)
    }

    private func makeViewModel() throws -> NativeChatViewModel {
        let viewModel = try NativeChatViewModel(client: makeClient(), routeMatch: nil)
        viewModel.conversations = try [NativeChatTestFixtures.decode(
            ChatConversation.self,
            #"{"id":"conversation-1","title":"Paged chat","created_at":"2026-01-01T00:00:00Z","created_by_id":"user-1","updated_at":"2026-01-01T00:03:00Z","updated_by_id":null,"deleted_at":null,"deleted_by_id":null}"#
        )]
        return viewModel
    }

    private static let pageOne = page(
        [("message-2", "Middle current"), ("message-3", "Newest")],
        hasNext: true,
        endCursor: "older-cursor"
    )
    private static let pageTwo = page([("message-1", "Oldest")], hasNext: false, endCursor: nil)
    private static let pageTwoWithDuplicates = page(
        [("message-1", "Oldest"), ("message-1", "Duplicate incoming"), ("message-2", "Stale overlap")],
        hasNext: false,
        endCursor: nil
    )
    private static let conversationPageWithDuplicates = conversationPage(
        [
            ("conversation-2", "Older conversation"),
            ("conversation-2", "Duplicate incoming"),
            ("conversation-1", "Stale overlap")
        ]
    )

    private static func conversationPage(
        _ conversations: [(String, String)],
        endCursor: String? = nil
    ) -> Data {
        let rows = conversations.map { id, title in
            #"{"id":"\#(id)","title":"\#(title)","created_at":"2026-01-01T00:00:00Z","created_by_id":"user-1","updated_at":"2026-01-01T00:00:00Z","updated_by_id":null,"deleted_at":null,"deleted_by_id":null}"#
        }.joined(separator: ",")
        let cursor = endCursor.map { #""\#($0)""# } ?? "null"
        return Data(
            #"{"results":[\#(rows)],"page_info":{"has_next_page":\#(endCursor != nil),"start_cursor":"start","end_cursor":\#(cursor)}}"#
                .utf8
        )
    }

    private static func page(
        _ messages: [(String, String)],
        hasNext: Bool,
        endCursor: String?
    ) -> Data {
        let rows = messages.map { id, content in
            #"{"id":"\#(id)","conversation_id":"conversation-1","created_at":"2026-01-01T00:01:00Z","created_by_id":"user-1","updated_at":"2026-01-01T00:01:00Z","updated_by_id":null,"deleted_at":null,"deleted_by_id":null,"content":{"role":"user","content":"\#(content)","error":null}}"#
        }.joined(separator: ",")
        let cursor = endCursor.map { #""\#($0)""# } ?? "null"
        return Data(
            #"{"results":[\#(rows)],"page_info":{"has_next_page":\#(hasNext),"start_cursor":"start","end_cursor":\#(cursor)}}"#
                .utf8
        )
    }
}
