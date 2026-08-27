@testable import VouchaFeatures
import XCTest

@MainActor
final class StaffSupportSendRecoveryTests: NativeRouteSurfaceViewModelTestCase {
    func testSendFailureRefetchPreservesUnsavedComposerState() async throws {
        let threadPath = "/api/v1/support/threads/thread-a"
        let messagesPath = "\(threadPath)/messages"
        CannedFeedURLProtocol.handlers[threadPath] = (threadResponse, 200)
        CannedFeedURLProtocol.queuedHandlers[messagesPath] = [
            (messagePage, 200, 0),
            (messagePage, 200, 0)
        ]
        CannedFeedURLProtocol.handlers["\(messagesPath)/message-a/sends"] = (
            Data("{}".utf8), 500
        )
        let model = try StaffSupportViewModel(
            client: makeClient(), administratorId: nil, mode: .threads, routeMatch: nil
        )

        await model.selectThread("thread-a")
        let message = try XCTUnwrap(model.messages.first)
        CannedFeedURLProtocol.suspendResponse(path: messagesPath)
        let recoveryRequest = CannedFeedURLProtocol.requestBarrier(path: messagesPath, method: "GET")
        let send = Task { await model.send(message) }
        _ = try await recoveryRequest.wait()
        model.replyText = "Newer unsaved reply"
        model.setDraftText("Newer unsaved draft", messageId: message.id)
        CannedFeedURLProtocol.releaseResponse(path: messagesPath)
        await send.value

        XCTAssertEqual(model.replyText, "Newer unsaved reply")
        XCTAssertEqual(model.draftText(for: message), "Newer unsaved draft")
        XCTAssertNotNil(model.errorMessage)
    }

    func testSameIDReselectionSuppressesStaleSendRecoveryError() async throws {
        let threadPath = "/api/v1/support/threads/thread-a"
        let messagesPath = "\(threadPath)/messages"
        CannedFeedURLProtocol.handlers[threadPath] = (threadResponse, 200)
        CannedFeedURLProtocol.queuedHandlers[messagesPath] = [
            (messagePage, 200, 0),
            (messagePage, 200, 0),
            (messagePage, 200, 0)
        ]
        CannedFeedURLProtocol.handlers["\(messagesPath)/message-a/sends"] = (
            Data("{}".utf8), 500
        )
        let model = try StaffSupportViewModel(
            client: makeClient(), administratorId: nil, mode: .threads, routeMatch: nil
        )

        await model.selectThread("thread-a")
        let message = try XCTUnwrap(model.messages.first)
        CannedFeedURLProtocol.suspendResponse(path: messagesPath)
        let recoveryRequest = CannedFeedURLProtocol.requestBarrier(path: messagesPath, method: "GET")
        let send = Task { await model.send(message) }
        _ = try await recoveryRequest.wait()

        let replacementRequest = CannedFeedURLProtocol.requestBarrier(path: messagesPath, method: "GET")
        let replacement = Task { await model.selectThread("thread-a") }
        _ = try await replacementRequest.wait()
        CannedFeedURLProtocol.releaseNewestResponse(path: messagesPath)
        _ = await replacement.value
        model.replyText = "Newer reply"
        model.setDraftText("Newer draft", messageId: "message-a")
        CannedFeedURLProtocol.releaseResponse(path: messagesPath)
        await send.value

        XCTAssertEqual(model.selectedThread?.id, "thread-a")
        XCTAssertEqual(model.replyText, "Newer reply")
        XCTAssertEqual(model.draftText(for: message), "Newer draft")
        XCTAssertNil(model.errorMessage)
    }

