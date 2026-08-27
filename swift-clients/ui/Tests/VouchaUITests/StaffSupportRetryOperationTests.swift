@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class StaffSupportRetryOperationTests: NativeRouteSurfaceViewModelTestCase {
    func testRetryReloadsCurrentControlsWhenFailedPaginationProvenanceIsStale() async throws {
        let path = "/api/v1/support/threads"
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (Data("{}".utf8), 500, 0),
            (Data("{\"results\":[],\"page_info\":{\"has_next_page\":false,\"end_cursor\":null}}".utf8), 200, 0)
        ]
        let model = try StaffSupportViewModel(
            client: makeClient(), administratorId: nil, mode: .threads, routeMatch: nil
        )
        model.query = "old"
        model.status = .open
        model.threadPageInfo = .init(hasNextPage: true, endCursor: "old-cursor")
        model.threadPageCursorProvenance = .init(query: "old", status: .open)

        await model.loadMoreList()
        model.query = "new"
        model.status = .assigned
        let recoveredDetail = await model.retry()

        XCTAssertFalse(recoveredDetail)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.query), [
            "limit=30&q=old&status=open&after=old-cursor", "limit=30&q=new&status=assigned"
        ])
    }

    func testLaterDetailFailureReplacesAnEarlierListRetryTarget() async throws {
        let listPath = "/api/v1/support/threads"
        let detailPath = "\(listPath)/thread-1"
        CannedFeedURLProtocol.handlers[listPath] = (Data("{}".utf8), 500)
        CannedFeedURLProtocol.handlers[detailPath] = (Data("{}".utf8), 500)
        CannedFeedURLProtocol.handlers["\(detailPath)/messages"] = (Data("{}".utf8), 500)
        let model = try StaffSupportViewModel(
            client: makeClient(), administratorId: nil, mode: .threads, routeMatch: nil
        )

        await model.reloadList()
        await model.selectThread("thread-1")
        await model.retry()

        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(listPath), 1)
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(detailPath), 2)
    }

    func testDetailRetryReportsRecoveryForCompactNavigation() async throws {
        let threadPath = "/api/v1/support/threads/thread-1"
        let messagesPath = "\(threadPath)/messages"
        CannedFeedURLProtocol.handlers[threadPath] = (Data("{}".utf8), 500)
        CannedFeedURLProtocol.handlers[messagesPath] = (Data("{}".utf8), 500)
        let model = try StaffSupportViewModel(
            client: makeClient(), administratorId: nil, mode: .threads, routeMatch: nil
        )

        await model.selectThread("thread-1")
        CannedFeedURLProtocol.handlers[threadPath] = (threadResponse, 200)
        CannedFeedURLProtocol.handlers[messagesPath] = (messagePage, 200)

        let recoveredDetail = await model.retry()
        XCTAssertTrue(recoveredDetail)
        XCTAssertEqual(model.selectedThread?.id, "thread-1")

        CannedFeedURLProtocol.handlers[threadPath] = (Data("{}".utf8), 500)
        CannedFeedURLProtocol.handlers[messagesPath] = (Data("{}".utf8), 500)
        await model.selectThread("thread-1")
        let failedThreadRetry = await model.retry()
        XCTAssertFalse(failedThreadRetry)
    }

    func testExistingContactPaginationFailureDoesNotReportDetailRecovery() async throws {
        let path = "/api/v1/support/contacts/contact-1"
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (contactDetail, 200, 0), (Data("{}".utf8), 500, 0), (Data("{}".utf8), 500, 0)
        ]
        let model = try StaffSupportViewModel(
            client: makeClient(), administratorId: nil, mode: .contacts, routeMatch: nil
        )

        let loadedContact = await model.selectContact("contact-1")
        let loadedContactPage = await model.selectContact("contact-1", after: "cursor")
        let failedContactRetry = await model.retry()
        XCTAssertTrue(loadedContact)
        XCTAssertFalse(loadedContactPage)
        XCTAssertFalse(failedContactRetry)
        XCTAssertEqual(model.selectedContact?.id, "contact-1")
    }

    func testOlderMessageRetryPreservesAccumulatedHistory() async throws {
        let threadPath = "/api/v1/support/threads/thread-1"
        let messagesPath = "\(threadPath)/messages"
        CannedFeedURLProtocol.handlers[threadPath] = (threadResponse, 200)
        CannedFeedURLProtocol.queuedHandlers[messagesPath] = [
            (messagePage(id: "recent", endCursor: "older"), 200, 0),
            (Data("{}".utf8), 500, 0),
            (messagePage(id: "old", endCursor: nil), 200, 0)
        ]
        let model = try StaffSupportViewModel(
            client: makeClient(), administratorId: nil, mode: .threads, routeMatch: nil
        )

        await model.selectThread("thread-1")
        await model.loadOlderMessages()
        let retriedDetail = await model.retry()

        XCTAssertFalse(retriedDetail)
        XCTAssertEqual(model.messages.map(\.id), ["old", "recent"])
        XCTAssertFalse(model.messagePageInfo?.hasNextPage ?? true)
        XCTAssertNil(model.messagePageInfo?.endCursor)
    }

    private var threadResponse: Data {
        Data("{\"thread\":\(thread)}".utf8)
    }

    private var messagePage: Data {
        Data("{\"results\":[],\"page_info\":{\"has_next_page\":false,\"start_cursor\":null,\"end_cursor\":null}}".utf8)
    }

    private func messagePage(id: String, endCursor: String?) -> Data {
        let cursor = endCursor.map { "\"\($0)\"" } ?? "null"
        return Data(
            "{\"results\":[{\"id\":\"\(id)\",\"support_thread_id\":\"thread-1\",\"direction\":\"inbound\",\"body_text\":\"\(id)\",\"body_html\":\"\",\"created_at\":\"2026-01-01T00:00:00Z\",\"created_by_id\":null,\"updated_at\":\"2026-01-01T00:00:00Z\",\"email_message_id\":null,\"email_subject\":null,\"email_from\":null,\"email_to\":null,\"drafted_at\":null,\"edited_at\":null,\"edited_by_id\":null,\"approved_at\":null,\"approved_by_id\":null,\"sent_at\":null}],\"page_info\":{\"has_next_page\":\(endCursor != nil),\"start_cursor\":null,\"end_cursor\":\(cursor)}}"
                .utf8
        )
    }

    private var thread: String {
        """
        {"id":"thread-1","support_contact_id":"contact-1","subject":"Subject","conversation_id":null,"created_at":"2026-01-01T00:00:00Z","updated_at":"2026-01-01T00:00:00Z","assigned_at":null,"assigned_to_id":null,"resolved_at":null,"resolved_by_id":null,"status":"open","contact_user_id":null}
        """
    }

    private var contactDetail: Data {
        Data(
            "{\"contact\":{\"id\":\"contact-1\",\"email_address\":\"contact@example.test\",\"name\":\"Contact\",\"user_id\":null,\"notes\":\"\",\"created_at\":\"2026-01-01T00:00:00Z\",\"updated_at\":\"2026-01-01T00:00:00Z\"},\"threads\":[],\"thread_page_info\":{\"has_next_page\":true,\"start_cursor\":null,\"end_cursor\":\"cursor\"}}"
                .utf8
        )
    }
}
