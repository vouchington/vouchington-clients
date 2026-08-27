@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class StaffSupportViewModelConcurrencyTests: NativeRouteSurfaceViewModelTestCase {
    func testRetryRetainsFailedDeepLinkedThreadAndContactSelection() async throws {
        let threadRoute = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/support/threads/thread-1"))
        CannedFeedURLProtocol.handlers["/api/v1/support/threads"] = (threadPage([]), 200)
        CannedFeedURLProtocol.queuedHandlers["/api/v1/support/threads/thread-1"] = [
            (Data("{}".utf8), 500, 0), (threadResponse("thread-1"), 200, 0)
        ]
        CannedFeedURLProtocol.handlers["/api/v1/support/threads/thread-1/messages"] = (
            messagePage(["message-1"], threadId: "thread-1"),
            200
        )
        let threadModel = try StaffSupportViewModel(
            client: makeClient(),
            administratorId: nil,
            mode: .threads,
            routeMatch: threadRoute.match
        )

        await threadModel.load()
        XCTAssertNil(threadModel.selectedThread)
        XCTAssertNotNil(threadModel.errorMessage)
        await threadModel.retry()
        XCTAssertEqual(threadModel.selectedThread?.id, "thread-1")

        let contactRoute = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/support/contacts/contact-1"))
        CannedFeedURLProtocol.handlers["/api/v1/support/contacts"] = (contactPage([]), 200)
        CannedFeedURLProtocol.queuedHandlers["/api/v1/support/contacts/contact-1"] = [
            (Data("{}".utf8), 500, 0), (contactDetail("contact-1", threads: ["thread-1"]), 200, 0)
        ]
        let contactModel = try StaffSupportViewModel(
            client: makeClient(),
            administratorId: nil,
            mode: .contacts,
            routeMatch: contactRoute.match
        )

        await contactModel.load()
        XCTAssertNil(contactModel.selectedContact)
        XCTAssertNotNil(contactModel.errorMessage)
        await contactModel.retry()
        XCTAssertEqual(contactModel.selectedContact?.id, "contact-1")
    }

    func testSupersededThreadAndContactSelectionsCannotOverwriteCurrentDetail() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/support/threads/thread-a"] = (threadResponse("thread-a"), 200)
        CannedFeedURLProtocol.handlers["/api/v1/support/threads/thread-a/messages"] = (
            messagePage(["message-a"], threadId: "thread-a"),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/support/threads/thread-b"] = (threadResponse("thread-b"), 200)
        CannedFeedURLProtocol.handlers["/api/v1/support/threads/thread-b/messages"] = (
            messagePage(["message-b"], threadId: "thread-b"),
            200
        )
        CannedFeedURLProtocol.suspendResponse(path: "/api/v1/support/threads/thread-a")
        let threadModel = try StaffSupportViewModel(
            client: makeClient(),
            administratorId: nil,
            mode: .threads,
            routeMatch: nil
        )
        let firstThread = Task { await threadModel.selectThread("thread-a") }
        _ = try await CannedFeedURLProtocol.requestBarrier(path: "/api/v1/support/threads/thread-a", method: "GET")
            .wait()
        await threadModel.selectThread("thread-b")
        CannedFeedURLProtocol.releaseResponse(path: "/api/v1/support/threads/thread-a")
        _ = await firstThread.value
        XCTAssertEqual(threadModel.selectedThread?.id, "thread-b")
        XCTAssertEqual(threadModel.messages.map(\.id), ["message-b"])

        CannedFeedURLProtocol.handlers["/api/v1/support/contacts/contact-a"] = (
            contactDetail("contact-a", threads: ["thread-a"]),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/support/contacts/contact-b"] = (
            contactDetail("contact-b", threads: ["thread-b"]),
            200
        )
        CannedFeedURLProtocol.suspendResponse(path: "/api/v1/support/contacts/contact-a")
        let contactModel = try StaffSupportViewModel(
            client: makeClient(),
            administratorId: nil,
            mode: .contacts,
            routeMatch: nil
        )
        let firstContact = Task { await contactModel.selectContact("contact-a") }
        _ = try await CannedFeedURLProtocol.requestBarrier(path: "/api/v1/support/contacts/contact-a", method: "GET")
            .wait()
        await contactModel.selectContact("contact-b")
        CannedFeedURLProtocol.releaseResponse(path: "/api/v1/support/contacts/contact-a")
        _ = await firstContact.value
        XCTAssertEqual(contactModel.selectedContact?.id, "contact-b")
        XCTAssertEqual(contactModel.contactThreads.map(\.id), ["thread-b"])
    }

    func testLateReplyAndDraftMutationsCannotUpdateAReplacementSelection() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/support/threads/thread-a"] = (threadResponse("thread-a"), 200)
        CannedFeedURLProtocol.queuedHandlers["/api/v1/support/threads/thread-a/messages"] = [
            (inboundMessagePage(["message-a"], threadId: "thread-a"), 200, 0),
            (messageResponse("reply-a", threadId: "thread-a"), 200, 0)
        ]
        CannedFeedURLProtocol.handlers["/api/v1/support/threads/thread-a/messages"] = (
            inboundMessagePage(["message-a"], threadId: "thread-a"),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/support/threads/thread-b"] = (threadResponse("thread-b"), 200)
        CannedFeedURLProtocol.handlers["/api/v1/support/threads/thread-b/messages"] = (
            messagePage(["message-b"], threadId: "thread-b"),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/support/threads/thread-a/drafts"] = (
            Data("{\"queued\":true}".utf8),
            202
        )
        let model = try StaffSupportViewModel(
            client: makeClient(),
            administratorId: nil,
            mode: .threads,
            routeMatch: nil
        )

        await model.selectThread("thread-a")
        model.replyText = "Reply for A"
        CannedFeedURLProtocol.suspendResponse(path: "/api/v1/support/threads/thread-a/messages")
        let reply = Task { await model.saveOutboundReply() }
        _ = try await CannedFeedURLProtocol.requestBarrier(
            path: "/api/v1/support/threads/thread-a/messages",
            method: "POST"
        ).wait()
        await model.selectThread("thread-b")
        CannedFeedURLProtocol.releaseResponse(path: "/api/v1/support/threads/thread-a/messages")
        await reply.value
        XCTAssertEqual(model.selectedThread?.id, "thread-b")
        XCTAssertEqual(model.messages.map(\.id), ["message-b"])

        await model.selectThread("thread-a")
        CannedFeedURLProtocol.suspendResponse(path: "/api/v1/support/threads/thread-a/drafts")
        let draft = Task { await model.generateDraft() }
        _ = try await CannedFeedURLProtocol.requestBarrier(
            path: "/api/v1/support/threads/thread-a/drafts",
            method: "POST"
        ).wait()
        await model.selectThread("thread-b")
        CannedFeedURLProtocol.releaseResponse(path: "/api/v1/support/threads/thread-a/drafts")
        await draft.value
        XCTAssertEqual(model.selectedThread?.id, "thread-b")
        XCTAssertEqual(model.messages.map(\.id), ["message-b"])
    }

    func testLaterListOperationCannotReceiveStaleSendRecoveryError() async throws {
        let threadPath = "/api/v1/support/threads/thread-a"
        let messagesPath = "\(threadPath)/messages"
        CannedFeedURLProtocol.handlers[threadPath] = (threadResponse("thread-a"), 200)
        CannedFeedURLProtocol.queuedHandlers[messagesPath] = [
            (messagePage(["message-a"], threadId: "thread-a"), 200, 0),
            (messagePage(["message-a"], threadId: "thread-a"), 200, 0)
        ]
        CannedFeedURLProtocol.handlers[messagesPath] = (messagePage(["message-a"], threadId: "thread-a"), 200)
        CannedFeedURLProtocol.handlers["\(messagesPath)/message-a/sends"] = (Data("{}".utf8), 500)
        CannedFeedURLProtocol.handlers["/api/v1/support/threads"] = (threadPage(["listed-thread"]), 200)
        let model = try StaffSupportViewModel(
            client: makeClient(),
            administratorId: nil,
            mode: .threads,
            routeMatch: nil
        )

        await model.selectThread("thread-a")
        let message = try XCTUnwrap(model.messages.first)
        CannedFeedURLProtocol.suspendResponse(path: messagesPath)
        let send = Task { await model.send(message) }
        _ = try await CannedFeedURLProtocol.requestBarrier(path: messagesPath, method: "GET").wait()

        CannedFeedURLProtocol.suspendResponse(path: "/api/v1/support/threads")
        let listRequest = CannedFeedURLProtocol.requestBarrier(path: "/api/v1/support/threads", method: "GET")
        let laterListOperation = Task { await model.reloadList() }
        _ = try await listRequest.wait()
        CannedFeedURLProtocol.releaseResponse(path: messagesPath)
        CannedFeedURLProtocol.releaseResponse(path: "/api/v1/support/threads")
        await send.value
        await laterListOperation.value

        XCTAssertEqual(model.selectedThread?.id, "thread-a")
        XCTAssertNil(model.errorMessage)
    }

    func testLatestThreadSearchAndFilterReloadWinsOverAnActiveRequest() async throws {
        let path = "/api/v1/support/threads"
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (threadPage(["stale-thread"]), 200, 0),
            (threadPage(["latest-thread"]), 200, 0)
        ]
        CannedFeedURLProtocol.suspendResponse(path: path)
        let model = try StaffSupportViewModel(
            client: makeClient(), administratorId: nil, mode: .threads, routeMatch: nil
        )

        model.query = "stale"
        let firstRequest = CannedFeedURLProtocol.requestBarrier(path: path, method: "GET")
        let staleReload = Task { await model.reloadList() }
        _ = try await firstRequest.wait()

        model.query = "latest"
        model.status = .resolved
        let latestRequest = CannedFeedURLProtocol.requestBarrier(path: path, method: "GET")
        let latestReload = Task { await model.reloadList() }
        let request = try await latestRequest.wait()
        CannedFeedURLProtocol.releaseResponse(path: path)
        await staleReload.value
        await latestReload.value

        XCTAssertEqual(model.threads.map(\.id), ["latest-thread"])
        XCTAssertEqual(
            URLComponents(url: request.url, resolvingAgainstBaseURL: false)?.queryItems,
            [
                URLQueryItem(name: "limit", value: "30"),
                URLQueryItem(name: "q", value: "latest"),
                URLQueryItem(name: "status", value: "resolved")
            ]
        )
    }

    func testLatestContactSearchReloadWinsOverAnActiveRequest() async throws {
        let path = "/api/v1/support/contacts"
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (contactPage(["stale-contact"]), 200, 0),
            (contactPage(["latest-contact"]), 200, 0)
        ]
        CannedFeedURLProtocol.suspendResponse(path: path)
        let model = try StaffSupportViewModel(
            client: makeClient(), administratorId: nil, mode: .contacts, routeMatch: nil
        )

        model.query = "stale"
        let firstRequest = CannedFeedURLProtocol.requestBarrier(path: path, method: "GET")
        let staleReload = Task { await model.reloadList() }
        _ = try await firstRequest.wait()

        model.query = "latest"
        let latestRequest = CannedFeedURLProtocol.requestBarrier(path: path, method: "GET")
        let latestReload = Task { await model.reloadList() }
        let request = try await latestRequest.wait()
        CannedFeedURLProtocol.releaseResponse(path: path)
        await staleReload.value
        await latestReload.value

        XCTAssertEqual(model.contacts.map(\.id), ["latest-contact"])
        XCTAssertEqual(
            URLComponents(url: request.url, resolvingAgainstBaseURL: false)?.queryItems,
            [
                URLQueryItem(name: "limit", value: "30"),
                URLQueryItem(name: "q", value: "latest")
            ]
        )
    }

    func testInFlightThreadAndContactPaginationCannotAppendAfterListInputsChange() async throws {
        let initialPath = "/api/v1/support/threads"
        CannedFeedURLProtocol.queuedHandlers[initialPath] = [(threadPage(["thread-1"]), 200, 0)]
        CannedFeedURLProtocol.suspendResponse(path: initialPath)
        let initial = try StaffSupportViewModel(
            client: makeClient(), administratorId: nil, mode: .threads, routeMatch: nil
        )
        initial.query = "billing"
        let initialRequest = CannedFeedURLProtocol.requestBarrier(path: initialPath, method: "GET")
        let initialReload = Task { await initial.reloadList() }
        _ = try await initialRequest.wait()
        initial.query = "refund"
        CannedFeedURLProtocol.releaseResponse(path: initialPath)
        await initialReload.value

        XCTAssertTrue(initial.threads.isEmpty)
        XCTAssertNil(initial.threadPageInfo)

        CannedFeedURLProtocol.reset()
        let threadPath = "/api/v1/support/threads"
        CannedFeedURLProtocol.queuedHandlers[threadPath] = [
            (threadPage(["thread-1"], hasNext: true, endCursor: "thread-cursor"), 200, 0),
            (threadPage(["thread-2"]), 200, 0)
        ]
        let threads = try StaffSupportViewModel(
            client: makeClient(), administratorId: nil, mode: .threads, routeMatch: nil
        )
        threads.query = "billing"
        threads.status = .open
        await threads.reloadList()

        CannedFeedURLProtocol.suspendResponse(path: threadPath)
        let threadRequest = CannedFeedURLProtocol.requestBarrier(path: threadPath, method: "GET")
        let threadLoadMore = Task { await threads.loadMoreList() }
        _ = try await threadRequest.wait()
        threads.query = "refund"
        CannedFeedURLProtocol.releaseResponse(path: threadPath)
        await threadLoadMore.value

        XCTAssertEqual(threads.threads.map(\.id), ["thread-1"])
        XCTAssertEqual(threads.threadPageInfo?.endCursor, "thread-cursor")
        XCTAssertFalse(threads.canLoadMoreList)

        CannedFeedURLProtocol.reset()
        let contactPath = "/api/v1/support/contacts"
        CannedFeedURLProtocol.queuedHandlers[contactPath] = [
            (contactPage(["contact-1"], hasNext: true, endCursor: "contact-cursor"), 200, 0),
            (contactPage(["contact-2"]), 200, 0)
        ]
        let contacts = try StaffSupportViewModel(
            client: makeClient(), administratorId: nil, mode: .contacts, routeMatch: nil
        )
        contacts.query = "person"
        await contacts.reloadList()

        CannedFeedURLProtocol.suspendResponse(path: contactPath)
        let contactRequest = CannedFeedURLProtocol.requestBarrier(path: contactPath, method: "GET")
        let contactLoadMore = Task { await contacts.loadMoreList() }
        _ = try await contactRequest.wait()
        contacts.query = "other"
        CannedFeedURLProtocol.releaseResponse(path: contactPath)
        await contactLoadMore.value

        XCTAssertEqual(contacts.contacts.map(\.id), ["contact-1"])
        XCTAssertEqual(contacts.contactPageInfo?.endCursor, "contact-cursor")
        XCTAssertFalse(contacts.canLoadMoreList)
    }

    func testDetailCompletionCannotClearInFlightListLoadingOrEnableStalePagination() async throws {
        let listPath = "/api/v1/support/threads"
        let detailPath = "/api/v1/support/threads/thread-1"
        CannedFeedURLProtocol.handlers[listPath] = (threadPage(["thread-2"]), 200)
        CannedFeedURLProtocol.handlers[detailPath] = (threadResponse("thread-1"), 200)
        CannedFeedURLProtocol.handlers["\(detailPath)/messages"] = (
            messagePage(["message-1"], threadId: "thread-1"), 200
        )
        let model = try StaffSupportViewModel(
            client: makeClient(), administratorId: nil, mode: .threads, routeMatch: nil
        )
        model.threads = [decodeThread("thread-1")]
        model.threadPageInfo = .init(hasNextPage: true, endCursor: "stale-cursor")
        model.threadPageCursorProvenance = .init(query: nil, status: nil)
        CannedFeedURLProtocol.suspendResponse(path: listPath)

        let listRequest = CannedFeedURLProtocol.requestBarrier(path: listPath, method: "GET")
        let listLoad = Task { await model.loadMoreList() }
        _ = try await listRequest.wait()
        await model.selectThread("thread-1")
        await model.loadMoreList()

        XCTAssertTrue(model.isListLoading)
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(listPath), 1)
        CannedFeedURLProtocol.releaseResponse(path: listPath)
        await listLoad.value
    }

    func testCancellingDraftPollingSilentlyStopsAnAmbiguousQueueReconciliation() async throws {
        let threadAPath = "/api/v1/support/threads/thread-a"
        let messagesAPath = "\(threadAPath)/messages"
        CannedFeedURLProtocol.handlers[threadAPath] = (threadResponse("thread-a"), 200)
        CannedFeedURLProtocol.queuedHandlers[messagesAPath] = [
            (inboundMessagePage(["inbound-a"], threadId: "thread-a"), 200, 0),
            (inboundMessagePage(["inbound-a"], threadId: "thread-a"), 200, 0)
        ]
        let model = try StaffSupportViewModel(
            client: makeClient(), administratorId: nil, mode: .threads, routeMatch: nil
        )

        await model.selectThread("thread-a")
        CannedFeedURLProtocol.suspendResponse(path: messagesAPath)
        let pollRequest = CannedFeedURLProtocol.requestBarrier(path: messagesAPath, method: "GET")
        let selection = try XCTUnwrap(model.activeThreadSelection)
        let draft = Task {
            await model.reconcileDraftQueueFailure(
                URLError(.networkConnectionLost),
                selection: selection,
                knownMessageIds: Set(model.messages.map(\.id)),
                errorGeneration: model.errorOperationGeneration
            )
        }
        _ = try await pollRequest.wait()
        model.cancelDraftPolling()
        await draft.value

        XCTAssertEqual(model.selectedThread?.id, "thread-a")
        XCTAssertEqual(CannedFeedURLProtocol.capturedPathCount(messagesAPath), 2)
        XCTAssertNil(model.errorMessage)
    }

    private func threadResponse(_ id: String) -> Data {
        Data("{\"thread\":\(thread(id))}".utf8)
    }

    private func threadPage(_ ids: [String])
        -> Data {
        Data("{\"results\":[\(ids.map(thread).joined(separator: ","))],\"page_info\":\(pageInfo())}".utf8)
    }

    private func threadPage(_ ids: [String], hasNext: Bool, endCursor: String?) -> Data {
        Data(
            "{\"results\":[\(ids.map(thread).joined(separator: ","))],\"page_info\":\(pageInfo(hasNext: hasNext, endCursor: endCursor))}"
                .utf8
        )
    }

    private func contactPage(_ ids: [String])
        -> Data {
        Data("{\"results\":[\(ids.map(contact).joined(separator: ","))],\"page_info\":\(pageInfo())}".utf8)
    }

    private func contactPage(_ ids: [String], hasNext: Bool, endCursor: String?) -> Data {
        Data(
            "{\"results\":[\(ids.map(contact).joined(separator: ","))],\"page_info\":\(pageInfo(hasNext: hasNext, endCursor: endCursor))}"
                .utf8
        )
    }

    private func contactDetail(
        _ id: String,
        threads: [String]
    )
        -> Data {
        Data(
            "{\"contact\":\(contact(id)),\"threads\":[\(threads.map(thread).joined(separator: ","))],\"thread_page_info\":\(pageInfo())}"
                .utf8
        )
    }

    private func messageResponse(
        _ id: String,
        threadId: String
    ) -> Data {
        Data("{\"message\":\(message(id, threadId: threadId))}".utf8)
    }

    private func messagePage(
        _ ids: [String],
        threadId: String
    )
        -> Data {
        Data(
            "{\"results\":[\(ids.map { message($0, threadId: threadId) }.joined(separator: ","))],\"page_info\":\(pageInfo())}"
                .utf8
        )
    }

    private func inboundMessagePage(_ ids: [String], threadId: String) -> Data {
        Data(
            "{\"results\":[\(ids.map { inboundMessage($0, threadId: threadId) }.joined(separator: ","))],\"page_info\":\(pageInfo())}"
                .utf8
        )
    }

    private func pageInfo() -> String {
        "{\"has_next_page\":false,\"start_cursor\":null,\"end_cursor\":null}"
    }

    private func pageInfo(hasNext: Bool, endCursor: String?) -> String {
        let cursor = endCursor.map { "\"\($0)\"" } ?? "null"
        return "{\"has_next_page\":\(hasNext),\"start_cursor\":null,\"end_cursor\":\(cursor)}"
    }

    private func contact(_ id: String)
        -> String {
        "{\"id\":\"\(id)\",\"email_address\":\"\(id)@example.test\",\"name\":\"Contact\",\"user_id\":null,\"notes\":\"\",\"created_at\":\"2026-01-01T00:00:00Z\",\"updated_at\":\"2026-01-01T00:00:00Z\"}"
    }

    private func thread(_ id: String)
        -> String {
        "{\"id\":\"\(id)\",\"support_contact_id\":\"contact-1\",\"subject\":\"Subject \(id)\",\"conversation_id\":null,\"created_at\":\"2026-01-01T00:00:00Z\",\"updated_at\":\"2026-01-01T00:00:00Z\",\"assigned_at\":null,\"assigned_to_id\":null,\"resolved_at\":null,\"resolved_by_id\":null,\"status\":\"open\",\"contact_user_id\":null}"
    }

    private func decodeThread(_ id: String) -> SupportThread {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        decoder.dateDecodingStrategy = .iso8601
        return try! decoder.decode(SupportThread.self, from: Data(thread(id).utf8))
    }

    private func message(
        _ id: String,
        threadId: String
    )
        -> String {
        "{\"id\":\"\(id)\",\"support_thread_id\":\"\(threadId)\",\"direction\":\"outbound\",\"body_text\":\"Body \(id)\",\"body_html\":\"\",\"created_at\":\"2026-01-01T00:00:00Z\",\"created_by_id\":null,\"updated_at\":\"2026-01-01T00:00:00Z\",\"email_message_id\":null,\"email_subject\":null,\"email_from\":null,\"email_to\":null,\"drafted_at\":null,\"edited_at\":null,\"edited_by_id\":null,\"approved_at\":null,\"approved_by_id\":null,\"sent_at\":null}"
    }

    private func inboundMessage(_ id: String, threadId: String) -> String {
        message(id, threadId: threadId).replacingOccurrences(of: "\"outbound\"", with: "\"inbound\"")
    }
}
