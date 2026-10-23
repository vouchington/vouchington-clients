import Foundation

public struct NativeMembershipPending: Codable, Sendable {
    public let financialOperations: [NativeMembershipFinancialOperation]
    public let grants: Int
    public let switches: [NativeMembershipSwitch]
    public let verifications: [NativeMembershipPendingVerification]

    public static let empty = NativeMembershipPending(
        financialOperations: [],
        grants: 0,
        switches: [],
        verifications: []
    )
}

public struct NativeMembershipFinancialOperation: Codable, Sendable {
    public let id: String
    public let kind: String
    public let provider: String
    public let status: String
}

public struct NativeMembershipSwitch: Codable, Sendable {
    public let eligibleAt: String
    public let plan: String
    public let provider: String
    public let sourceId: String
}

public struct NativeMembershipPendingVerification: Codable, Sendable {
    public let id: String
    @RequiredNullable public var nextProcessingAt: String?
    public let provider: String
    @RequiredNullable public var reasonCode: String?
    public let status: String
}

public struct NativeMembershipSource: Codable, Sendable {
    public let accessEndsAt: String
    public let effectiveAt: String
    public let id: String
    public let isEffective: Bool
    public let kind: String
    public let plan: String
    public let provider: String
    @RequiredNullable public var renewal: NativeMembershipRenewal?
    public let shouldAutoRenew: Bool
    public let status: String
}

public struct NativeMembershipRenewal: Codable, Sendable {
    public let effectiveAt: String
    @RequiredNullable public var price: Money?
    public let productId: String
}
