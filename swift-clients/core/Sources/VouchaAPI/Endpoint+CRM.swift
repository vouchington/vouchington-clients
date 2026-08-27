import Foundation
import VouchaModels

public extension Endpoint {
    static func crmContacts(
        query: String? = nil,
        status: CrmContactStatus? = nil,
        vertical: CrmContactVertical? = nil,
        linked: Bool? = nil,
        after: String? = nil,
        limit: Int = 25
    ) -> Endpoint {
        var queryItems: [URLQueryItem] = [
            .init(name: "limit", value: "\(limit)")
        ]
        if let query, !query.isEmpty {
            queryItems.append(.init(name: "q", value: query))
        }
        if let status {
            queryItems.append(.init(name: "status", value: status.rawValue))
        }
        if let vertical {
            queryItems.append(.init(name: "vertical", value: vertical.rawValue))
        }
        if let linked {
            queryItems.append(.init(name: "linked", value: linked ? "true" : "false"))
        }
        if let after {
            queryItems.append(.init(name: "after", value: after))
        }
        return Endpoint(.GET, path: "/api/v1/crm/contacts", queryItems: queryItems)
    }

    static func crmContact(contactId: String) -> Endpoint {
        Endpoint(.GET, path: "/api/v1/crm/contacts/\(pathSegment(contactId))")
    }

    static func createCrmContact(
        name: String,
        email: String,
        phone: String? = nil,
        vertical: CrmContactVertical? = nil,
        contactType: CrmContactType? = nil,
        source: CrmContactSource? = nil,
        followerCount: Int? = nil,
        notes: String? = nil,
        metadata: [String: DecodedJSONValue]? = nil,
        assignedToId: String? = nil,
        socialAccounts: [CrmContactSocialAccountInput]? = nil
    ) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/crm/contacts",
            body: CrmContactCreateBody(
                name: name,
                email: email,
                phone: phone,
                vertical: vertical,
                contactType: contactType,
                source: source,
                followerCount: followerCount,
                notes: notes,
                metadata: metadata,
                assignedToId: assignedToId,
                socialAccounts: socialAccounts
            )
        )
    }

    static func updateCrmContact(
        contactId: String,
        name: String? = nil,
        email: String? = nil,
        phone: String? = nil,
        vertical: CrmContactVertical? = nil,
        contactType: CrmContactType? = nil,
        followerCount: Int? = nil,
        notes: String? = nil,
        metadata: [String: DecodedJSONValue]? = nil,
        assignedToId: String? = nil,
        contactedAt: Date? = nil,
        respondedAt: Date? = nil,
        convertedAt: Date? = nil,
        optedOutAt: Date? = nil
    ) -> Endpoint {
        Endpoint(
            .PATCH,
            path: "/api/v1/crm/contacts/\(pathSegment(contactId))",
            body: CrmContactUpdateBody(
                name: name,
                email: email,
                phone: phone,
                vertical: vertical,
                contactType: contactType,
                followerCount: followerCount,
                notes: notes,
                metadata: metadata,
                assignedToId: assignedToId,
                contactedAt: contactedAt,
                respondedAt: respondedAt,
                convertedAt: convertedAt,
                optedOutAt: optedOutAt
            )
        )
    }

    static func archiveCrmContact(contactId: String) -> Endpoint {
        Endpoint(.DELETE, path: "/api/v1/crm/contacts/\(pathSegment(contactId))")
    }

    static func crmContactEmails(contactId: String, after: String? = nil, limit: Int = 25) -> Endpoint {
        var queryItems: [URLQueryItem] = [.init(name: "limit", value: "\(limit)")]
        if let after {
            queryItems.append(.init(name: "after", value: after))
        }
        return Endpoint(.GET, path: "/api/v1/crm/contacts/\(pathSegment(contactId))/emails", queryItems: queryItems)
    }

    static func sendCrmEmail(
        contactId: String,
        subject: String,
        bodyHtml: String? = nil,
        bodyText: String? = nil,
        emailProvider: CrmEmailProvider,
        ctaUrl: String? = nil,
        aiPrompt: String? = nil,
        aiGeneratedAt: Date? = nil
    ) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/crm/contacts/\(pathSegment(contactId))/emails",
            body: CrmEmailSendBody(
                subject: subject,
                bodyHtml: bodyHtml,
                bodyText: bodyText,
                emailProvider: emailProvider,
                ctaUrl: ctaUrl,
                aiPrompt: aiPrompt,
                aiGeneratedAt: aiGeneratedAt
            )
        )
    }

    static func crmContactNotes(contactId: String, after: String? = nil, limit: Int = 25) -> Endpoint {
        var queryItems: [URLQueryItem] = [.init(name: "limit", value: "\(limit)")]
        if let after {
            queryItems.append(.init(name: "after", value: after))
        }
        return Endpoint(.GET, path: "/api/v1/crm/contacts/\(pathSegment(contactId))/notes", queryItems: queryItems)
    }

    static func createCrmNote(contactId: String, body: String) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/crm/contacts/\(pathSegment(contactId))/notes",
            body: CrmNoteBody(body: body)
        )
    }

    static func deleteCrmNote(contactId: String, noteId: String) -> Endpoint {
        Endpoint(.DELETE, path: "/api/v1/crm/contacts/\(pathSegment(contactId))/notes/\(pathSegment(noteId))")
    }

    static func linkCrmContactToUser(contactId: String, userId: String) -> Endpoint {
        Endpoint(
            .PUT,
            path: "/api/v1/crm/contacts/\(pathSegment(contactId))/user-link",
            body: ["user_id": userId]
        )
    }

    static func unlinkCrmContactFromUser(contactId: String) -> Endpoint {
        Endpoint(.DELETE, path: "/api/v1/crm/contacts/\(pathSegment(contactId))/user-link")
    }

    static func crmContactEmailDraft(contactId: String, prompt: String? = nil, tone: String? = nil) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/crm/contacts/\(pathSegment(contactId))/email-drafts",
            body: CrmContactEmailDraftBody(prompt: prompt, tone: tone)
        )
    }

    static func importCrmContacts(csv: String) -> Endpoint {
        Endpoint(.POST, path: "/api/v1/imports/crm-contacts", body: CrmImportBody(csv: csv))
    }
}
