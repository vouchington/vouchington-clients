import Foundation
@testable import VouchaAPI
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class CRMContactsViewModelTests: NativeRouteSurfaceViewModelTestCase {
    func testInitDerivesFiltersFromRouteQueryValues() throws {
        let route = try XCTUnwrap(
            NativeRouteCatalog.matchingRoute(for: "/crm?q=alice&status=converted&vertical=credit_cards&linked=true")
        )
        let linkedViewModel = CRMContactsViewModel(client: nil, routeMatch: route.match)

        XCTAssertEqual(linkedViewModel.searchQuery, "alice")
        XCTAssertEqual(linkedViewModel.selectedStatus, .converted)
        XCTAssertEqual(linkedViewModel.selectedVertical, .creditCards)
        XCTAssertEqual(linkedViewModel.selectedLinkedFilter, .linked)

        let unlinkedRoute = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/crm?linked=false"))
        let unlinkedViewModel = CRMContactsViewModel(client: nil, routeMatch: unlinkedRoute.match)
        XCTAssertEqual(unlinkedViewModel.selectedLinkedFilter, .unlinked)

        let defaultRoute = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/crm"))
        let defaultViewModel = CRMContactsViewModel(client: nil, routeMatch: defaultRoute.match)
        XCTAssertEqual(defaultViewModel.selectedLinkedFilter, .all)
    }

    func testLoadContactsUsesRouteFiltersAndAppendsMoreResults() async throws {
        let route = try XCTUnwrap(
            NativeRouteCatalog.matchingRoute(for: "/crm?q=alice&status=new&vertical=credit_cards&linked=true")
        )
        CannedFeedURLProtocol.queuedHandlers["/api/v1/crm/contacts"] = [
            (
                CRMContactsTestFixtures.crmContactsPage(ids: ["contact-1"], endCursor: "cursor-1", hasNextPage: true),
                200,
                0
            ),
            (CRMContactsTestFixtures.crmContactsPage(ids: ["contact-2"], endCursor: nil, hasNextPage: false), 200, 0)
        ]

        let viewModel = try CRMContactsViewModel(client: makeClient(), routeMatch: route.match)

        await viewModel.load()
        await viewModel.loadMoreContacts()

        XCTAssertEqual(viewModel.contacts.map(\.id), ["contact-1", "contact-2"])
        XCTAssertEqual(viewModel.contactsPageInfo?.endCursor, nil)
        XCTAssertFalse(viewModel.canLoadMoreContacts)

        let firstRequest = try XCTUnwrap(CannedFeedURLProtocol.capturedURLs.first)
        let firstQueryItems = try XCTUnwrap(URLComponents(url: firstRequest, resolvingAgainstBaseURL: false)?
            .queryItems)
        XCTAssertEqual(firstQueryItems.first(where: { $0.name == "q" })?.value, "alice")
        XCTAssertEqual(firstQueryItems.first(where: { $0.name == "status" })?.value, "new")
        XCTAssertEqual(firstQueryItems.first(where: { $0.name == "vertical" })?.value, "credit_cards")
        XCTAssertEqual(firstQueryItems.first(where: { $0.name == "linked" })?.value, "true")

        let secondRequest = try XCTUnwrap(CannedFeedURLProtocol.capturedURLs.dropFirst().first)
        let secondQueryItems = try XCTUnwrap(URLComponents(url: secondRequest, resolvingAgainstBaseURL: false)?
            .queryItems)
        XCTAssertEqual(secondQueryItems.first(where: { $0.name == "after" })?.value, "cursor-1")
    }

    func testSelectContactNilResetsDetailState() async throws {
        let viewModel = CRMContactsViewModel(client: nil, routeMatch: nil)
        viewModel.selectedContactId = "contact-1"
        viewModel.selectedContact = CrmContact(
            id: "contact-1",
            name: "Alice",
            email: "alice@example.test",
            createdAt: Date(timeIntervalSince1970: 1_700_000_000),
            updatedAt: Date(timeIntervalSince1970: 1_700_000_000)
        )
        viewModel.selectedSocialAccounts = [
            CrmContactSocialAccount(
                id: "social-1",
                contactId: "contact-1",
                platform: .instagram,
                handle: "@alice",
                createdAt: Date(timeIntervalSince1970: 1_700_000_000),
                updatedAt: Date(timeIntervalSince1970: 1_700_000_000)
            )
        ]
        viewModel.selectedEmails = [CrmMessage(
            id: "message-1",
            conversationId: "conversation-1",
            direction: .outbound,
            fromEmail: "admin@voucha.ai",
            toEmail: "alice@example.test",
            createdAt: Date(timeIntervalSince1970: 1_700_000_000),
            updatedAt: Date(timeIntervalSince1970: 1_700_000_000)
        )]
        viewModel.selectedNotes = [CrmNote(
            id: "note-1",
            conversationId: "conversation-1",
            contactId: "contact-1",
            body: "Note",
            createdById: "user-1",
            createdAt: Date(timeIntervalSince1970: 1_700_000_000),
            updatedAt: Date(timeIntervalSince1970: 1_700_000_000)
        )]
        viewModel.selectedEmailPageInfo = try CRMContactsTestFixtures.crmMessagePageInfo(endCursor: "cursor-1")
        viewModel.selectedNotePageInfo = try CRMContactsTestFixtures.crmNotePageInfo(endCursor: "cursor-1")
        viewModel.detailErrorMessage = .verbatim("err")
        viewModel.emailErrorMessage = .verbatim("err")
        viewModel.noteErrorMessage = .verbatim("err")
        viewModel.canShowArchiveConfirmation = true
        viewModel.editForm.name = "Alice"
        viewModel.linkedUserId = "user-1"

        await viewModel.selectContact(id: nil)

        XCTAssertNil(viewModel.selectedContactId)
        XCTAssertNil(viewModel.selectedContact)
        XCTAssertTrue(viewModel.selectedSocialAccounts.isEmpty)
        XCTAssertTrue(viewModel.selectedEmails.isEmpty)
        XCTAssertTrue(viewModel.selectedNotes.isEmpty)
        XCTAssertNil(viewModel.selectedEmailPageInfo)
        XCTAssertNil(viewModel.selectedNotePageInfo)
        XCTAssertNil(viewModel.detailErrorMessage)
        XCTAssertNil(viewModel.emailErrorMessage)
        XCTAssertNil(viewModel.noteErrorMessage)
        XCTAssertFalse(viewModel.canShowArchiveConfirmation)
        XCTAssertEqual(viewModel.editForm.name, "")
        XCTAssertEqual(viewModel.linkedUserId, "")
    }

    func testSelectContactClearsStaleDetailEditingBeforeNewLoad() async {
        let viewModel = CRMContactsViewModel(client: nil, routeMatch: nil)
        viewModel.selectedContactId = "contact-1"
        viewModel.selectedContact = CrmContact(
            id: "contact-1",
            name: "Alice",
            email: "alice@example.test",
            createdAt: Date(timeIntervalSince1970: 1_700_000_000),
            updatedAt: Date(timeIntervalSince1970: 1_700_000_000)
        )
        viewModel.editForm.name = "Stale name"
        viewModel.editForm.email = "stale@example.test"
        viewModel.linkedUserId = "user-1"

        await viewModel.selectContact(id: "contact-2")

        XCTAssertEqual(viewModel.selectedContactId, "contact-2")
        XCTAssertNil(viewModel.selectedContact)
        XCTAssertEqual(viewModel.editForm.name, "")
        XCTAssertEqual(viewModel.editForm.email, "")
        XCTAssertEqual(viewModel.linkedUserId, "")
    }

    func testCreateAndImportValidationPathsSetMessagesWithoutNetworkCalls() async throws {
        let viewModel = try CRMContactsViewModel(client: makeClient(), routeMatch: nil)

        await viewModel.createContact()
        XCTAssertEqual(uiEnglish(viewModel.createErrorMessage), "Name is required.")

        viewModel.createForm.name = "Alice"
        await viewModel.createContact()
        XCTAssertEqual(uiEnglish(viewModel.createErrorMessage), "Email is required.")

        viewModel.importCsv = "   "
        await viewModel.importContacts()
        XCTAssertEqual(uiEnglish(viewModel.importErrorMessage), "CSV is required.")

        viewModel.selectedContactId = "contact-1"
        viewModel.editForm.name = "   "
        await viewModel.saveSelectedContact()
        XCTAssertEqual(uiEnglish(viewModel.detailErrorMessage), "Name is required.")
    }
}
