import Foundation
@testable import VouchaAPI
import XCTest

final class EndpointCRMTests: XCTestCase {
    func testContactListEndpointUsesSearchFiltersAndPagination() {
        let endpoint = Endpoint.crmContacts(
            query: "alice",
            status: .inConversation,
            vertical: .creditCards,
            linked: true,
            after: "cursor-1",
            limit: 50
        )

        XCTAssertEqual(endpoint.method, .GET)
        XCTAssertEqual(endpoint.path, "/api/v1/crm/contacts")
        XCTAssertEqual(endpoint.queryItems, [
            URLQueryItem(name: "limit", value: "50"),
            URLQueryItem(name: "q", value: "alice"),
            URLQueryItem(name: "status", value: "in_conversation"),
            URLQueryItem(name: "vertical", value: "credit_cards"),
            URLQueryItem(name: "linked", value: "true"),
            URLQueryItem(name: "after", value: "cursor-1")
        ])
    }

    func testContactMutationAndActionEndpointsUseExpectedRoutes() {
        assertEndpoint(Endpoint.crmContact(contactId: "contact 1"), path: "/api/v1/crm/contacts/contact%201")
        assertEndpoint(
            Endpoint.createCrmContact(name: "Alice", email: "alice@example.test"),
            method: .POST,
            path: "/api/v1/crm/contacts",
            body: ["name": "Alice", "email": "alice@example.test"]
        )
        assertEndpoint(
            Endpoint.updateCrmContact(
                contactId: "contact 1",
                name: "Alice Creator",
                email: "alice@example.test",
                phone: "+1-415-555-0100",
                vertical: .creditCards,
                contactType: .influencer,
                followerCount: 250_000,
                notes: "Creator outreach contact"
            ),
            method: .PATCH,
            path: "/api/v1/crm/contacts/contact%201",
            body: [
                "name": "Alice Creator",
                "email": "alice@example.test",
                "phone": "+1-415-555-0100",
                "vertical": "credit_cards",
                "contact_type": "influencer",
                "follower_count": 250_000,
                "notes": "Creator outreach contact"
            ]
        )
        assertEndpoint(
            Endpoint.archiveCrmContact(contactId: "contact 1"),
            method: .DELETE,
            path: "/api/v1/crm/contacts/contact%201"
        )
        assertEndpoint(
            Endpoint.linkCrmContactToUser(contactId: "contact 1", userId: "user 1"),
            method: .PUT,
            path: "/api/v1/crm/contacts/contact%201/user-link",
            body: ["user_id": "user 1"]
        )
        assertEndpoint(
            Endpoint.unlinkCrmContactFromUser(contactId: "contact 1"),
            method: .DELETE,
            path: "/api/v1/crm/contacts/contact%201/user-link"
        )
    }

    func testContactDetailCollectionEndpointsAndDraftImportUseExpectedRoutes() {
        assertEndpoint(
            Endpoint.crmContactEmails(contactId: "contact 1", after: "cursor 2", limit: 10),
            path: "/api/v1/crm/contacts/contact%201/emails"
        )
        XCTAssertEqual(
            Endpoint.crmContactEmails(contactId: "contact 1", after: "cursor 2", limit: 10).queryItems,
            [
                URLQueryItem(name: "limit", value: "10"),
                URLQueryItem(name: "after", value: "cursor 2")
            ]
        )
        assertEndpoint(
            Endpoint.sendCrmEmail(
                contactId: "contact 1",
                subject: "Warm intro",
                bodyHtml: "<p>Hello</p>",
                bodyText: "Hello",
                emailProvider: .ses,
                ctaUrl: "https://example.com",
                aiPrompt: "Write a warm intro"
            ),
            method: .POST,
            path: "/api/v1/crm/contacts/contact%201/emails",
            body: [
                "subject": "Warm intro",
                "body_html": "<p>Hello</p>",
                "body_text": "Hello",
                "email_provider": "ses",
                "cta_url": "https://example.com",
                "ai_prompt": "Write a warm intro"
            ]
        )
        assertEndpoint(
            Endpoint.crmContactNotes(contactId: "contact 1", after: "cursor 2", limit: 10),
            path: "/api/v1/crm/contacts/contact%201/notes"
        )
        assertEndpoint(
            Endpoint.createCrmNote(contactId: "contact 1", body: "Met at the conference"),
            method: .POST,
            path: "/api/v1/crm/contacts/contact%201/notes",
            body: ["body": "Met at the conference"]
        )
        assertEndpoint(
            Endpoint.deleteCrmNote(contactId: "contact 1", noteId: "note 1"),
            method: .DELETE,
            path: "/api/v1/crm/contacts/contact%201/notes/note%201"
        )
        assertEndpoint(
            Endpoint.crmContactEmailDraft(contactId: "contact 1", prompt: "Focus on travel", tone: "friendly"),
            method: .POST,
            path: "/api/v1/crm/contacts/contact%201/email-drafts",
            body: ["prompt": "Focus on travel", "tone": "friendly"]
        )
        assertEndpoint(
            Endpoint.importCrmContacts(csv: "name,email\nAlice,alice@example.test\n"),
            method: .POST,
            path: "/api/v1/imports/crm-contacts",
            body: ["csv": "name,email\nAlice,alice@example.test\n"]
        )
    }
}
