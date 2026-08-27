import VouchaAPI
@testable import VouchaFeatures
import XCTest

@MainActor
final class StaffSupportViewModelTests: NativeRouteSurfaceViewModelTestCase {
    func testThreadListSearchAndPaginationMergeRows() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/support/threads"] = [
            (threadPage(ids: ["thread-1"], hasNext: true, endCursor: "next"), 200, 0),
            (threadPage(ids: ["thread-1", "thread-2"], hasNext: false, endCursor: nil), 200, 0)
        ]
        let viewModel = try StaffSupportViewModel(
            client: makeClient(), administratorId: "admin-1", mode: .threads, routeMatch: nil
        )
        viewModel.query = " refund "
        viewModel.status = .assigned

        await viewModel.reloadList()
        await viewModel.loadMoreList()

        XCTAssertEqual(viewModel.threads.map(\.id), ["thread-1", "thread-2"])
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.query), [
            "limit=30&q=refund&status=assigned", "limit=30&q=refund&status=assigned&after=next"
        ])
    }

    func testListPaginationRequiresTheInstalledThreadAndContactQueryAndStatus() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/support/threads"] = [
            (threadPage(ids: ["thread-1"], hasNext: true, endCursor: "thread-cursor"), 200, 0),
            (threadPage(ids: ["thread-2"], hasNext: false, endCursor: nil), 200, 0)
        ]
        let threads = try StaffSupportViewModel(
            client: makeClient(), administratorId: nil, mode: .threads, routeMatch: nil
        )
        threads.query = " billing "
        threads.status = .open

        await threads.reloadList()
        threads.query = "refund"
        await threads.loadMoreList()
        XCTAssertFalse(threads.canLoadMoreList)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.count, 1)

        threads.query = "billing"
        threads.status = .assigned
        await threads.loadMoreList()
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.count, 1)

        threads.status = .open
        await threads.loadMoreList()
        XCTAssertEqual(threads.threads.map(\.id), ["thread-1", "thread-2"])
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.query), [
            "limit=30&q=billing&status=open", "limit=30&q=billing&status=open&after=thread-cursor"
        ])

        CannedFeedURLProtocol.reset()
        CannedFeedURLProtocol.queuedHandlers["/api/v1/support/contacts"] = [
            (contactPage(ids: ["contact-1"], hasNext: true, endCursor: "contact-cursor"), 200, 0),
            (contactPage(ids: ["contact-2"], hasNext: false, endCursor: nil), 200, 0)
        ]
        let contacts = try StaffSupportViewModel(
            client: makeClient(), administratorId: nil, mode: .contacts, routeMatch: nil
        )
        contacts.query = " person "

        await contacts.reloadList()
        contacts.query = "other"
        await contacts.loadMoreList()
        XCTAssertFalse(contacts.canLoadMoreList)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.count, 1)

        contacts.query = "person"
        await contacts.loadMoreList()
        XCTAssertEqual(contacts.contacts.map(\.id), ["contact-1", "contact-2"])
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.query), [
            "limit=30&q=person", "limit=30&q=person&after=contact-cursor"
        ])
    }

    func testThreadLifecycleMutationsReconcileTheActiveStatusFilter() async throws {
        let threadPath = "/api/v1/support/threads/thread-1"
        CannedFeedURLProtocol.handlers[threadPath] = (threadResponse(id: "thread-1"), 200)
        CannedFeedURLProtocol.handlers["\(threadPath)/messages"] = (messagePage(
            ids: [], hasNext: false, endCursor: nil
        ), 200)
        let viewModel = try StaffSupportViewModel(
            client: makeClient(), administratorId: "admin-1", mode: .threads, routeMatch: nil
        )

        await viewModel.selectThread("thread-1")
        viewModel.status = .assigned
        viewModel.threads = []
        CannedFeedURLProtocol.handlers[threadPath] = (threadResponse(id: "thread-1", status: "assigned"), 200)
        await viewModel.assignToMe()
        XCTAssertEqual(viewModel.threads.map(\.status), [.assigned])

        CannedFeedURLProtocol.handlers[threadPath] = (threadResponse(id: "thread-1", status: "resolved"), 200)
        await viewModel.setResolved(true)
        XCTAssertTrue(viewModel.threads.isEmpty)

        viewModel.status = .resolved
        let resolvedThread = try XCTUnwrap(viewModel.selectedThread)
        viewModel.threads = [resolvedThread]
        CannedFeedURLProtocol.handlers[threadPath] = (threadResponse(id: "thread-1", status: "open"), 200)
        await viewModel.setResolved(false)
        XCTAssertTrue(viewModel.threads.isEmpty)
        XCTAssertEqual(viewModel.selectedThread?.status, .open)
    }

    func testThreadMutationDoesNotInsertAnAbsentRowWhileSearchIsActive() async throws {
        let threadPath = "/api/v1/support/threads/thread-1"
        CannedFeedURLProtocol.handlers[threadPath] = (threadResponse(id: "thread-1"), 200)
        CannedFeedURLProtocol.handlers["\(threadPath)/messages"] = (messagePage(
            ids: [], hasNext: false, endCursor: nil
        ), 200)
        let viewModel = try StaffSupportViewModel(
            client: makeClient(), administratorId: "admin-1", mode: .threads, routeMatch: nil
        )

        await viewModel.selectThread("thread-1")
        viewModel.query = "billing"
        viewModel.status = .assigned
        viewModel.threads = []
        CannedFeedURLProtocol.handlers[threadPath] = (threadResponse(id: "thread-1", status: "assigned"), 200)
        await viewModel.assignToMe()

        XCTAssertTrue(viewModel.threads.isEmpty)
        XCTAssertEqual(viewModel.selectedThread?.status, .assigned)
    }

    func testThreadSelectionMessagePaginationAndMutationsUpdateState() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/support/threads/thread-1"] = (threadResponse(id: "thread-1"), 200)
        CannedFeedURLProtocol.queuedHandlers["/api/v1/support/threads/thread-1/messages"] = [
            (messagePage(ids: ["message-2"], hasNext: true, endCursor: "older"), 200, 0),
            (messagePage(ids: ["message-1"], hasNext: false, endCursor: nil), 200, 0)
        ]
        CannedFeedURLProtocol.handlers["/api/v1/support/threads/thread-1"] = (
            threadResponse(id: "thread-1", status: "assigned"),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/support/threads/thread-1/messages"] = (
            messageResponse(id: "message-3"),
            200
        )
        let viewModel = try StaffSupportViewModel(
            client: makeClient(), administratorId: "admin-1", mode: .threads, routeMatch: nil
        )

        await viewModel.selectThread("thread-1")
        await viewModel.loadOlderMessages()
        await viewModel.assignToMe()
        await viewModel.setResolved(true)
        viewModel.replyText = " saved reply "
        await viewModel.saveOutboundReply()

        XCTAssertEqual(viewModel.selectedThread?.id, "thread-1")
        XCTAssertEqual(viewModel.messages.map(\.id), ["message-1", "message-2", "message-3"])
        XCTAssertEqual(viewModel.replyText, "")
        XCTAssertTrue(CannedFeedURLProtocol.capturedMethods.contains("PATCH"))
        XCTAssertTrue(CannedFeedURLProtocol.capturedMethods.contains("POST"))
    }

    func testContactListAndDetailPaginationMergeRows() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/support/contacts"] = (
            contactPage(ids: ["contact-1"], hasNext: true, endCursor: "next"),
            200
        )
        CannedFeedURLProtocol.queuedHandlers["/api/v1/support/contacts/contact-1"] = [
            (contactDetail(threadIds: ["thread-1"], hasNext: true, endCursor: "older"), 200, 0),
            (contactDetail(threadIds: ["thread-1", "thread-2"], hasNext: false, endCursor: nil), 200, 0)
        ]
        let viewModel = try StaffSupportViewModel(
            client: makeClient(), administratorId: "admin-1", mode: .contacts, routeMatch: nil
        )

        await viewModel.reloadList()
        await viewModel.selectContact("contact-1")
        await viewModel.selectContact("contact-1", after: "older")

        XCTAssertEqual(viewModel.contacts.map(\.id), ["contact-1"])
        XCTAssertEqual(viewModel.contactThreads.map(\.id), ["thread-1", "thread-2"])
        XCTAssertNil(viewModel.errorMessage)
    }

    func testServerErrorLeavesErrorMessageForRetry() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/support/threads"] = (Data("{}".utf8), 500)
        let viewModel = try StaffSupportViewModel(
            client: makeClient(), administratorId: nil, mode: .threads, routeMatch: nil
        )

        await viewModel.reloadList()

        XCTAssertNotNil(viewModel.errorMessage)
        XCTAssertFalse(viewModel.isLoading)
    }

    func testRetryPrefersFailedFilteredListPaginationOverAnActiveThreadSelection() async throws {
        let listPath = "/api/v1/support/threads"
        let detailPath = "\(listPath)/thread-1"
        CannedFeedURLProtocol.queuedHandlers[listPath] = [
            (threadPage(ids: ["thread-1"], hasNext: true, endCursor: "next"), 200, 0),
            (Data("{}".utf8), 500, 0),
            (threadPage(ids: ["thread-2"], hasNext: false, endCursor: nil), 200, 0)
        ]
        CannedFeedURLProtocol.handlers[detailPath] = (threadResponse(id: "thread-1"), 200)
        CannedFeedURLProtocol.handlers["\(detailPath)/messages"] = (
            messagePage(ids: ["message-1"], hasNext: false, endCursor: nil), 200
        )
        let viewModel = try StaffSupportViewModel(
            client: makeClient(), administratorId: nil, mode: .threads, routeMatch: nil
        )
        viewModel.query = " refund "
        viewModel.status = .open

        await viewModel.reloadList()
        await viewModel.selectThread("thread-1")
        await viewModel.loadMoreList()
        await viewModel.retry()

        XCTAssertEqual(viewModel.threads.map(\.id), ["thread-1", "thread-2"])
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(detailPath), 1)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.filter { $0.path == listPath }.map(\.query), [
            "limit=30&q=refund&status=open", "limit=30&q=refund&status=open&after=next",
            "limit=30&q=refund&status=open&after=next"
        ])
    }

    func testLoadAndRetryFollowInitialThreadAndContactRoutes() async throws {
        let threadRoute = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/support/threads/thread-1"))
        CannedFeedURLProtocol.handlers["/api/v1/support/threads"] = (
            threadPage(ids: [], hasNext: false, endCursor: nil),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/support/threads/thread-1"] = (threadResponse(id: "thread-1"), 200)
        CannedFeedURLProtocol.handlers["/api/v1/support/threads/thread-1/messages"] = (
            messagePage(ids: ["message-1"], hasNext: false, endCursor: nil),
            200
        )
        let threadModel = try StaffSupportViewModel(
            client: makeClient(),
            administratorId: nil,
            mode: .threads,
            routeMatch: threadRoute.match
        )

        await threadModel.load()
        await threadModel.retry()

        XCTAssertEqual(threadModel.selectedThread?.id, "thread-1")

        let contactRoute = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/support/contacts/contact-1"))
        CannedFeedURLProtocol.handlers["/api/v1/support/contacts"] = (
            contactPage(ids: [], hasNext: false, endCursor: nil),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/support/contacts/contact-1"] = (
            contactDetail(threadIds: ["thread-1"], hasNext: false, endCursor: nil),
            200
        )
        let contactModel = try StaffSupportViewModel(
            client: makeClient(),
            administratorId: nil,
            mode: .contacts,
            routeMatch: contactRoute.match
        )

        await contactModel.load()
        await contactModel.retry()

        XCTAssertEqual(contactModel.selectedContact?.id, "contact-1")
    }

    func testDraftLifecycleMutationsAndImmediateGeneratedDraft() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/support/threads/thread-1"] = (threadResponse(id: "thread-1"), 200)
        CannedFeedURLProtocol.queuedHandlers["/api/v1/support/threads/thread-1/messages"] = [
            (messagePage(ids: ["inbound", "draft-1"], hasNext: true, endCursor: "older", drafted: true), 200, 0),
            (messagePage(ids: ["older-1"], hasNext: false, endCursor: nil), 200, 0),
            (messagePage(ids: ["draft-1", "draft-2"], hasNext: true, endCursor: "newer", drafted: true), 200, 0)
        ]
        CannedFeedURLProtocol.handlers["/api/v1/support/threads/thread-1/messages/draft-1"] = (
            messageResponse(id: "draft-1", drafted: true), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/support/threads/thread-1/messages/draft-1/approvals"] = (
            messageResponse(id: "draft-1", drafted: true, approved: true), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/support/threads/thread-1/messages/draft-1/sends"] = (
            messageResponse(id: "draft-1", drafted: true, approved: true, sent: true), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/support/threads/thread-1/drafts"] = (
            Data("{\"queued\":true}".utf8),
            202
        )
        let viewModel = try StaffSupportViewModel(
            client: makeClient(),
            administratorId: nil,
            mode: .threads,
            routeMatch: nil
        )

        await viewModel.selectThread("thread-1")
        await viewModel.loadOlderMessages()
        let draft = try XCTUnwrap(viewModel.messages.first { $0.id == "draft-1" })
        viewModel.setDraftText("Edited", messageId: draft.id)
        await viewModel.saveDraft(draft)
        await viewModel.approve(draft)
        await viewModel.send(draft)
        await viewModel.generateDraft()

        XCTAssertEqual(viewModel.draftText(for: draft), "Edited")
        XCTAssertEqual(viewModel.messages.map(\.id), ["older-1", "inbound", "draft-1", "draft-2"])
        XCTAssertFalse(viewModel.messagePageInfo?.hasNextPage ?? true)
        XCTAssertNil(viewModel.messagePageInfo?.endCursor)
        XCTAssertFalse(viewModel.isWaitingForDraft)
        XCTAssertNil(viewModel.errorMessage)
    }

    func testGenerateDraftAllowsUnloadedOlderInboundContext() async throws {
        let threadPath = "/api/v1/support/threads/thread-1"
        let messagesPath = "\(threadPath)/messages"
        CannedFeedURLProtocol.handlers[threadPath] = (threadResponse(id: "thread-1"), 200)
        CannedFeedURLProtocol.queuedHandlers[messagesPath] = [
            (messagePage(ids: ["outbound"], hasNext: true, endCursor: "older"), 200, 0),
            (messagePage(ids: ["draft-1"], hasNext: false, endCursor: nil, drafted: true), 200, 0)
        ]
        CannedFeedURLProtocol.handlers["\(threadPath)/drafts"] = (Data("{\"queued\":true}".utf8), 202)
        let viewModel = try StaffSupportViewModel(
            client: makeClient(), administratorId: nil, mode: .threads, routeMatch: nil
        )

        await viewModel.selectThread("thread-1")
        await viewModel.generateDraft()

        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.contains { $0.path == "\(threadPath)/drafts" })
        XCTAssertEqual(viewModel.messages.map(\.id), ["outbound", "draft-1"])
    }

    func testDirtyDraftCannotBeApprovedUntilItsDisplayedTextIsSaved() async throws {
        let threadPath = "/api/v1/support/threads/thread-1"
        CannedFeedURLProtocol.handlers[threadPath] = (threadResponse(id: "thread-1", status: "assigned"), 200)
        CannedFeedURLProtocol.handlers["\(threadPath)/messages"] = (
            messagePage(ids: ["draft-1"], hasNext: false, endCursor: nil, drafted: true), 200
        )
        let viewModel = try StaffSupportViewModel(
            client: makeClient(), administratorId: nil, mode: .threads, routeMatch: nil
        )

        await viewModel.selectThread("thread-1")
        let draft = try XCTUnwrap(viewModel.messages.first)
        viewModel.setDraftText("Unsaved displayed text", messageId: draft.id)
        await viewModel.approve(draft)

        XCTAssertTrue(viewModel.isDraftDirty(draft))
        XCTAssertFalse(CannedFeedURLProtocol.capturedURLs.contains {
            $0.path == "\(threadPath)/messages/draft-1/approvals"
        })
    }

    func testUnsentDraftPreventsAnotherDraftQueueRequest() async throws {
        let threadPath = "/api/v1/support/threads/thread-1"
        CannedFeedURLProtocol.handlers[threadPath] = (threadResponse(id: "thread-1", status: "assigned"), 200)
        CannedFeedURLProtocol.handlers["\(threadPath)/messages"] = (
            messagePage(ids: ["inbound", "draft-1"], hasNext: false, endCursor: nil, drafted: true), 200
        )
        let viewModel = try StaffSupportViewModel(
            client: makeClient(), administratorId: nil, mode: .threads, routeMatch: nil
        )

        await viewModel.selectThread("thread-1")
        await viewModel.generateDraft()

        XCTAssertFalse(viewModel.canGenerateDraft)
        XCTAssertFalse(CannedFeedURLProtocol.capturedURLs.contains { $0.path == "\(threadPath)/drafts" })
    }

    func testNetworkErrorWhileQueueingDraftReconcilesADelayedAuthoritativeDraft() async throws {
        let threadPath = "/api/v1/support/threads/thread-1"
        CannedFeedURLProtocol.handlers[threadPath] = (threadResponse(id: "thread-1", status: "assigned"), 200)
        CannedFeedURLProtocol.queuedHandlers["\(threadPath)/messages"] = [
            (messagePage(ids: ["inbound"], hasNext: false, endCursor: nil), 200, 0),
            (messagePage(ids: ["inbound"], hasNext: false, endCursor: nil), 200, 0),
            (messagePage(ids: ["inbound", "draft-1"], hasNext: false, endCursor: nil, drafted: true), 200, 0)
        ]
        CannedFeedURLProtocol.errors["\(threadPath)/drafts"] = URLError(.networkConnectionLost)
        let viewModel = try StaffSupportViewModel(
            client: makeClient(), administratorId: nil, mode: .threads, routeMatch: nil
        )

        await viewModel.selectThread("thread-1")
        await viewModel.generateDraft()

        XCTAssertEqual(viewModel.messages.map(\.id), ["inbound", "draft-1"])
        XCTAssertNil(viewModel.errorMessage)
    }

    func testResolvedThreadCannotIssueOutboundOrDraftMutations() async throws {
        let threadPath = "/api/v1/support/threads/thread-1"
        CannedFeedURLProtocol.handlers[threadPath] = (threadResponse(id: "thread-1", status: "resolved"), 200)
        CannedFeedURLProtocol.handlers["\(threadPath)/messages"] = (
            messagePage(ids: ["inbound", "draft-1"], hasNext: false, endCursor: nil, drafted: true), 200
        )
        let viewModel = try StaffSupportViewModel(
            client: makeClient(), administratorId: nil, mode: .threads, routeMatch: nil
        )

        await viewModel.selectThread("thread-1")
        let draft = try XCTUnwrap(viewModel.messages.first { $0.id == "draft-1" })
        viewModel.replyText = "Blocked reply"
        await viewModel.saveOutboundReply()
        await viewModel.generateDraft()
        await viewModel.saveDraft(draft)
        await viewModel.approve(draft)
        await viewModel.send(draft)

        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(threadPath), 1)
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount("\(threadPath)/messages"), 1)
    }

    private func threadPage(ids: [String], hasNext: Bool, endCursor: String?) -> Data {
        let rows = ids.map { thread(id: $0) }.joined(separator: ",")
        return Data("{\"results\":[\(rows)],\"page_info\":\(pageInfo(hasNext: hasNext, endCursor: endCursor))}".utf8)
    }

    private func threadResponse(id: String, status: String = "open") -> Data {
        Data("{\"thread\":\(thread(id: id, status: status))}".utf8)
    }

    private func contactPage(ids: [String], hasNext: Bool, endCursor: String?) -> Data {
        let rows = ids
            .map {
                "{\"id\":\"\($0)\",\"email_address\":\"\($0)@example.test\",\"name\":\"Contact\",\"user_id\":null,\"notes\":\"\",\"created_at\":\"2026-01-01T00:00:00Z\",\"updated_at\":\"2026-01-01T00:00:00Z\"}"
            }.joined(separator: ",")
        return Data("{\"results\":[\(rows)],\"page_info\":\(pageInfo(hasNext: hasNext, endCursor: endCursor))}".utf8)
    }

    private func contactDetail(
        contactId: String = "contact-1",
        threadIds: [String],
        hasNext: Bool,
        endCursor: String?
    ) -> Data {
        let threads = threadIds.map { thread(id: $0) }.joined(separator: ",")
        return Data(
            "{\"contact\":{\"id\":\"\(contactId)\",\"email_address\":\"contact@example.test\",\"name\":\"Contact\",\"user_id\":null,\"notes\":\"\",\"created_at\":\"2026-01-01T00:00:00Z\",\"updated_at\":\"2026-01-01T00:00:00Z\"},\"threads\":[\(threads)],\"thread_page_info\":\(pageInfo(hasNext: hasNext, endCursor: endCursor))}"
                .utf8
        )
    }

    private func messagePage(
        ids: [String],
        hasNext: Bool,
        endCursor: String?,
        drafted: Bool = false,
        threadId: String = "thread-1"
    ) -> Data {
        let messages = ids.map { message(id: $0, drafted: drafted, threadId: threadId) }.joined(separator: ",")
        return Data("{\"results\":[\(messages)],\"page_info\":\(pageInfo(hasNext: hasNext, endCursor: endCursor))}"
            .utf8)
    }

    private func messageResponse(
        id: String,
        drafted: Bool = false,
        approved: Bool = false,
        sent: Bool = false,
        threadId: String = "thread-1"
    ) -> Data {
        let responseMessage = message(
            id: id, drafted: drafted, approved: approved, sent: sent, threadId: threadId
        )
        return Data("{\"message\":\(responseMessage)}".utf8)
    }

    private func thread(id: String, status: String = "open") -> String {
        "{\"id\":\"\(id)\",\"support_contact_id\":\"contact-1\",\"subject\":\"Subject \(id)\",\"conversation_id\":null,\"created_at\":\"2026-01-01T00:00:00Z\",\"updated_at\":\"2026-01-01T00:00:00Z\",\"assigned_at\":null,\"assigned_to_id\":null,\"resolved_at\":null,\"resolved_by_id\":null,\"status\":\"\(status)\",\"contact_user_id\":null}"
    }

    private func message(
        id: String,
        drafted: Bool = false,
        approved: Bool = false,
        sent: Bool = false,
        threadId: String = "thread-1"
    ) -> String {
        let direction = id == "inbound" ? "inbound" : "outbound"
        let draftedAt = drafted ? "\"2026-01-01T00:00:00Z\"" : "null"
        let approvedAt = approved ? "\"2026-01-01T00:00:00Z\"" : "null"
        let sentAt = sent ? "\"2026-01-01T00:00:00Z\"" : "null"
        return "{\"id\":\"\(id)\",\"support_thread_id\":\"\(threadId)\",\"direction\":\"\(direction)\",\"body_text\":\"Body \(id)\",\"body_html\":\"\",\"created_at\":\"2026-01-01T00:00:00Z\",\"created_by_id\":null,\"updated_at\":\"2026-01-01T00:00:00Z\",\"email_message_id\":null,\"email_subject\":null,\"email_from\":null,\"email_to\":null,\"drafted_at\":\(draftedAt),\"edited_at\":null,\"edited_by_id\":null,\"approved_at\":\(approvedAt),\"approved_by_id\":null,\"sent_at\":\(sentAt)}"
    }

    private func pageInfo(hasNext: Bool, endCursor: String?) -> String {
        let cursor = endCursor.map { "\"\($0)\"" } ?? "null"
        return "{\"has_next_page\":\(hasNext),\"start_cursor\":null,\"end_cursor\":\(cursor)}"
    }
}
