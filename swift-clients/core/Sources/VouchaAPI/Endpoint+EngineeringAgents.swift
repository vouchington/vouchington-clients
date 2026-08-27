import Foundation
import VouchaModels

public extension Endpoint {
    static func agents(after: String? = nil, limit: Int = 25) -> Endpoint {
        paginatedAgentEndpoint(path: "/api/v1/agents", after: after, limit: limit)
    }

    static func agent(idOrSlug: String) -> Endpoint {
        Endpoint(.GET, path: "/api/v1/agents/\(pathSegment(idOrSlug))")
    }

    static func agentConversations(
        agentIdOrSlug: String,
        filter: AgentConversationFilter? = nil,
        after: String? = nil,
        limit: Int = 25
    ) -> Endpoint {
        let endpoint = paginatedAgentEndpoint(
            path: "/api/v1/agents/\(pathSegment(agentIdOrSlug))/conversations",
            after: after,
            limit: limit
        )
        guard let filter = filter?.trimmed else { return endpoint }
        return Endpoint(
            endpoint.method,
            path: endpoint.path,
            queryItems: endpoint.queryItems + [URLQueryItem(name: filter.kind.rawValue, value: filter.value)]
        )
    }

    static func agentConversation(
        agentIdOrSlug: String,
        conversationId: String,
        after: String? = nil,
        limit: Int = 50
    ) -> Endpoint {
        var queryItems = [URLQueryItem(name: "limit", value: "\(limit)")]
        if let after {
            queryItems.append(URLQueryItem(name: "after", value: after))
        }
        return Endpoint(
            .GET,
            path: "/api/v1/agents/\(pathSegment(agentIdOrSlug))/conversations/\(pathSegment(conversationId))",
            queryItems: queryItems
        )
    }

    private static func paginatedAgentEndpoint(path: String, after: String?, limit: Int) -> Endpoint {
        var queryItems = [URLQueryItem(name: "limit", value: "\(limit)")]
        if let after {
            queryItems.append(URLQueryItem(name: "after", value: after))
        }
        return Endpoint(.GET, path: path, queryItems: queryItems)
    }
}
