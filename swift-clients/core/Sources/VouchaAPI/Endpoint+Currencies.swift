import Foundation
import VouchaModels

public typealias CurrencyPage = Page<Currency>

public extension Endpoint {
    static func currencies(after: String? = nil, limit: Int? = nil) -> Endpoint {
        pagedGet(path: "/api/v1/currencies", after: after, limit: limit)
    }
}

public extension APIClient {
    func currencies(after: String? = nil, limit: Int? = nil) async throws -> CurrencyPage {
        try await send(.currencies(after: after, limit: limit))
    }
}