    func testDifferentThreadSelectionSuppressesLateSendRecovery() async throws {
        let threadAPath = "/api/v1/support/threads/thread-a"
        let messagesAPath = "\(threadAPath)/messages"
        let threadBPath = "/api/v1/support/threads/thread-b"
        let messagesBPath = "\(threadBPath)/messages"
        CannedFeedURLProtocol.handlers[threadAPath] = (threadResponse, 200)
        CannedFeedURLProtocol.queuedHandlers[messagesAPath] = [
            (messagePage, 200, 0),
            (messagePage, 200, 0)
        ]
        CannedFeedURLProtocol.handlers["\(messagesAPath)/message-a/sends"] = (
            Data("{}".utf8), 500
        )
        CannedFeedURLProtocol.handlers[threadBPath] = (threadBResponse, 200)
        CannedFeedURLProtocol.handlers[messagesBPath] = (messageBPage, 200)
        let model = try StaffSupportViewModel(
            client: makeClient(), administratorId: nil, mode: .threads, routeMatch: nil
        )

        await model.selectThread("thread-a")
        let messageA = try XCTUnwrap(model.messages.first)
        CannedFeedURLProtocol.suspendResponse(path: messagesAPath)
        let recoveryRequest = CannedFeedURLProtocol.requestBarrier(path: messagesAPath, method: "GET")
        let send = Task { await model.send(messageA) }
        _ = try await recoveryRequest.wait()

        CannedFeedURLProtocol.suspendResponse(path: messagesBPath)
        let threadBRequest = CannedFeedURLProtocol.requestBarrier(path: messagesBPath, method: "GET")
        let selectB = Task { await model.selectThread("thread-b") }
        _ = try await threadBRequest.wait()
        CannedFeedURLProtocol.releaseResponse(path: messagesBPath)
        _ = await selectB.value
        let messageB = try XCTUnwrap(model.messages.first)
        model.replyText = "B reply"
        model.setDraftText("B draft", messageId: messageB.id)
        model.errorMessage = .verbatim("B error")

        CannedFeedURLProtocol.releaseResponse(path: messagesAPath)
        await send.value

        XCTAssertEqual(model.selectedThread?.id, "thread-b")
        XCTAssertEqual(model.messages.map(\.id), ["message-b"])
        XCTAssertEqual(model.replyText, "B reply")
        XCTAssertEqual(model.draftText(for: messageB), "B draft")
        XCTAssertEqual(model.errorMessage, .verbatim("B error"))
    }

}

private extension StaffSupportSendRecoveryTests {
    var threadResponse: Data {
        Data("{\"thread\":\(thread)}".utf8)
    }

    var messagePage: Data {
        let json = "{\"results\":[\(message)],\"page_info\":{\"has_next_page\":false,"
            + "\"start_cursor\":null,\"end_cursor\":null}}"
        return Data(json.utf8)
    }

    var thread: String {
        """
        {
            "id":"thread-a","support_contact_id":"contact-1","subject":"Subject",
            "conversation_id":null,"created_at":"2026-01-01T00:00:00Z",
            "updated_at":"2026-01-01T00:00:00Z","assigned_at":null,"assigned_to_id":null,
            "resolved_at":null,"resolved_by_id":null,"status":"open","contact_user_id":null
        }
        """
    }

    var message: String {
        """
        {
            "id":"message-a","support_thread_id":"thread-a","direction":"outbound",
            "body_text":"Body","body_html":"","created_at":"2026-01-01T00:00:00Z",
            "created_by_id":null,"updated_at":"2026-01-01T00:00:00Z","email_message_id":null,
            "email_subject":null,"email_from":null,"email_to":null,"drafted_at":null,
            "edited_at":null,"edited_by_id":null,"approved_at":null,"approved_by_id":null,
            "sent_at":null
        }
        """
    }

    var threadBResponse: Data {
        Data("{\"thread\":\(threadB)}".utf8)
    }

    var messageBPage: Data {
        let json = "{\"results\":[\(messageB)],\"page_info\":{\"has_next_page\":false,"
            + "\"start_cursor\":null,\"end_cursor\":null}}"
        return Data(json.utf8)
    }

    var threadB: String {
        """
        {
            "id":"thread-b","support_contact_id":"contact-1","subject":"Subject B",
            "conversation_id":null,"created_at":"2026-01-01T00:00:00Z",
            "updated_at":"2026-01-01T00:00:00Z","assigned_at":null,"assigned_to_id":null,
            "resolved_at":null,"resolved_by_id":null,"status":"open","contact_user_id":null
        }
        """
    }

    var messageB: String {
        """
        {
            "id":"message-b","support_thread_id":"thread-b","direction":"outbound",
            "body_text":"Body B","body_html":"","created_at":"2026-01-01T00:00:00Z",
            "created_by_id":null,"updated_at":"2026-01-01T00:00:00Z","email_message_id":null,
            "email_subject":null,"email_from":null,"email_to":null,"drafted_at":null,
            "edited_at":null,"edited_by_id":null,"approved_at":null,"approved_by_id":null,
            "sent_at":null
        }
        """
    }
}
