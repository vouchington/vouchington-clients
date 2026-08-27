import Foundation
@testable import VouchaAPI
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class CRMContactsViewModelMutationCoverageTests: NativeRouteSurfaceViewModelTestCase {
    func testEmailDraftDeleteNoteAndArchiveMutationsUpdateLocalState() async throws {
        let contactId = "contact-1"
        let noteId = "note-1"
        let contact = CrmContact(
            id: contactId,
            name: "Alice Creator",
            email: "alice@example.test",
            createdAt: Date(timeIntervalSince1970: 1_700_000_000),
            updatedAt: Date(timeIntervalSince1970: 1_700_000_000)
        )
        CannedFeedURLProtocol.handlers["/api/v1/crm/contacts/\(contactId)/email-drafts"] = (
            ApiFixtureLoader.data("native.crm.contact-email-draft.default"),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/crm/contacts/\(contactId)/notes/\(noteId)"] = (
            Data("{}".utf8),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/crm/contacts/\(contactId)"] = (
            Data("{}".utf8),
            200
        )

        let viewModel = try CRMContactsViewModel(client: makeClient(), routeMatch: nil)
        viewModel.selectedContactId = contactId
        viewModel.selectedContact = contact
        viewModel.contacts = [contact]
        viewModel.selectedNotes = [
            CrmNote(
                id: noteId,
                conversationId: "conversation-1",
                contactId: contactId,
                body: "Follow up",
                createdById: "user-1",
                createdAt: Date(timeIntervalSince1970: 1_700_000_000),
                updatedAt: Date(timeIntervalSince1970: 1_700_000_000)
            )
        ]
        viewModel.emailDraftPrompt = "Focus on travel"
        viewModel.emailDraftTone = "friendly"

        await viewModel.generateEmailDraft()
        XCTAssertEqual(viewModel.emailSubject, "Warm intro")
        XCTAssertTrue(viewModel.emailBodyText.contains("great to see"))

        await viewModel.deleteNote(noteId: noteId)
        XCTAssertTrue(viewModel.selectedNotes.isEmpty)

        await viewModel.archiveSelectedContact()
        XCTAssertNil(viewModel.selectedContact)
        XCTAssertNil(viewModel.selectedContactId)
        XCTAssertTrue(viewModel.contacts.isEmpty)
    }

    func testValidationGuardsSetErrorsWithoutSendingNetworkRequests() async throws {
        let viewModel = try CRMContactsViewModel(client: makeClient(), routeMatch: nil)

        await viewModel.createContact()
        XCTAssertEqual(uiEnglish(viewModel.createErrorMessage), "Name is required.")

        viewModel.createForm.name = "Alice"
        await viewModel.createContact()
        XCTAssertEqual(uiEnglish(viewModel.createErrorMessage), "Email is required.")

        viewModel.selectedContactId = "contact-1"
        viewModel.editForm.name = "Alice"
        await viewModel.saveSelectedContact()
        XCTAssertEqual(uiEnglish(viewModel.detailErrorMessage), "Email is required.")

        viewModel.emailSubject = " "
        viewModel.emailBodyText = " "
        await viewModel.sendEmail()
        XCTAssertEqual(uiEnglish(viewModel.emailErrorMessage), "Subject is required.")

        viewModel.emailSubject = "Hello"
        await viewModel.sendEmail()
        XCTAssertEqual(uiEnglish(viewModel.emailErrorMessage), "Body is required.")

        await viewModel.createNote()
        XCTAssertEqual(uiEnglish(viewModel.noteErrorMessage), "Note body is required.")

        viewModel.importCsv = "   "
        await viewModel.importContacts()
        XCTAssertEqual(uiEnglish(viewModel.importErrorMessage), "CSV is required.")
    }
}
