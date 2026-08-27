import Foundation
import ViewInspector
import VouchaDesignSystem
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class CRMContactsSurfaceInteractionTests: NativeRouteSurfaceViewModelTestCase {
    func testSurfaceButtonsInvokeViewModelActions() throws {
        let contactId = "00000000-0000-7000-8000-000000000584"
        let viewModel = CRMContactsViewModel(client: nil, routeMatch: nil)
        let contact = makeContact(id: contactId, userId: "user-1")
        viewModel.contacts = [contact]
        viewModel.contactsPageInfo = try CRMContactsTestFixtures.crmContactPageInfo(endCursor: "contact-cursor")
        viewModel.selectedContactId = contactId
        viewModel.selectedContact = contact
        viewModel.editForm.populate(from: contact)
        viewModel.createForm.name = "Bob Builder"
        viewModel.createForm.email = "bob@example.test"
        viewModel.importCsv = "name,email\nBob Builder,bob@example.test\n"
        viewModel.linkedUserId = "user-1"
        viewModel.selectedEmailPageInfo = try CRMContactsTestFixtures.crmMessagePageInfo(endCursor: "email-cursor")
        viewModel.selectedNotePageInfo = try CRMContactsTestFixtures.crmNotePageInfo(endCursor: "note-cursor")
        viewModel.emailSubject = "Warm intro"
        viewModel.emailBodyText = "Hello"
        viewModel.noteBody = "Follow up"
        viewModel.selectedEmails = [makeMessage()]
        viewModel.selectedNotes = [makeNote(contactId: contactId)]

        let sut = CRMContactsSurface(viewModel: viewModel)
        let paginationControls = try sut.inspect().findAll(HybridPaginationControl.self)
        XCTAssertEqual(paginationControls.count, 3)
        XCTAssertNoThrow(try sut.inspect().find(button: "Search").tap())
        XCTAssertNoThrow(try sut.inspect().find(button: "Create contact").tap())
        XCTAssertNoThrow(try sut.inspect().find(button: "Import CSV").tap())
        for paginationControl in paginationControls {
            XCTAssertNoThrow(try paginationControl.actualView().inspect().find(button: "Load more").tap())
        }
        XCTAssertNoThrow(try sut.inspect().find(button: "Save").tap())
        XCTAssertNoThrow(try sut.inspect().find(button: "Archive").tap())
        XCTAssertTrue(viewModel.canShowArchiveConfirmation)
        viewModel.dismissArchiveConfirmation()
        XCTAssertNoThrow(try sut.inspect().find(button: "Link account").tap())
        XCTAssertNoThrow(try sut.inspect().find(button: "Unlink").tap())
        XCTAssertNoThrow(try sut.inspect().find(button: "AI draft").tap())
        XCTAssertNoThrow(try sut.inspect().find(button: "Send email").tap())
        XCTAssertNoThrow(try sut.inspect().find(button: "Add note").tap())
    }

    private func makeContact(id: String, userId: String?) -> CrmContact {
        CrmContact(
            id: id,
            name: "Alice Creator",
            email: "alice@example.test",
            vertical: .creditCards,
            contactType: .influencer,
            source: .manual,
            followerCount: 250_000,
            userId: userId,
            createdById: "user-1",
            createdAt: Date(timeIntervalSince1970: 1_688_212_000),
            updatedAt: Date(timeIntervalSince1970: 1_688_212_000)
        )
    }

    private func makeMessage() -> CrmMessage {
        CrmMessage(
            id: "message-1",
            conversationId: "conversation-1",
            direction: .outbound,
            fromEmail: "admin@voucha.ai",
            toEmail: "alice@example.test",
            subject: "Warm intro",
            bodyText: "Hello",
            emailProvider: .ses,
            createdAt: Date(timeIntervalSince1970: 1_688_212_000),
            updatedAt: Date(timeIntervalSince1970: 1_688_212_000)
        )
    }

    private func makeNote(contactId: String) -> CrmNote {
        CrmNote(
            id: "note-1",
            conversationId: "conversation-1",
            contactId: contactId,
            body: "Follow up",
            createdById: "user-1",
            createdAt: Date(timeIntervalSince1970: 1_688_212_000),
            updatedAt: Date(timeIntervalSince1970: 1_688_212_000)
        )
    }
}
