import Foundation
import ViewInspector
@testable import VouchaAPI
@testable import VouchaFeatures
import VouchaLocalization
import VouchaModels
import XCTest

@MainActor
final class CRMContactsSurfaceTests: NativeRouteSurfaceViewModelTestCase {
    func testSidebarRendersCreateImportAndEmptyDetailStates() throws {
        let viewModel = CRMContactsViewModel(client: nil, routeMatch: nil)
        let sut = CRMContactsSurface(viewModel: viewModel)

        XCTAssertNoThrow(try sut.inspect().find(button: "Search"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Create contact"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Import CSV"))
        XCTAssertEqual(
            try sut.inspect().find(text: "Select a contact to edit, email, archive, or add notes.").string(),
            "Select a contact to edit, email, archive, or add notes."
        )
    }

    func testEmailHistoryShowsReceivedTimestampForInboundMessages() throws {
        let receivedAt = Date(timeIntervalSince1970: 1_688_212_500)
        let contact = CrmContact(
            id: "contact-1",
            name: "Alice Creator",
            email: "alice@example.test",
            vertical: .creditCards,
            contactType: .influencer,
            source: .manual,
            followerCount: 250_000,
            notes: "Creator outreach contact",
            createdById: "00000000-0000-7000-8000-000000000001",
            createdAt: Date(timeIntervalSince1970: 1_688_212_000),
            updatedAt: Date(timeIntervalSince1970: 1_688_212_000)
        )
        let viewModel = CRMContactsViewModel(client: nil, routeMatch: nil)
        viewModel.selectedContactId = contact.id
        viewModel.selectedContact = contact
        viewModel.selectedEmails = [CrmMessage(
            id: "message-1",
            conversationId: "conversation-1",
            direction: .inbound,
            fromEmail: "alice@example.test",
            toEmail: "admin@voucha.ai",
            subject: "Re: Warm intro",
            bodyText: "Thanks",
            sentAt: nil,
            receivedAt: receivedAt,
            createdAt: Date(timeIntervalSince1970: 1_688_212_400),
            updatedAt: Date(timeIntervalSince1970: 1_688_212_400)
        )]
        let locale = Locale(identifier: "es")
        let timeZone = try XCTUnwrap(TimeZone(secondsFromGMT: 0))
        let sut = CRMContactsSurface(viewModel: viewModel)
        let expectedTimestamp = UiMessages.date(
            receivedAt,
            date: .abbreviated,
            time: .shortened,
            locale: locale,
            timeZone: timeZone
        )

        XCTAssertEqual(
            sut.emailTimestamp(viewModel.selectedEmails[0], locale: locale, timeZone: timeZone),
            expectedTimestamp
        )
    }

