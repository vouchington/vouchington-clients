import Foundation

public extension Endpoint {
    static func adminAiCosts(after: String? = nil) -> Endpoint {
        var queryItems = [URLQueryItem(name: "limit", value: "25")]
        if let after {
            queryItems.append(URLQueryItem(name: "after", value: after))
        }
        return Endpoint(.GET, path: "/api/v1/admin/ai-costs", queryItems: queryItems)
    }
}

public extension APIClient {
    func adminAiCosts(after: String? = nil) async throws -> AiCostTotalsPage {
        try await send(.adminAiCosts(after: after))
    }
}
