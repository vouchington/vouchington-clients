import Foundation
import ViewInspector
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class CRMContactsSurfaceCoverageTests: NativeRouteSurfaceViewModelTestCase {
    func testSidebarRendersLoadedContactsLoadMoreAndErrorStates() throws {
        let viewModel = CRMContactsViewModel(client: nil, routeMatch: nil)
        let contact = CrmContact(
            id: "contact-1",
            name: "Alice Creator",
            email: "alice@example.test",
            vertical: .creditCards,
            followerCount: 250_000,
            userId: "user-1",
            createdAt: Date(timeIntervalSince1970: 1_700_000_000),
            updatedAt: Date(timeIntervalSince1970: 1_700_000_000)
        )
        viewModel.contacts = [contact]
        viewModel.contactsPageInfo = try contactPageInfo(endCursor: "cursor-1", hasNextPage: true)
        viewModel.selectedContactId = contact.id
        viewModel.listErrorMessage = .verbatim("List failed")

        let sut = CRMContactsSurface(viewModel: viewModel)

        XCTAssertEqual(try sut.inspect().find(text: "Alice Creator").string(), "Alice Creator")
        XCTAssertNoThrow(try sut.inspect().find(button: "Load more"))
        XCTAssertEqual(try sut.inspect().find(text: "List failed").string(), "List failed")
    }

    private func contactPageInfo(endCursor: String?, hasNextPage: Bool) throws -> Page<CrmContact>.PageInfo {
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        return try decoder.decode(
            Page<CrmContact>.PageInfo.self,
            from: Data("""
            {
              "has_next_page": \(hasNextPage),
              "end_cursor": \(endCursor.map { "\"\($0)\"" } ?? "null"),
              "start_cursor": null
            }
            """.utf8)
        )
    }
}
