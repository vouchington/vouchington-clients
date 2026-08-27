import Foundation
import VouchaModels

/// The backend expects `entityType` and `entityId` in camelCase while `APIClient` snake-cases
/// ordinary keyed bodies, so encode via literal dictionary keys.
struct ReportBody: Encodable {
    let entityType: String
    let entityId: String
    let reason: String
    let note: String?
    let cfTurnstileResponse: String?

    func encode(to encoder: any Encoder) throws {
        var payload: [String: String] = [
            "entityType": entityType,
            "entityId": entityId,
            "reason": reason
        ]
        if let note {
            payload["note"] = note
        }
        if let cfTurnstileResponse {
            payload["cf_turnstile_response"] = cfTurnstileResponse
        }
        var container = encoder.singleValueContainer()
        try container.encode(payload)
    }
}

public extension Endpoint {
    static func hostnames(query: String? = nil, limit: Int = 25) -> Endpoint {
        var items: [URLQueryItem] = [.init(name: "limit", value: "\(limit)")]
        if let query {
            items.append(.init(name: "query", value: query))
        }
        return Endpoint(.GET, path: "/api/v1/hostnames", queryItems: items)
    }

    static func hostname(idOrHostname: String) -> Endpoint {
        Endpoint(.GET, path: "/api/v1/hostnames/\(pathSegment(idOrHostname))")
    }

    static func voteHostname(hostnameId: String, choice: ElectionVoteChoice) -> Endpoint {
        Endpoint(.PUT, path: "/api/v1/hostnames/\(pathSegment(hostnameId))/vote", body: ["choice": choice.rawValue])
    }

    static func clearHostnameVote(hostnameId: String) -> Endpoint {
        Endpoint(.DELETE, path: "/api/v1/hostnames/\(pathSegment(hostnameId))/vote")
    }

    static func report(
        entityType: String,
        entityId: String,
        reason: String,
        note: String? = nil,
        turnstileToken: String? = nil
    ) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/reports",
            body: ReportBody(
                entityType: entityType,
                entityId: entityId,
                reason: reason,
                note: note,
                cfTurnstileResponse: turnstileToken
            )
        )
    }

    static func url(urlId: String) -> Endpoint {
        Endpoint(.GET, path: "/api/v1/urls/\(pathSegment(urlId))")
    }

    static func urlCrawls(urlId: String, after: String? = nil, limit: Int = 25) -> Endpoint {
        var items: [URLQueryItem] = [.init(name: "limit", value: "\(limit)")]
        if let after {
            items.append(.init(name: "after", value: after))
        }
        return Endpoint(.GET, path: "/api/v1/urls/\(pathSegment(urlId))/crawls", queryItems: items)
    }

    static func urlCrawl(urlId: String, crawlId: String) -> Endpoint {
        Endpoint(.GET, path: "/api/v1/urls/\(pathSegment(urlId))/crawls/\(pathSegment(crawlId))")
    }

    static func triggerUrlCrawl(urlId: String) -> Endpoint {
        Endpoint(.POST, path: "/api/v1/urls/\(pathSegment(urlId))/crawl")
    }
}
