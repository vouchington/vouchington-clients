import Foundation
@testable import VouchaFeatures
@testable import VouchaModels
import XCTest

@MainActor
final class NativeSupportViewModelRegressionTests: NativeRouteSurfaceViewModelTestCase {
    func testSupportViewModelIgnoresStaleThreadDetailResponsesWhenSelectionChanges() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/support-threads/thread-1"] = [(
            NativeChatSupportSurfaceTests.supportThreadDetailData,
            200,
            0.1
        )]
        CannedFeedURLProtocol.handlers["/api/v1/my/support-threads/thread-2"] = (
            Self.supportThreadTwoDetailData,
            200
        )

        let client = try makeClient()
        let viewModel = NativeSupportViewModel(client: client, routeMatch: nil)
        viewModel.threads = try [
            makeThread(
                id: "thread-1",
                subject: "Need help",
                conversationId: "conversation-1",
                createdAt: "2026-01-01T00:00:00Z",
                updatedAt: "2026-01-01T00:01:00Z"
            ),
            makeThread(
                id: "thread-2",
                subject: "Second request",
                conversationId: "conversation-2",
                createdAt: "2026-01-01T00:02:00Z",
                updatedAt: "2026-01-01T00:03:00Z"
            )
        ]

        let staleSelection = Task { await viewModel.selectThread(id: "thread-1") }
        try await Task.sleep(nanoseconds: 10_000_000)
        await viewModel.selectThread(id: "thread-2")
        await staleSelection.value

        XCTAssertEqual(viewModel.selectedThreadId, "thread-2")
        XCTAssertEqual(viewModel.selectedThreadMessages.map(\.id), ["support-message-2"])
        XCTAssertEqual(viewModel.selectedThreadMessages.first?.bodyText, "Thread two reply")
    }

    func testSupportViewModelClearsDetailLoadingAfterStaleResponsesFinish() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/support-threads/thread-1"] = [(
            NativeChatSupportSurfaceTests.supportThreadDetailData,
            200,
            0.1
        )]
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/support-threads/thread-2"] = [(
            Self.supportThreadTwoDetailData,
            200,
            0.1
        )]

        let client = try makeClient()
        let viewModel = NativeSupportViewModel(client: client, routeMatch: nil)
        viewModel.threads = try [
            makeThread(
                id: "thread-1",
                subject: "Need help",
                conversationId: "conversation-1",
                createdAt: "2026-01-01T00:00:00Z",
                updatedAt: "2026-01-01T00:01:00Z"
            ),
            makeThread(
                id: "thread-2",
                subject: "Second request",
                conversationId: "conversation-2",
                createdAt: "2026-01-01T00:02:00Z",
                updatedAt: "2026-01-01T00:03:00Z"
            )
        ]

        let firstSelection = Task { await viewModel.selectThread(id: "thread-1") }
        try await Task.sleep(nanoseconds: 10_000_000)
        let secondSelection = Task { await viewModel.selectThread(id: "thread-2") }
        try await Task.sleep(nanoseconds: 10_000_000)
        await viewModel.selectThread(id: nil)

        await firstSelection.value
        await secondSelection.value

        XCTAssertNil(viewModel.selectedThreadId)
        XCTAssertFalse(viewModel.isLoadingDetail)
        XCTAssertTrue(viewModel.selectedThreadMessages.isEmpty)
    }

    func testSupportViewModelSkipsConcurrentThreadPaginationRequests() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/support-threads"] = [
            (NativeChatSupportSurfaceTests.supportThreadsPageTwoData, 200, 0.1),
            (NativeChatSupportSurfaceTests.supportThreadsPageTwoData, 200, 0.1)
        ]

        let client = try makeClient()
        let viewModel = NativeSupportViewModel(client: client, routeMatch: nil)
        viewModel.threads = try [
            makeThread(
                id: "thread-1",
                subject: "First request",
                conversationId: "conversation-1",
                createdAt: "2026-01-01T00:00:00Z",
                updatedAt: "2026-01-01T00:01:00Z"
            )
        ]
        viewModel.listPageInfo = .init(hasNextPage: true, endCursor: "cursor-1")

        let first = Task { await viewModel.loadMoreThreads() }
        let second = Task { await viewModel.loadMoreThreads() }
        await first.value
        await second.value

        XCTAssertEqual(viewModel.threads.map(\.id), ["thread-1", "thread-2"])
        XCTAssertEqual(
            CannedFeedURLProtocol.capturedURLs.filter { $0.path == "/api/v1/my/support-threads" }.count,
            1
        )
    }

    func testSupportViewModelPreservesSelectedThreadDuringResetRefresh() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/my/support-threads"] = (
            NativeChatSupportSurfaceTests.supportThreadsListData,
            200
        )

        let client = try makeClient()
        let viewModel = NativeSupportViewModel(client: client, routeMatch: nil)
        let localThread = try makeThread(
            id: "thread-local",
            subject: "Local follow-up",
            conversationId: "conversation-local",
            createdAt: "2026-01-01T00:10:00Z",
            updatedAt: "2026-01-01T00:11:00Z"
        )
        viewModel.threads = [localThread]
        viewModel.selectedThreadId = "thread-local"

        await viewModel.loadThreads(reset: true)

        XCTAssertEqual(viewModel.threads.map(\.id), ["thread-0", "thread-local"])
        XCTAssertEqual(viewModel.selectedThreadId, "thread-local")
    }

    func testSupportViewModelSkipsConcurrentThreadMessagePaginationRequests() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/support-threads/thread-1"] = [
            (NativeChatSupportSurfaceTests.supportThreadDetailPageTwoData, 200, 0.1),
            (NativeChatSupportSurfaceTests.supportThreadDetailPageTwoData, 200, 0.1)
        ]

        let client = try makeClient()
        let viewModel = NativeSupportViewModel(client: client, routeMatch: nil)
        viewModel.selectedThreadId = "thread-1"
        viewModel.selectedThreadMessages = try [
            makeMessage(
                id: "support-message-1",
                threadId: "thread-1",
                direction: "inbound",
                bodyText: "Initial note",
                createdAt: "2026-01-01T00:00:00Z"
            )
        ]
        viewModel.selectedThreadPageInfo = .init(hasNextPage: true, endCursor: "cursor-1")

        let first = Task { await viewModel.loadMoreSelectedThreadMessages() }
        let second = Task { await viewModel.loadMoreSelectedThreadMessages() }
        await first.value
        await second.value

        XCTAssertEqual(viewModel.selectedThreadMessages.map(\.id), ["support-message-0", "support-message-1"])
        XCTAssertEqual(
            CannedFeedURLProtocol.capturedURLs.filter { $0.path == "/api/v1/my/support-threads/thread-1" }.count,
            1
        )
    }

    func testSupportViewModelSkipsStaleCreateCompletionAfterDraftChanges() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/support-threads"] = [
            (NativeChatSupportSurfaceTests.createdThreadData, 201, 0.1)
        ]
        CannedFeedURLProtocol.handlers["/api/v1/my/support-threads/thread-1"] = (
            NativeChatSupportSurfaceTests.supportThreadDetailData,
            200
        )

        let client = try makeClient()
        let viewModel = NativeSupportViewModel(client: client, routeMatch: nil)
        viewModel.subject = "Need help"
        viewModel.message = "Original note"
        viewModel.conversationId = "conversation-1"

        let createTask = Task { await viewModel.createThread() }
        try await Task.sleep(nanoseconds: 10_000_000)
        await viewModel.startNewThread()
        viewModel.subject = "Different subject"
        viewModel.message = "Different body"
        viewModel.conversationId = "conversation-2"

        await createTask.value

        XCTAssertFalse(viewModel.isCreating)
        XCTAssertNil(viewModel.selectedThreadId)
        XCTAssertEqual(viewModel.subject, "Different subject")
        XCTAssertEqual(viewModel.message, "Different body")
        XCTAssertEqual(viewModel.conversationId, "conversation-2")
        XCTAssertTrue(viewModel.threads.isEmpty)
        XCTAssertNil(viewModel.createErrorMessage)
        XCTAssertEqual(
            CannedFeedURLProtocol.capturedURLs.filter { $0.path == "/api/v1/my/support-threads" }.count,
            1
        )
        XCTAssertEqual(
            CannedFeedURLProtocol.capturedURLs.filter { $0.path == "/api/v1/my/support-threads/thread-1" }.count,
            0
        )
    }

    func testSupportViewModelSkipsDuplicateCreateWhileSubmitting() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/my/support-threads"] = [
            (NativeChatSupportSurfaceTests.createdThreadData, 201, 0.1),
            (NativeChatSupportSurfaceTests.createdThreadData, 201, 0.1)
        ]
        CannedFeedURLProtocol.handlers["/api/v1/my/support-threads/thread-1"] = (
            NativeChatSupportSurfaceTests.supportThreadDetailData,
            200
        )
        let client = try makeClient()
        let viewModel = NativeSupportViewModel(client: client, routeMatch: nil)
        viewModel.subject = "Need help"
        viewModel.message = "Initial note"

        let first = Task { await viewModel.createThread() }
        try await Task.sleep(nanoseconds: 10_000_000)
        await viewModel.createThread()
        await first.value

        XCTAssertFalse(viewModel.isCreating)
        XCTAssertEqual(viewModel.threads.map(\.id), ["thread-1"])
        XCTAssertEqual(
            CannedFeedURLProtocol.capturedURLs.filter { $0.path == "/api/v1/my/support-threads" }.count,
            1
        )
    }
}

