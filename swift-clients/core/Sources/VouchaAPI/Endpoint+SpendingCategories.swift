import Foundation
import VouchaModels

public struct CreateSpendingCategoryBody: Encodable, Sendable {
    public let spendingCategoryId: String
    public let amount: Money
    public let spendingFrequency: SpendingFrequency
    public let note: String?

    public init(
        spendingCategoryId: String,
        amount: Money,
        spendingFrequency: SpendingFrequency,
        note: String? = nil
    ) {
        self.spendingCategoryId = spendingCategoryId
        self.amount = amount
        self.spendingFrequency = spendingFrequency
        self.note = note
    }

    private enum CodingKeys: String, CodingKey {
        case spendingCategoryId = "spendingCategoryTopicId"
        case amount, spendingFrequency, note
    }
}

public struct UpdateSpendingCategoryBody: Encodable, Sendable {
    public let amount: Money?
    public let spendingFrequency: SpendingFrequency?
    public let note: NullableValue<String>?

    public init(
        amount: Money? = nil,
        spendingFrequency: SpendingFrequency? = nil,
        note: NullableValue<String>? = nil
    ) {
        self.amount = amount
        self.spendingFrequency = spendingFrequency
        self.note = note
    }
}

public extension Endpoint {
    static func spendingCategories(after: String? = nil, limit: Int = 25) -> Endpoint {
        var queryItems = [URLQueryItem(name: "limit", value: String(limit))]
        if let after {
            queryItems.append(URLQueryItem(name: "after", value: after))
        }
        return Endpoint(.GET, path: "/api/v1/my/spending-categories", queryItems: queryItems)
    }

    static func createSpendingCategory(body: CreateSpendingCategoryBody) -> Endpoint {
        Endpoint(.POST, path: "/api/v1/my/spending-categories", body: body)
    }

    static func updateSpendingCategory(id: String, body: UpdateSpendingCategoryBody) -> Endpoint {
        Endpoint(.PATCH, path: "/api/v1/my/spending-categories/\(pathSegment(id))", body: body)
    }

    static func deleteSpendingCategory(id: String) -> Endpoint {
        Endpoint(.DELETE, path: "/api/v1/my/spending-categories/\(pathSegment(id))")
    }

    static func spendingCategoryTopics(query: String, limit: Int = 10) -> Endpoint {
        topics(query: query, limit: limit, spendingCategory: true)
    }
}
