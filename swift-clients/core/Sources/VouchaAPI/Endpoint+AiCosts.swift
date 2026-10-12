import Foundation

public extension Endpoint {
    static func adminAiCosts(after: String? = nil) -> Endpoint {
        pagedGet(path: "/api/v1/admin/ai-costs", after: after, limit: 25)
    }
}

public extension APIClient {
    func adminAiCosts(after: String? = nil) async throws -> AiCostTotalsPage {
        try await send(.adminAiCosts(after: after))
    }
}
