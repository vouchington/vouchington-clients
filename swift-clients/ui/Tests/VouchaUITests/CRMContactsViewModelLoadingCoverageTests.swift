import Foundation
@testable import VouchaAPI
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class CRMContactsViewModelLoadingCoverageTests: NativeRouteSurfaceViewModelTestCase {
    func testLoadSelectsInitialContactAndPopulatesDetailState() async throws {
        let contactId = "00000000-0000-7000-8000-000000000584"
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/crm/\(contactId)"))
        CannedFeedURLProtocol.handlers["/api/v1/crm/contacts"] = (
            ApiFixtureLoader.data("native.crm.contacts.default"),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/crm/contacts/\(contactId)"] = (
            ApiFixtureLoader.data("native.crm.contact-detail.default"),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/crm/contacts/\(contactId)/emails"] = (
            ApiFixtureLoader.data("native.crm.contact-emails.default"),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/crm/contacts/\(contactId)/notes"] = (
            ApiFixtureLoader.data("native.crm.contact-notes.default"),
            200
        )

        let viewModel = try CRMContactsViewModel(client: makeClient(), routeMatch: route.match)

        await viewModel.load()

        XCTAssertEqual(viewModel.selectedContactId, contactId)
        XCTAssertEqual(viewModel.selectedContact?.id, contactId)
        XCTAssertEqual(viewModel.selectedContact?.name, "Alice Creator")
        XCTAssertEqual(viewModel.selectedEmails.count, 1)
        XCTAssertEqual(viewModel.selectedNotes.count, 1)
        XCTAssertEqual(viewModel.editForm.name, "Alice Creator")
        XCTAssertEqual(viewModel.linkedUserId, "")
    }

    func testLoadReportsDecodeFailuresForListAndDetailRequests() async throws {
        let contactId = "contact-1"
        CannedFeedURLProtocol.handlers["/api/v1/crm/contacts"] = (Data("{".utf8), 200)
        let viewModel = try CRMContactsViewModel(client: makeClient(), routeMatch: nil)

        await viewModel.reloadContacts()

        XCTAssertTrue(uiEnglish(viewModel.listErrorMessage)?.contains("Failed to decode response:") == true)

        CannedFeedURLProtocol.handlers["/api/v1/crm/contacts/\(contactId)"] = (Data("{".utf8), 200)
        CannedFeedURLProtocol.handlers["/api/v1/crm/contacts/\(contactId)/emails"] = (
            CRMContactsTestFixtures.crmContactsPage(ids: [], endCursor: nil, hasNextPage: false),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/crm/contacts/\(contactId)/notes"] = (
            CRMContactsTestFixtures.crmContactsPage(ids: [], endCursor: nil, hasNextPage: false),
            200
        )

        await viewModel.selectContact(id: contactId)

        XCTAssertTrue(uiEnglish(viewModel.detailErrorMessage)?.contains("Failed to decode response:") == true)
    }

    func testLoadMoreMethodsAreNoOpsInPristinePaginationState() async throws {
        let viewModel = try CRMContactsViewModel(client: makeClient(), routeMatch: nil)
        viewModel.selectedContactId = "contact-1"

        await viewModel.loadMoreContacts()
        await viewModel.loadMoreEmails()
        await viewModel.loadMoreNotes()

        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.isEmpty)
    }

    func testLoadMoreContactsRetriesAnInitialFailureWithoutACursor() async throws {
        let path = "/api/v1/crm/contacts"
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (Data("{}".utf8), 500, 0),
            (CRMContactsTestFixtures.crmContactsPage(
                ids: ["contact-retry"],
                endCursor: nil,
                hasNextPage: false
            ), 200, 0)
        ]
        let viewModel = try CRMContactsViewModel(client: makeClient(), routeMatch: nil)

        await viewModel.reloadContacts()
        XCTAssertNotNil(viewModel.contactsPagination.lastError)

        await viewModel.loadMoreContacts()

        XCTAssertEqual(viewModel.contacts.map(\.id), ["contact-retry"])
        XCTAssertNil(viewModel.contactsPagination.lastError)
        let requests = CannedFeedURLProtocol.capturedURLs.filter { $0.path == path }
        XCTAssertEqual(requests.count, 2)
        XCTAssertNil(try queryItem(named: "after", in: XCTUnwrap(requests.last)))
    }

    func testLoadMoreEmailAndNoteRetriesAfterInitialDetailFailure() async throws {
        let contactId = "00000000-0000-7000-8000-000000000584"
        let detailPath = "/api/v1/crm/contacts/\(contactId)"
        let emailPath = "\(detailPath)/emails"
        let notePath = "\(detailPath)/notes"
        CannedFeedURLProtocol.handlers[detailPath] = (
            ApiFixtureLoader.data("native.crm.contact-detail.default"),
            200
        )
        CannedFeedURLProtocol.queuedHandlers[emailPath] = [
            (Data("{}".utf8), 500, 0),
            (ApiFixtureLoader.data("native.crm.contact-emails.default"), 200, 0)
        ]
        CannedFeedURLProtocol.queuedHandlers[notePath] = [
            (Data("{}".utf8), 500, 0),
            (ApiFixtureLoader.data("native.crm.contact-notes.default"), 200, 0)
        ]
        let viewModel = try CRMContactsViewModel(client: makeClient(), routeMatch: nil)

        await viewModel.selectContact(id: contactId)
        XCTAssertNotNil(viewModel.emailPagination.lastError)
        XCTAssertNotNil(viewModel.notePagination.lastError)

        await viewModel.loadMoreEmails()
        await viewModel.loadMoreNotes()

        XCTAssertEqual(viewModel.selectedEmails.count, 1)
        XCTAssertEqual(viewModel.selectedNotes.count, 1)
        XCTAssertNil(viewModel.emailPagination.lastError)
        XCTAssertNil(viewModel.notePagination.lastError)
        let emailRequests = CannedFeedURLProtocol.capturedURLs.filter { $0.path == emailPath }
        let noteRequests = CannedFeedURLProtocol.capturedURLs.filter { $0.path == notePath }
        XCTAssertEqual(emailRequests.count, 2)
        XCTAssertEqual(noteRequests.count, 2)
        XCTAssertNil(try queryItem(named: "after", in: XCTUnwrap(emailRequests.last)))
        XCTAssertNil(try queryItem(named: "after", in: XCTUnwrap(noteRequests.last)))
    }

    private func queryItem(named name: String, in url: URL) -> URLQueryItem? {
        URLComponents(url: url, resolvingAgainstBaseURL: false)?.queryItems?.first { $0.name == name }
    }
}