private extension NativeSupportViewModelRegressionTests {
    func makeThread(
        id: String,
        subject: String,
        conversationId: String,
        createdAt: String,
        updatedAt: String
    ) throws -> SupportThread {
        try NativeChatSupportSurfaceTests.decode(
            SupportThread.self,
            """
            {
              "id": "\(id)",
              "support_contact_id": "contact-1",
              "subject": "\(subject)",
              "conversation_id": "\(conversationId)",
              "created_at": "\(createdAt)",
              "updated_at": "\(updatedAt)",
              "assigned_at": null,
              "assigned_to_id": null,
              "resolved_at": null,
              "resolved_by_id": null,
              "status": "open",
              "contact_user_id": "user-1"
            }
            """
        )
    }

    func makeMessage(
        id: String,
        threadId: String,
        direction: String,
        bodyText: String,
        createdAt: String
    ) throws -> SupportMessage {
        try NativeChatSupportSurfaceTests.decode(
            SupportMessage.self,
            """
            {
              "id": "\(id)",
              "support_thread_id": "\(threadId)",
              "direction": "\(direction)",
              "body_text": "\(bodyText)",
              "body_html": "<p>\(bodyText)</p>",
              "created_at": "\(createdAt)",
              "created_by_id": "user-1",
              "updated_at": "\(createdAt)",
              "email_message_id": null,
              "email_subject": null,
              "email_from": null,
              "email_to": null,
              "drafted_at": null,
              "edited_at": null,
              "edited_by_id": null,
              "approved_at": null,
              "approved_by_id": null,
              "sent_at": null
            }
            """
        )
    }

