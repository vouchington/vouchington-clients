import Foundation
import VouchaAPI

extension EndpointManifestCoverage {
    static let nativeCrmEndpoints: [ManifestRegisteredEndpoint] = [
        ManifestRegisteredEndpoint(id: "native.crm.contacts.default") {
            Endpoint.crmContacts(query: "alice", status: .new, vertical: .creditCards)
        },
        ManifestRegisteredEndpoint(id: "native.crm.contact-detail.default") {
            Endpoint.crmContact(contactId: "00000000-0000-7000-8000-000000000584")
        },
        ManifestRegisteredEndpoint(id: "native.crm.contact-create.default") {
            Endpoint.createCrmContact(
                name: "Alice Creator",
                email: "alice@example.test",
                vertical: .creditCards,
                contactType: .influencer,
                followerCount: 250_000,
                notes: "Creator outreach contact"
            )
        },
        ManifestRegisteredEndpoint(id: "native.crm.contact-update.default") {
            Endpoint.updateCrmContact(
                contactId: "00000000-0000-7000-8000-000000000584",
                name: "Alice Creator",
                email: "alice@example.test",
                phone: "+1-415-555-0100",
                vertical: .creditCards,
                contactType: .influencer,
                followerCount: 255_000,
                notes: "Updated outreach notes"
            )
        },
        ManifestRegisteredEndpoint(id: "native.crm.contact-archive.default") {
            Endpoint.archiveCrmContact(contactId: "00000000-0000-7000-8000-000000000584")
        },
        ManifestRegisteredEndpoint(id: "native.crm.contact-emails.default") {
            Endpoint.crmContactEmails(contactId: "00000000-0000-7000-8000-000000000584")
        },
        ManifestRegisteredEndpoint(id: "native.crm.contact-email-send.default") {
            Endpoint.sendCrmEmail(
                contactId: "00000000-0000-7000-8000-000000000584",
                subject: "Warm intro",
                bodyText: "Hi Alice, great to connect.",
                emailProvider: .ses,
                aiPrompt: "Write a warm intro",
                aiGeneratedAt: Date(timeIntervalSince1970: 1_782_908_970)
            )
        },
        ManifestRegisteredEndpoint(id: "native.crm.contact-notes.default") {
            Endpoint.crmContactNotes(contactId: "00000000-0000-7000-8000-000000000584")
        },
        ManifestRegisteredEndpoint(id: "native.crm.contact-note-create.default") {
            Endpoint.createCrmNote(contactId: "00000000-0000-7000-8000-000000000584", body: "Met at the conference")
        },
        ManifestRegisteredEndpoint(id: "native.crm.contact-note-delete.default") {
            Endpoint.deleteCrmNote(
                contactId: "00000000-0000-7000-8000-000000000584",
                noteId: "00000000-0000-7000-8000-000000000901"
            )
        },
        ManifestRegisteredEndpoint(id: "native.crm.contact-link-user.default") {
            Endpoint.linkCrmContactToUser(
                contactId: "00000000-0000-7000-8000-000000000584",
                userId: "00000000-0000-7000-8000-000000000001"
            )
        },
        ManifestRegisteredEndpoint(id: "native.crm.contact-unlink-user.default") {
            Endpoint.unlinkCrmContactFromUser(contactId: "00000000-0000-7000-8000-000000000584")
        },
        ManifestRegisteredEndpoint(id: "native.crm.contact-email-draft.default") {
            Endpoint.crmContactEmailDraft(
                contactId: "00000000-0000-7000-8000-000000000584",
                prompt: "Focus on travel content",
                tone: "friendly"
            )
        },
        ManifestRegisteredEndpoint(id: "native.crm.import.success.default") {
            Endpoint.importCrmContacts(csv: "name,email\nAlice Creator,alice@example.test")
        },
        ManifestRegisteredEndpoint(id: "native.crm.import.validation.default") {
            Endpoint.importCrmContacts(csv: "name,email,follower_count\nAlice Creator,alice@example.test,-1")
        }
    ]
}
