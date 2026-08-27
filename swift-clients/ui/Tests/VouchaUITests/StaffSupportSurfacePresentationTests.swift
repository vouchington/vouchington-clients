import Foundation
import ViewInspector
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class StaffSupportSurfacePresentationTests: XCTestCase {
    func testThreadDetailRendersDraftLifecycleAndReplyControls() throws {
        let viewModel = StaffSupportViewModel(
            client: nil, administratorId: "admin-1", mode: .threads, routeMatch: nil
        )
        viewModel.selectedThread = thread(id: "thread-1", status: .assigned)
        viewModel.messages = [
            message(id: "inbound", direction: .inbound),
            message(id: "draft", direction: .outbound, drafted: true)
        ]
        viewModel.messagePageInfo = .init(hasNextPage: true, endCursor: "older")
        viewModel.replyText = "Saved reply"
        let surface = StaffSupportSurface(client: nil, administratorId: "admin-1", mode: .threads, routeMatch: nil)
        let inspection = try surface.staffSupportDetail(viewModel).inspect()

        XCTAssertEqual(try inspection.find(text: "Subject thread-1").string(), "Subject thread-1")
        XCTAssertNoThrow(try inspection.find(button: "Load more"))
        XCTAssertThrowsError(try inspection.find(button: "Generate AI draft"))
        XCTAssertNoThrow(try inspection.find(button: "Save"))
        XCTAssertNoThrow(try inspection.find(button: "Approve"))
        XCTAssertNoThrow(try inspection.find(button: "Save outbound reply"))
        XCTAssertNoThrow(try inspection.find(text: "Saves this reply in the thread. It is not emailed."))
        XCTAssertNoThrow(try inspection.find(text: "Customer"))
        XCTAssertNoThrow(try inspection.find(text: "Support team"))
    }

    func testContactDetailAndSidebarRenderPaginationSearchAndErrorStates() throws {
        let viewModel = StaffSupportViewModel(
            client: nil, administratorId: nil, mode: .contacts, routeMatch: nil
        )
        viewModel.contacts = [contact(id: "contact-1")]
        viewModel.contactPageInfo = .init(hasNextPage: true, endCursor: "next")
        viewModel.contactPageCursorProvenance = .init(query: nil, status: nil)
        viewModel.selectedContact = contact(id: "contact-1")
        viewModel.contactThreads = [thread(id: "thread-1", status: .resolved)]
        viewModel.contactThreadPageInfo = .init(hasNextPage: true, endCursor: "older")
        viewModel.errorMessage = .verbatim("Unable to load contacts")
        let surface = StaffSupportSurface(client: nil, administratorId: nil, mode: .contacts, routeMatch: nil)

        var navigatedPath: String?
        let detail = try surface.staffSupportDetail(viewModel, onNavigateToTargetPath: { navigatedPath = $0 }).inspect()
        let sidebar = try surface.staffSupportSidebar(viewModel).inspect()

        XCTAssertEqual(try detail.find(text: "contact-1@example.test").string(), "contact-1@example.test")
        XCTAssertEqual(try detail.find(text: "Subject thread-1").string(), "Subject thread-1")
        XCTAssertNoThrow(try detail.find(button: "Load more"))
        try detail.find(button: "Subject thread-1").tap()
        XCTAssertEqual(navigatedPath, "/support/threads/thread-1")
        XCTAssertNoThrow(try sidebar.find(text: "Search"))
        XCTAssertNoThrow(try sidebar.find(text: "Unable to load contacts"))
        XCTAssertNoThrow(try sidebar.find(button: "Try Again"))
        XCTAssertThrowsError(try sidebar.find(text: "Status"))
    }

    func testApprovedDraftIsReadOnlyAndOffersOnlySend() throws {
        let viewModel = StaffSupportViewModel(
            client: nil, administratorId: "admin-1", mode: .threads, routeMatch: nil
        )
        viewModel.selectedThread = thread(id: "thread-1", status: .assigned)
        viewModel.messages = [message(id: "approved", direction: .outbound, drafted: true, approved: true)]
        let detail = try StaffSupportSurface(
            client: nil, administratorId: "admin-1", mode: .threads, routeMatch: nil
        ).staffSupportDetail(viewModel).inspect()

        XCTAssertNoThrow(try detail.find(text: "Body approved"))
        XCTAssertNoThrow(try detail.find(button: "Send"))
        XCTAssertThrowsError(try detail.find(button: "Save"))
        XCTAssertThrowsError(try detail.find(ViewType.TextField.self))
        XCTAssertThrowsError(try detail.find(button: "Approve"))
    }

    func testDirtyDraftDisablesApprovalUntilTheDisplayedTextMatchesTheServerDraft() throws {
        let viewModel = StaffSupportViewModel(
            client: nil, administratorId: "admin-1", mode: .threads, routeMatch: nil
        )
        viewModel.selectedThread = thread(id: "thread-1", status: .assigned)
        let draft = message(id: "draft", direction: .outbound, drafted: true)
        viewModel.messages = [draft]
        viewModel.setDraftText("Unsaved visible edit", messageId: draft.id)
        let detail = try StaffSupportSurface(
            client: nil, administratorId: "admin-1", mode: .threads, routeMatch: nil
        ).staffSupportDetail(viewModel).inspect()

        XCTAssertTrue(try detail.find(button: "Approve").isDisabled())
    }

    func testSubjectOnlyThreadHidesAiDraftGeneration() throws {
        let viewModel = StaffSupportViewModel(
            client: nil, administratorId: "admin-1", mode: .threads, routeMatch: nil
        )
        viewModel.selectedThread = thread(id: "thread-1", status: .assigned)
        viewModel.messages = [message(id: "outbound", direction: .outbound)]
        let detail = try StaffSupportSurface(
            client: nil, administratorId: "admin-1", mode: .threads, routeMatch: nil
        ).staffSupportDetail(viewModel).inspect()

        XCTAssertThrowsError(try detail.find(button: "Generate AI draft"))
        XCTAssertNoThrow(try detail.find(button: "Save outbound reply"))
    }

    func testUnloadedOlderMessagesKeepAiDraftGenerationAvailable() throws {
        let viewModel = StaffSupportViewModel(
            client: nil, administratorId: "admin-1", mode: .threads, routeMatch: nil
        )
        viewModel.selectedThread = thread(id: "thread-1", status: .assigned)
        viewModel.messages = [message(id: "outbound", direction: .outbound)]
        viewModel.messagePageInfo = .init(hasNextPage: true, endCursor: "older")
        let detail = try StaffSupportSurface(
            client: nil, administratorId: "admin-1", mode: .threads, routeMatch: nil
        ).staffSupportDetail(viewModel).inspect()

        XCTAssertNoThrow(try detail.find(button: "Generate AI draft"))
    }

    func testResolvedAndClosedThreadsHideNewReplyDraftAndAssignmentControls() throws {
        let resolved = StaffSupportViewModel(
            client: nil, administratorId: "admin-1", mode: .threads, routeMatch: nil
        )
        resolved.selectedThread = thread(id: "resolved", status: .resolved)
        resolved.messages = [message(id: "draft", direction: .outbound, drafted: true)]
        let resolvedDetail = try StaffSupportSurface(
            client: nil, administratorId: "admin-1", mode: .threads, routeMatch: nil
        ).staffSupportDetail(resolved).inspect()

        XCTAssertNoThrow(try resolvedDetail.find(button: "Reopen"))
        XCTAssertThrowsError(try resolvedDetail.find(button: "Assign to me"))
        XCTAssertThrowsError(try resolvedDetail.find(button: "Generate AI draft"))
        XCTAssertThrowsError(try resolvedDetail.find(button: "Save outbound reply"))
        XCTAssertThrowsError(try resolvedDetail.find(ViewType.TextField.self))
        XCTAssertThrowsError(try resolvedDetail.find(button: "Save"))
        XCTAssertThrowsError(try resolvedDetail.find(button: "Approve"))

        let closed = StaffSupportViewModel(
            client: nil, administratorId: "admin-1", mode: .threads, routeMatch: nil
        )
        closed.selectedThread = thread(id: "closed", status: .closed)
        let closedDetail = try StaffSupportSurface(
            client: nil, administratorId: "admin-1", mode: .threads, routeMatch: nil
        ).staffSupportDetail(closed).inspect()

        XCTAssertThrowsError(try closedDetail.find(button: "Assign to me"))
        XCTAssertThrowsError(try closedDetail.find(button: "Resolve"))
        XCTAssertThrowsError(try closedDetail.find(button: "Reopen"))
        XCTAssertThrowsError(try closedDetail.find(button: "Generate AI draft"))
        XCTAssertThrowsError(try closedDetail.find(button: "Save outbound reply"))
    }

    func testThreadSidebarIncludesStatusFilterAndAllPresentationStatusValues() throws {
        let viewModel = StaffSupportViewModel(
            client: nil, administratorId: nil, mode: .threads, routeMatch: nil
        )
        viewModel.threads = [thread(id: "thread-1", status: .open)]
        viewModel.threadPageInfo = .init(hasNextPage: true, endCursor: "next")
        viewModel.threadPageCursorProvenance = .init(query: nil, status: nil)
        let surface = StaffSupportSurface(client: nil, administratorId: nil, mode: .threads, routeMatch: nil)

        XCTAssertNoThrow(try surface.staffSupportSidebar(viewModel).inspect().find(text: "Status"))
        XCTAssertEqual(
            [SupportThreadStatus.open, .assigned, .resolved, .closed].map { uiEnglish(supportThreadStatusText($0)) },
            ["Open", "Assigned", "Resolved", "Closed"]
        )
        XCTAssertEqual(
            [StaffSupportThreadStatusFilter.open, .assigned, .resolved]
                .map { uiEnglish(staffSupportThreadStatusFilterText($0)) },
            ["Open", "Assigned", "Resolved"]
        )
    }

    func testRouteDestinationRendersStaffSupportSurfaces() throws {
        let threads = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/support"))
        let contacts = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/support/contacts"))
        let threadSurface = NativeRouteDestinationSurface(
            entry: threads.entry,
            client: nil,
            routeMatch: threads.match,
            routeQuery: nil,
            isSignedIn: true,
            showSignIn: {}
        )
        let contactSurface = NativeRouteDestinationSurface(
            entry: contacts.entry,
            client: nil,
            routeMatch: contacts.match,
            routeQuery: nil,
            isSignedIn: true,
            showSignIn: {}
        )

        XCTAssertNoThrow(try threadSurface.inspect().find(StaffSupportSurface.self))
        XCTAssertNoThrow(try contactSurface.inspect().find(StaffSupportSurface.self))
    }

    private func thread(id: String, status: SupportThreadStatus) -> SupportThread {
        decode(
            SupportThread.self,
            "{\"id\":\"\(id)\",\"support_contact_id\":\"contact\",\"subject\":\"Subject \(id)\",\"conversation_id\":null,\"created_at\":\"2026-01-01T00:00:00Z\",\"updated_at\":\"2026-01-01T00:00:00Z\",\"assigned_at\":null,\"assigned_to_id\":null,\"resolved_at\":null,\"resolved_by_id\":null,\"status\":\"\(status.rawValue)\",\"contact_user_id\":null}"
        )
    }

    private func message(
        id: String,
        direction: SupportMessageDirection,
        drafted: Bool = false,
        approved: Bool = false
    ) -> SupportMessage {
        let draftedAt = drafted ? "\"2026-01-01T00:00:00Z\"" : "null"
        let approvedAt = approved ? "\"2026-01-01T00:00:00Z\"" : "null"
        return decode(
            SupportMessage.self,
            "{\"id\":\"\(id)\",\"support_thread_id\":\"thread-1\",\"direction\":\"\(direction.rawValue)\",\"body_text\":\"Body \(id)\",\"body_html\":\"\",\"created_at\":\"2026-01-01T00:00:00Z\",\"created_by_id\":null,\"updated_at\":\"2026-01-01T00:00:00Z\",\"email_message_id\":null,\"email_subject\":null,\"email_from\":null,\"email_to\":null,\"drafted_at\":\(draftedAt),\"edited_at\":null,\"edited_by_id\":null,\"approved_at\":\(approvedAt),\"approved_by_id\":null,\"sent_at\":null}"
        )
    }

    private func contact(id: String) -> SupportContact {
        decode(
            SupportContact.self,
            "{\"id\":\"\(id)\",\"email_address\":\"\(id)@example.test\",\"name\":\"Contact\",\"user_id\":null,\"notes\":\"\",\"created_at\":\"2026-01-01T00:00:00Z\",\"updated_at\":\"2026-01-01T00:00:00Z\"}"
        )
    }

    private func decode<T: Decodable>(_: T.Type, _ json: String) -> T {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        decoder.dateDecodingStrategy = .iso8601
        return try! decoder.decode(T.self, from: Data(json.utf8))
    }
}