    func testRouteDetailLoadsContactAndArchiveConfirmationCanBeOpened() async throws {
        let route = try XCTUnwrap(NativeRouteCatalog.matchingRoute(for: "/crm/00000000-0000-7000-8000-000000000584"))
        CannedFeedURLProtocol.handlers["/api/v1/crm/contacts"] = (
            ApiFixtureLoader.data("native.crm.contacts.default"),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/crm/contacts/00000000-0000-7000-8000-000000000584"] = (
            ApiFixtureLoader.data("native.crm.contact-detail.default"),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/crm/contacts/00000000-0000-7000-8000-000000000584/emails"] = (
            ApiFixtureLoader.data("native.crm.contact-emails.default"),
            200
        )
        CannedFeedURLProtocol.handlers["/api/v1/crm/contacts/00000000-0000-7000-8000-000000000584/notes"] = (
            ApiFixtureLoader.data("native.crm.contact-notes.default"),
            200
        )

        let viewModel = try CRMContactsViewModel(client: makeClient(), routeMatch: route.match)
        let sut = CRMContactsSurface(viewModel: viewModel)
        await viewModel.load()

        for _ in 0 ..< 30 {
            if viewModel.selectedContact?.id == "00000000-0000-7000-8000-000000000584" {
                break
            }
            try await Task.sleep(nanoseconds: 50_000_000)
        }

        XCTAssertEqual(viewModel.selectedContact?.name, "Alice Creator")
        XCTAssertNoThrow(try sut.inspect().find(button: "Save"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Archive"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Send email"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Add note"))

        try sut.inspect().find(button: "Archive").tap()
        XCTAssertTrue(viewModel.canShowArchiveConfirmation)
    }

    func testCreateImportAndDetailActionsUseNativeCRMEndpoints() async throws {
        let contactId = "00000000-0000-7000-8000-000000000584"
        CannedFeedURLProtocol.queuedHandlers["/api/v1/crm/contacts"] = [
            (ApiFixtureLoader.data("native.crm.contacts.default"), 200, 0),
            (ApiFixtureLoader.data("native.crm.contact-create.default"), 200, 0),
            (ApiFixtureLoader.data("native.crm.contacts.default"), 200, 0)
        ]
        CannedFeedURLProtocol.queuedHandlers["/api/v1/crm/contacts/\(contactId)"] = [
            (ApiFixtureLoader.data("native.crm.contact-detail.default"), 200, 0),
            (ApiFixtureLoader.data("native.crm.contact-update.default"), 200, 0)
        ]
        CannedFeedURLProtocol.queuedHandlers["/api/v1/crm/contacts/\(contactId)/user-link"] = [
            (ApiFixtureLoader.data("native.crm.contact-link-user.default"), 200, 0),
            (ApiFixtureLoader.data("native.crm.contact-unlink-user.default"), 200, 0)
        ]
        CannedFeedURLProtocol.handlers["/api/v1/crm/contacts/\(contactId)/email-drafts"] = (
            ApiFixtureLoader.data("native.crm.contact-email-draft.default"),
            200
        )
        CannedFeedURLProtocol.queuedHandlers["/api/v1/crm/contacts/\(contactId)/emails"] = [
            (ApiFixtureLoader.data("native.crm.contact-emails.default"), 200, 0),
            (ApiFixtureLoader.data("native.crm.contact-email-send.default"), 200, 0)
        ]
        CannedFeedURLProtocol.queuedHandlers["/api/v1/crm/contacts/\(contactId)/notes"] = [
            (ApiFixtureLoader.data("native.crm.contact-notes.default"), 200, 0),
            (ApiFixtureLoader.data("native.crm.contact-note-create.default"), 200, 0)
        ]
        CannedFeedURLProtocol.queuedHandlers["/api/v1/imports/crm-contacts"] = [
            (ApiFixtureLoader.data("native.crm.import.success.default"), 201, 0)
        ]

        let contact = CrmContact(
            id: contactId,
            name: "Alice Creator",
            email: "alice@example.test",
            vertical: .creditCards,
            contactType: .influencer,
            source: .manual,
            followerCount: 250_000,
            notes: "Creator outreach contact",
            createdById: "00000000-0000-7000-8000-000000000001",
            createdAt: Date(timeIntervalSince1970: 1_688_212_000),
            updatedAt: Date(timeIntervalSince1970: 1_688_212_000)
        )
        let viewModel = try CRMContactsViewModel(client: makeClient(), routeMatch: nil)
        viewModel.selectedContactId = contact.id
        viewModel.selectedContact = contact
        viewModel.editForm.populate(from: contact)
        viewModel.selectedEmails = [CrmMessage(
            id: "message-1",
            conversationId: "conversation-1",
            direction: .outbound,
            fromEmail: "admin@voucha.ai",
            toEmail: "alice@example.test",
            subject: "Warm intro",
            bodyText: "Hi Alice, great to connect.",
            bodyHtml: nil,
            emailProvider: .ses,
            sesMessageId: "ses-msg-0001",
            sentAt: Date(timeIntervalSince1970: 1_688_212_400),
            aiPrompt: "Write a warm intro",
            aiGeneratedAt: Date(timeIntervalSince1970: 1_688_212_370),
            sentById: "00000000-0000-7000-8000-000000000001",
            createdAt: Date(timeIntervalSince1970: 1_688_212_400),
            updatedAt: Date(timeIntervalSince1970: 1_688_212_400)
        )]
        viewModel.selectedNotes = [CrmNote(
            id: "note-1",
            conversationId: "conversation-1",
            contactId: contact.id,
            body: "Met at the conference",
            createdById: "00000000-0000-7000-8000-000000000001",
            createdAt: Date(timeIntervalSince1970: 1_688_212_460),
            updatedAt: Date(timeIntervalSince1970: 1_688_212_460)
        )]
        viewModel.selectedSocialAccounts = [CrmContactSocialAccount(
            id: "social-1",
            contactId: contact.id,
            platform: .instagram,
            handle: "@alicecreator",
            profileUrl: "https://www.instagram.com/alicecreator",
            followerCount: 250_000,
            followerCountUpdatedAt: Date(timeIntervalSince1970: 1_688_212_300),
            createdAt: Date(timeIntervalSince1970: 1_688_212_000),
            updatedAt: Date(timeIntervalSince1970: 1_688_212_000)
        )]
        let sut = CRMContactsSurface(viewModel: viewModel)

        viewModel.createForm.name = "Bob Builder"
        viewModel.createForm.email = "bob@example.test"
        viewModel.importCsv = "name,email\nBob Builder,bob@example.test\n"
        viewModel.linkedUserId = "00000000-0000-7000-8000-000000000001"
        viewModel.emailDraftPrompt = "Focus on travel"
        viewModel.emailDraftTone = "friendly"
        viewModel.emailSubject = "Warm intro"
        viewModel.emailBodyText = "Hello there"
        viewModel.noteBody = "Follow up after the conference."

        XCTAssertNoThrow(try sut.inspect().find(button: "Create contact"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Import CSV"))
        XCTAssertNoThrow(try sut.inspect().find(button: "AI draft"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Send email"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Add note"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Link account"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Unlink"))
        XCTAssertNoThrow(try sut.inspect().find(button: "Save"))

        await viewModel.createContact()
        viewModel.linkedUserId = "00000000-0000-7000-8000-000000000001"
        viewModel.emailDraftPrompt = "Focus on travel"
        viewModel.emailDraftTone = "friendly"
        await viewModel.generateEmailDraft()
        viewModel.emailSubject = "Warm intro"
        viewModel.emailBodyText = "Hello there"
        await viewModel.sendEmail()
        viewModel.noteBody = "Follow up after the conference."
        await viewModel.createNote()
        await viewModel.linkSelectedContactToUser()
        await viewModel.unlinkSelectedContactFromUser()
        await viewModel.saveSelectedContact()
        viewModel.importCsv = "name,email\nBob Builder,bob@example.test\n"
        await viewModel.importContacts()

        for _ in 0 ..< 30 {
            if CannedFeedURLProtocol.capturedURLs.contains(where: { $0.path == "/api/v1/crm/contacts" }),
               CannedFeedURLProtocol.capturedURLs.contains(where: {
                   $0.path == "/api/v1/imports/crm-contacts"
               }),
               CannedFeedURLProtocol.capturedURLs.contains(where: {
                   $0.path == "/api/v1/crm/contacts/\(contactId)/email-drafts"
               }),
               CannedFeedURLProtocol.capturedURLs.contains(where: {
                   $0.path == "/api/v1/crm/contacts/\(contactId)/emails"
               }),
               CannedFeedURLProtocol.capturedURLs.contains(where: {
                   $0.path == "/api/v1/crm/contacts/\(contactId)/notes"
               }),
               CannedFeedURLProtocol.capturedURLs.contains(where: {
                   $0.path == "/api/v1/crm/contacts/\(contactId)/user-link"
               }) {
                break
            }
            try await Task.sleep(nanoseconds: 50_000_000)
        }

        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.contains { $0.path == "/api/v1/crm/contacts" })
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.contains { $0.path == "/api/v1/imports/crm-contacts" })
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs
            .contains { $0.path == "/api/v1/crm/contacts/\(contactId)/email-drafts" })
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs
            .contains { $0.path == "/api/v1/crm/contacts/\(contactId)/emails" })
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs
            .contains { $0.path == "/api/v1/crm/contacts/\(contactId)/notes" })
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs
            .contains { $0.path == "/api/v1/crm/contacts/\(contactId)/user-link" })
    }

}
