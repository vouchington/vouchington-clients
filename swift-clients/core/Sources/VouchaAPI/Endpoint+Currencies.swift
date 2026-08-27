import Foundation
import VouchaModels

public typealias CurrencyPage = Page<Currency>

public extension Endpoint {
    static func currencies(after: String? = nil, limit: Int? = nil) -> Endpoint {
        var queryItems: [URLQueryItem] = []
        if let limit {
            queryItems.append(URLQueryItem(name: "limit", value: String(limit)))
        }
        if let after {
            queryItems.append(URLQueryItem(name: "after", value: after))
        }
        return Endpoint(.GET, path: "/api/v1/currencies", queryItems: queryItems)
    }
}

public extension APIClient {
    func currencies(after: String? = nil, limit: Int? = nil) async throws -> CurrencyPage {
        try await send(.currencies(after: after, limit: limit))
    }
}