    static let supportThreadTwoDetailData = Data(
        """
        {
          "thread": {
            "id": "thread-2",
            "support_contact_id": "contact-1",
            "subject": "Second request",
            "conversation_id": "conversation-2",
            "created_at": "2026-01-01T00:02:00Z",
            "updated_at": "2026-01-01T00:03:00Z",
            "assigned_at": null,
            "assigned_to_id": null,
            "resolved_at": null,
            "resolved_by_id": null,
            "status": "open",
            "contact_user_id": "user-1"
          },
          "messages": [
            {
              "id": "support-message-2",
              "support_thread_id": "thread-2",
              "direction": "outbound",
              "body_text": "Thread two reply",
              "body_html": "<p>Thread two reply</p>",
              "created_at": "2026-01-01T00:04:00Z",
              "created_by_id": "user-2",
              "updated_at": "2026-01-01T00:04:00Z",
              "email_message_id": null,
              "email_subject": null,
              "email_from": null,
              "email_to": null,
              "drafted_at": null,
              "edited_at": null,
              "edited_by_id": null,
              "approved_at": null,
              "approved_by_id": null,
              "sent_at": null
            }
          ],
          "page_info": {
            "has_next_page": false,
            "start_cursor": "support-message-2",
            "end_cursor": null
          }
        }
        """.utf8
    )
}
