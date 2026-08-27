import Foundation
import VouchaModels

private struct AssignStaffSupportThreadBody: Encodable {
    let assignedToId: String
}

private struct ResolveStaffSupportThreadBody: Encodable {
    let resolved: Bool
}

private struct StaffSupportMessageBody: Encodable {
    let bodyText: String
}

private struct StaffSupportContactBody: Encodable {
    let name: String?
    let notes: String?
}

public extension Endpoint {
    static func staffSupportThreads(
        query: String? = nil,
        status: StaffSupportThreadStatusFilter? = nil,
        after: String? = nil,
        limit: Int = 30
    ) -> Endpoint {
        Endpoint(.GET, path: "/api/v1/support/threads", queryItems: supportQueryItems(
            query: query, status: status?.rawValue, after: after, limit: limit
        ))
    }

    static func staffSupportThread(threadId: String) -> Endpoint {
        Endpoint(.GET, path: "/api/v1/support/threads/\(pathSegment(threadId))")
    }

    static func assignStaffSupportThread(threadId: String, administratorId: String) -> Endpoint {
        Endpoint(
            .PATCH,
            path: "/api/v1/support/threads/\(pathSegment(threadId))",
            body: AssignStaffSupportThreadBody(assignedToId: administratorId)
        )
    }

    static func setStaffSupportThreadResolved(threadId: String, resolved: Bool) -> Endpoint {
        Endpoint(
            .PATCH,
            path: "/api/v1/support/threads/\(pathSegment(threadId))",
            body: ResolveStaffSupportThreadBody(resolved: resolved)
        )
    }

    static func staffSupportMessages(threadId: String, after: String? = nil, limit: Int = 50) -> Endpoint {
        Endpoint(
            .GET,
            path: "/api/v1/support/threads/\(pathSegment(threadId))/messages",
            queryItems: supportQueryItems(after: after, limit: limit)
        )
    }

    static func createStaffSupportMessage(threadId: String, bodyText: String) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/support/threads/\(pathSegment(threadId))/messages",
            body: StaffSupportMessageBody(bodyText: bodyText)
        )
    }

    static func queueStaffSupportDraft(threadId: String) -> Endpoint {
        Endpoint(.POST, path: "/api/v1/support/threads/\(pathSegment(threadId))/drafts")
    }

    static func updateStaffSupportDraft(threadId: String, messageId: String, bodyText: String) -> Endpoint {
        Endpoint(
            .PATCH,
            path: "/api/v1/support/threads/\(pathSegment(threadId))/messages/\(pathSegment(messageId))",
            body: StaffSupportMessageBody(bodyText: bodyText)
        )
    }

    static func approveStaffSupportMessage(threadId: String, messageId: String) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/support/threads/\(pathSegment(threadId))/messages/\(pathSegment(messageId))/approvals"
        )
    }

    static func sendStaffSupportMessage(threadId: String, messageId: String) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/support/threads/\(pathSegment(threadId))/messages/\(pathSegment(messageId))/sends"
        )
    }

    static func staffSupportContacts(query: String? = nil, after: String? = nil, limit: Int = 30) -> Endpoint {
        Endpoint(.GET, path: "/api/v1/support/contacts", queryItems: supportQueryItems(
            query: query, after: after, limit: limit
        ))
    }

    static func staffSupportContact(contactId: String, after: String? = nil, limit: Int = 50) -> Endpoint {
        Endpoint(
            .GET,
            path: "/api/v1/support/contacts/\(pathSegment(contactId))",
            queryItems: supportQueryItems(after: after, limit: limit)
        )
    }

    static func updateStaffSupportContact(contactId: String, name: String?, notes: String?) -> Endpoint {
        Endpoint(
            .PATCH,
            path: "/api/v1/support/contacts/\(pathSegment(contactId))",
            body: StaffSupportContactBody(name: name, notes: notes)
        )
    }

    private static func supportQueryItems(
        query: String? = nil,
        status: String? = nil,
        after: String? = nil,
        limit: Int
    ) -> [URLQueryItem] {
        var items = [URLQueryItem(name: "limit", value: "\(limit)")]
        if let query, !query.isEmpty {
            items.append(.init(name: "q", value: query))
        }
        if let status {
            items.append(.init(name: "status", value: status))
        }
        if let after {
            items.append(.init(name: "after", value: after))
        }
        return items
    }
}
