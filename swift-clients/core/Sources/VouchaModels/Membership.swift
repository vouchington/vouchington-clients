import Foundation

public enum MembershipStatus: String, Codable, CaseIterable, Sendable {
    case active
    case cancelled
    case expired
    case pastDue = "past_due"
    case paused
}

public enum MembershipBillingInterval: String, Codable, CaseIterable, Sendable {
    case monthly
    case yearly
}

public struct MembershipSku: Decodable, Identifiable, Sendable {
    public let id: String
    public let plan: String
    public let price: Money
    public let interval: MembershipBillingInterval
    public let stripePriceId: String
    public let retiredAt: Date?
}

public struct Membership: Decodable, Identifiable, Sendable {
    public let id: String
    public let userId: String
    public let plan: String
    public let status: MembershipStatus
    public let startedAt: Date
    public let expiresAt: Date?
    public let stripeSubscriptionId: String?
    public let stripeCustomerId: String?
    public let grantedById: String?
    public let cancelledAt: Date?
    public let expiredAt: Date?
    public let pastDueAt: Date?
    public let pausedAt: Date?
    public let cancelAtPeriodEnd: Bool
    public let latestChangeId: String?
    public let createdAt: Date
    public let updatedAt: Date
    public let sku: MembershipSku
    public let hasStripeSubscription: Bool
}

public struct MembershipResponse: Decodable, Sendable {
    public let membership: Membership?
}

public struct MembershipCheckoutSessionResponse: Decodable, Sendable {
    public let checkoutSession: Session

    public struct Session: Decodable, Sendable {
        public let id: String
        public let url: String?
    }
}

public struct MembershipPortalSessionResponse: Decodable, Sendable {
    public let portalSession: Session

    public struct Session: Decodable, Sendable {
        public let url: String
    }
}
