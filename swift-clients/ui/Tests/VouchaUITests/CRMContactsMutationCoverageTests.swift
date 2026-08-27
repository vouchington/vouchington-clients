import Foundation
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class CRMContactsMutationCoverageTests: NativeRouteSurfaceViewModelTestCase {
    func testContactMutationsUpdateLocalState() async throws {
        let contactId = "00000000-0000-7000-8000-000000000584"
        CannedFeedURLProtocol.queuedHandlers["/api/v1/crm/contacts/\(contactId)"] = [
            (ApiFixtureLoader.data("native.crm.contact-update.default"), 200, 0),
            (Data("{}".utf8), 200, 0)
        ]
        CannedFeedURLProtocol.queuedHandlers["/api/v1/crm/contacts/\(contactId)/user-link"] = [
            (ApiFixtureLoader.data("native.crm.contact-link-user.default"), 200, 0),
            (ApiFixtureLoader.data("native.crm.contact-unlink-user.default"), 200, 0)
        ]

        let viewModel = try CRMContactsViewModel(client: makeClient(), routeMatch: nil)
        let contact = makeContact(id: contactId, userId: nil)
        viewModel.contacts = [contact]
        viewModel.selectedContactId = contactId
        viewModel.selectedContact = contact
        viewModel.editForm.populate(from: contact)
        viewModel.editForm.name = "Alice Updated"
        viewModel.editForm.email = "alice.updated@example.test"
        viewModel.editForm.phone = "+1-415-555-0100"
        viewModel.editForm.vertical = .creditCards
        viewModel.editForm.followerCountText = "255000"
        viewModel.editForm.notes = "Updated outreach notes"

        await viewModel.saveSelectedContact()
        XCTAssertEqual(viewModel.selectedContact?.id, contactId)
        XCTAssertEqual(viewModel.contacts.first?.name, "Alice Creator")
        XCTAssertNil(viewModel.detailErrorMessage)

        viewModel.linkedUserId = "00000000-0000-7000-8000-000000000001"
        await viewModel.linkSelectedContactToUser()
        XCTAssertEqual(viewModel.selectedContact?.userId, "00000000-0000-7000-8000-000000000001")

        await viewModel.unlinkSelectedContactFromUser()
        XCTAssertEqual(viewModel.linkedUserId, "")

        viewModel.canShowArchiveConfirmation = true
        await viewModel.archiveSelectedContact()
        XCTAssertTrue(viewModel.contacts.isEmpty)
        XCTAssertNil(viewModel.selectedContact)
        XCTAssertNil(viewModel.selectedContactId)
        XCTAssertFalse(viewModel.canShowArchiveConfirmation)
    }

    func testEmailNoteImportAndPaginationMutationsUpdateLocalState() async throws {
        let contactId = "00000000-0000-7000-8000-000000000584"
        CannedFeedURLProtocol.handlers["/api/v1/crm/contacts/\(contactId)/email-drafts"] = (
            ApiFixtureLoader.data("native.crm.contact-email-draft.default"),
            200
        )
        CannedFeedURLProtocol.queuedHandlers["/api/v1/crm/contacts/\(contactId)/emails"] = [
            (ApiFixtureLoader.data("native.crm.contact-email-send.default"), 200, 0),
            (ApiFixtureLoader.data("native.crm.contact-emails.default"), 200, 0)
        ]
        CannedFeedURLProtocol.queuedHandlers["/api/v1/crm/contacts/\(contactId)/notes"] = [
            (ApiFixtureLoader.data("native.crm.contact-note-create.default"), 200, 0),
            (ApiFixtureLoader.data("native.crm.contact-notes.default"), 200, 0)
        ]
        CannedFeedURLProtocol
            .handlers["/api/v1/crm/contacts/\(contactId)/notes/00000000-0000-7000-8000-000000000901"] = (
                Data("{}".utf8),
                200
            )
        CannedFeedURLProtocol.handlers["/api/v1/imports/crm-contacts"] = (
            ApiFixtureLoader.data("native.crm.import.success.default"),
            201
        )
        CannedFeedURLProtocol.handlers["/api/v1/crm/contacts"] = (
            ApiFixtureLoader.data("native.crm.contacts.default"),
            200
        )

        let viewModel = try CRMContactsViewModel(client: makeClient(), routeMatch: nil)
        viewModel.importSessionFactory = { self.makeImportSession() }
        viewModel.selectedContactId = contactId
        viewModel.selectedEmailPageInfo = try CRMContactsTestFixtures.crmMessagePageInfo(endCursor: "email-cursor")
        viewModel.selectedNotePageInfo = try CRMContactsTestFixtures.crmNotePageInfo(endCursor: "note-cursor")

        viewModel.emailDraftPrompt = "Focus on travel"
        viewModel.emailDraftTone = "friendly"
        await viewModel.generateEmailDraft()
        XCTAssertEqual(viewModel.emailSubject, "Warm intro")
        XCTAssertEqual(viewModel.emailBodyText, "Hi Alice, it was great to see your recent creator work.")

        viewModel.emailSubject = "Warm intro"
        viewModel.emailBodyText = "Hello there"
        viewModel.emailCtaUrl = "https://example.test"
        viewModel.emailDraftPrompt = "Use a warm tone"
        await viewModel.sendEmail()
        XCTAssertEqual(viewModel.selectedEmails.first?.subject, "Warm intro")
        XCTAssertEqual(viewModel.emailSubject, "")
        XCTAssertEqual(viewModel.emailCtaUrl, "")
        let sendBody = try XCTUnwrap(capturedBody(for: "/api/v1/crm/contacts/\(contactId)/emails"))
        XCTAssertTrue(sendBody.contains("ai_generated_at"))

        viewModel.noteBody = "Follow up after the conference."
        await viewModel.createNote()
        XCTAssertEqual(viewModel.selectedNotes.first?.id, "00000000-0000-7000-8000-000000000901")
        await viewModel.deleteNote(noteId: "00000000-0000-7000-8000-000000000901")
        XCTAssertTrue(viewModel.selectedNotes.isEmpty)

        await viewModel.loadMoreEmails()
        await viewModel.loadMoreNotes()
        XCTAssertFalse(viewModel.selectedEmails.isEmpty)
        XCTAssertFalse(viewModel.selectedNotes.isEmpty)

        viewModel.importCsv = "name,email\nAlice,alice@example.test\n"
        await viewModel.importContacts()
        XCTAssertEqual(viewModel.importCsv, "name,email\n")
        XCTAssertEqual(viewModel.contacts.first?.id, contactId)
    }

    func testConcurrentLoadMoreContactsDoesNotDuplicatePages() async throws {
        let viewModel = try CRMContactsViewModel(client: makeClient(), routeMatch: nil)
        viewModel.contacts = [makeContact(id: "contact-1", userId: nil)]
        viewModel.contactsPageInfo = try CRMContactsTestFixtures.crmContactPageInfo(endCursor: "cursor-1")

        CannedFeedURLProtocol.queuedHandlers["/api/v1/crm/contacts"] = [
            (CRMContactsTestFixtures.crmContactsPage(ids: ["contact-2"], endCursor: nil, hasNextPage: false), 200, 0.2)
        ]

        let firstLoad = Task { await viewModel.loadMoreContacts() }
        try await waitForCapturedPath("/api/v1/crm/contacts", count: 1)
        let secondLoad = Task { await viewModel.loadMoreContacts() }
        await firstLoad.value
        await secondLoad.value

        XCTAssertEqual(
            CannedFeedURLProtocol.capturedURLs.filter { $0.path == "/api/v1/crm/contacts" }.count,
            1
        )
        XCTAssertEqual(viewModel.contacts.map(\.id), ["contact-1", "contact-2"])
        XCTAssertFalse(viewModel.canLoadMoreContacts)
    }

    func testConcurrentLoadMoreEmailsDoesNotDuplicatePages() async throws {
        let contactId = "00000000-0000-7000-8000-000000000584"
        let viewModel = try CRMContactsViewModel(client: makeClient(), routeMatch: nil)
        viewModel.selectedContactId = contactId
        viewModel.selectedEmails = [message(id: "message-1", conversationId: "conversation-1", subject: "Existing")]
        viewModel.selectedEmailPageInfo = try CRMContactsTestFixtures.crmMessagePageInfo(endCursor: "email-cursor")
        CannedFeedURLProtocol.queuedHandlers["/api/v1/crm/contacts/\(contactId)/emails"] = [
            (CRMContactsTestFixtures.crmMessagesPage(ids: ["message-2"], endCursor: nil, hasNextPage: false), 200, 0.2)
        ]

        let firstLoad = Task { await viewModel.loadMoreEmails() }
        try await waitForCapturedPath("/api/v1/crm/contacts/\(contactId)/emails", count: 1)
        let secondLoad = Task { await viewModel.loadMoreEmails() }
        await firstLoad.value
        await secondLoad.value

        XCTAssertEqual(
            CannedFeedURLProtocol.capturedURLs.filter { $0.path == "/api/v1/crm/contacts/\(contactId)/emails" }.count,
            1
        )
        XCTAssertEqual(viewModel.selectedEmails.map(\.id), ["message-1", "message-2"])
        XCTAssertFalse(viewModel.canLoadMoreEmails)
    }

    func testSendEmailIgnoresStaleCompletionAfterContactSwitch() async throws {
        let contactId = "00000000-0000-7000-8000-000000000584"
        CannedFeedURLProtocol.queuedHandlers["/api/v1/crm/contacts/\(contactId)/emails"] = [
            (ApiFixtureLoader.data("native.crm.contact-email-send.default"), 200, 0.2)
        ]

        let viewModel = try CRMContactsViewModel(client: makeClient(), routeMatch: nil)
        viewModel.selectedContactId = contactId
        viewModel.selectedEmails = [
            message(id: "message-old", conversationId: "conversation-2", subject: "Existing reply")
        ]
        viewModel.emailSubject = "Warm intro"
        viewModel.emailBodyText = "Hello there"

        let sendTask = Task { await viewModel.sendEmail() }
        try await waitForCapturedPath("/api/v1/crm/contacts/\(contactId)/emails", count: 1)
        viewModel.selectedContactId = "contact-2"
        viewModel.selectedEmails = [
            message(id: "message-new", conversationId: "conversation-3", subject: "Contact 2 thread")
        ]
        viewModel.emailSubject = "Contact 2 subject"
        viewModel.emailBodyText = "Contact 2 body"

        await sendTask.value

        XCTAssertEqual(viewModel.selectedEmails.map(\.id), ["message-new"])
        XCTAssertEqual(viewModel.emailSubject, "Contact 2 subject")
        XCTAssertEqual(viewModel.emailBodyText, "Contact 2 body")
        XCTAssertNil(viewModel.emailErrorMessage)
    }

    func testGenerateDraftIgnoresStaleCompletionAfterContactSwitch() async throws {
        let contactId = "00000000-0000-7000-8000-000000000584"
        CannedFeedURLProtocol.queuedHandlers["/api/v1/crm/contacts/\(contactId)/email-drafts"] = [
            (ApiFixtureLoader.data("native.crm.contact-email-draft.default"), 200, 0.2)
        ]

        let viewModel = try CRMContactsViewModel(client: makeClient(), routeMatch: nil)
        viewModel.selectedContactId = contactId
        viewModel.emailSubject = "Existing subject"

        let draftTask = Task { await viewModel.generateEmailDraft() }
        try await waitForCapturedPath("/api/v1/crm/contacts/\(contactId)/email-drafts", count: 1)
        viewModel.selectedContactId = "contact-2"
        viewModel.emailSubject = "Contact 2 subject"

        await draftTask.value

        XCTAssertEqual(viewModel.emailSubject, "Contact 2 subject")
        XCTAssertNil(viewModel.emailDraftGeneratedAt)
        XCTAssertNil(viewModel.emailErrorMessage)
    }

    func testSaveContactIgnoresStaleCompletionAfterContactSwitch() async throws {
        let contactId = "00000000-0000-7000-8000-000000000584"
        CannedFeedURLProtocol.queuedHandlers["/api/v1/crm/contacts/\(contactId)"] = [
            (ApiFixtureLoader.data("native.crm.contact-update.default"), 200, 0.2)
        ]

        let viewModel = try CRMContactsViewModel(client: makeClient(), routeMatch: nil)
        let contact = makeContact(id: contactId, userId: nil)
        viewModel.contacts = [contact]
        viewModel.selectedContactId = contactId
        viewModel.selectedContact = contact
        viewModel.editForm.populate(from: contact)

        let saveTask = Task { await viewModel.saveSelectedContact() }
        try await waitForCapturedPath("/api/v1/crm/contacts/\(contactId)", count: 1)
        viewModel.selectedContactId = "contact-2"
        viewModel.selectedContact = makeContact(id: "contact-2", userId: nil)
        viewModel.editForm.name = "Contact 2"

        await saveTask.value

        XCTAssertEqual(viewModel.selectedContactId, "contact-2")
        XCTAssertEqual(viewModel.selectedContact?.id, "contact-2")
        XCTAssertEqual(viewModel.editForm.name, "Contact 2")
        XCTAssertNil(viewModel.detailErrorMessage)
    }

    func testImportValidation422FormatsRowMessages() async throws {
        let viewModel = try CRMContactsViewModel(client: makeClient(), routeMatch: nil)
        CannedFeedURLProtocol.handlers["/api/v1/imports/crm-contacts"] = (
            ApiFixtureLoader.data("native.crm.import.validation.default"),
            422
        )
        viewModel.importCsv = "name,email,follower_count\nAlice Creator,alice@example.test,-1\n"

        await viewModel.importContacts()

        XCTAssertEqual(
            uiEnglish(viewModel.importErrorMessage),
            "Row 2: follower_count must be a non-negative integer"
        )
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
            createdById: "00000000-0000-7000-8000-000000000001",
            createdAt: Date(timeIntervalSince1970: 1_688_212_000),
            updatedAt: Date(timeIntervalSince1970: 1_688_212_000)
        )
    }

    private func message(id: String, conversationId: String, subject: String) -> CrmMessage {
        CrmMessage(
            id: id,
            conversationId: conversationId,
            direction: .outbound,
            fromEmail: "admin@voucha.ai",
            toEmail: "alice@example.test",
            subject: subject,
            bodyText: "Body",
            bodyHtml: nil,
            emailProvider: .ses,
            sesMessageId: nil,
            sentAt: Date(timeIntervalSince1970: 1_688_212_400),
            aiPrompt: nil,
            aiGeneratedAt: nil,
            sentById: "00000000-0000-7000-8000-000000000001",
            createdAt: Date(timeIntervalSince1970: 1_688_212_400),
            updatedAt: Date(timeIntervalSince1970: 1_688_212_400)
        )
    }

    private func makeImportSession() -> URLSession {
        let configuration = URLSessionConfiguration.default
        configuration.protocolClasses = [CannedFeedURLProtocol.self]
        return URLSession(configuration: configuration)
    }

    private func waitForCapturedPath(_ path: String, count: Int) async throws {
        for _ in 0 ..< 40 {
            if CannedFeedURLProtocol.capturedURLs.filter({ $0.path == path }).count >= count {
                return
            }
            try await Task.sleep(nanoseconds: 10_000_000)
        }
        XCTFail("Timed out waiting for \(path) request.")
    }

    private func capturedBody(for path: String) -> String? {
        zip(CannedFeedURLProtocol.capturedURLs, CannedFeedURLProtocol.capturedBodies)
            .first { url, _ in url.path == path }?
            .1
    }
}
