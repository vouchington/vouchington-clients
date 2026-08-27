import Foundation

public struct MembershipSkuSummary: Codable, Identifiable, Sendable {
    public let id: String
    public let plan: String
    public let price: Money
    public let interval: String
    public let stripePriceId: String
}

public struct MembershipPlansResponse: Codable, Sendable {
    public let plans: [String: [MembershipSkuSummary]]
    public let benefitCatalog: MembershipBenefitCatalog?
}

public struct MembershipBenefitCatalog: Codable, Sendable {
    public let version: Int
    public let groups: [MembershipBenefitGroup]
}

public struct MembershipBenefitGroup: Codable, Sendable {
    public let id: String
    public let benefits: [MembershipBenefit]
}

public struct MembershipBenefit: Codable, Sendable {
    public let id: String
    public let placements: [String]
    public let values: [String: MembershipBenefitValue]
}

public struct MembershipBenefitValue: Codable, Sendable {
    public let kind: String
    public let access: String?
    public let included: Bool?
    public let level: String?
    public let quantity: Int?
}
