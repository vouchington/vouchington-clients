import Foundation

public struct SpendingCategoryTopicSummary: Codable, Equatable, Identifiable, Sendable {
    public let id: String
    public let name: String
    public let slug: String

    public init(id: String, name: String, slug: String) {
        self.id = id
        self.name = name
        self.slug = slug
    }
}

public enum SpendingFrequency: String, Codable, Equatable, Sendable, CaseIterable {
    case monthly
    case annually
}

public enum SpendingCategoryOwnerType: String, Codable, Equatable, Sendable {
    case individual
    case household
}

public struct SpendingCategory: Codable, Equatable, Identifiable, Sendable {
    public let id: String
    public let spendingCategoryId: String
    public let amount: Money
    public let spendingFrequency: SpendingFrequency
    public let note: String?
    public let ownerType: SpendingCategoryOwnerType
    public let canManage: Bool
    public let spendingCategory: SpendingCategoryTopicSummary

    public init(
        id: String,
        spendingCategoryId: String,
        amount: Money,
        spendingFrequency: SpendingFrequency,
        note: String?,
        ownerType: SpendingCategoryOwnerType,
        canManage: Bool = true,
        spendingCategory: SpendingCategoryTopicSummary
    ) {
        self.id = id
        self.spendingCategoryId = spendingCategoryId
        self.amount = amount
        self.spendingFrequency = spendingFrequency
        self.note = note
        self.ownerType = ownerType
        self.canManage = canManage
        self.spendingCategory = spendingCategory
    }

    public init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        id = try container.decode(String.self, forKey: .id)
        spendingCategoryId = try container.decode(String.self, forKey: .spendingCategoryId)
        amount = try container.decode(Money.self, forKey: .amount)
        spendingFrequency = try container.decode(SpendingFrequency.self, forKey: .spendingFrequency)
        note = try container.decodeIfPresent(String.self, forKey: .note)
        ownerType = try container.decode(SpendingCategoryOwnerType.self, forKey: .ownerType)
        canManage = try container.decodeIfPresent(Bool.self, forKey: .canManage) ?? true
        spendingCategory = try container.decode(SpendingCategoryTopicSummary.self, forKey: .spendingCategory)
    }

    public func encode(to encoder: any Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(id, forKey: .id)
        try container.encode(spendingCategoryId, forKey: .spendingCategoryId)
        try container.encode(amount, forKey: .amount)
        try container.encode(spendingFrequency, forKey: .spendingFrequency)
        try container.encode(note, forKey: .note)
        try container.encode(ownerType, forKey: .ownerType)
        try container.encode(canManage, forKey: .canManage)
        try container.encode(spendingCategory, forKey: .spendingCategory)
    }

    private enum CodingKeys: String, CodingKey {
        case id, amount, spendingFrequency, note, ownerType, canManage, spendingCategory
        case spendingCategoryId = "spendingCategoryTopicId"
    }
}

public struct SpendingCategoryPage: Codable, Sendable {
    public let results: [SpendingCategory]
    public let pageInfo: Page<SpendingCategory>.PageInfo

    public init(results: [SpendingCategory], pageInfo: Page<SpendingCategory>.PageInfo = .init(hasNextPage: false)) {
        self.results = results
        self.pageInfo = pageInfo
    }

    public init(from decoder: any Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        results = try container.decode([SpendingCategory].self, forKey: .results)
        pageInfo = try container
            .decodeIfPresent(Page<SpendingCategory>.PageInfo.self, forKey: .pageInfo) ?? .init(hasNextPage: false)
    }

    private enum CodingKeys: String, CodingKey { case results, pageInfo }
}

public struct SpendingCategoryResponse: Codable, Sendable {
    public let spendingCategory: SpendingCategory
}
