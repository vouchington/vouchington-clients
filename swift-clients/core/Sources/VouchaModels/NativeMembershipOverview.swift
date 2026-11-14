import Foundation

public struct NativeMembershipOverview: Codable, Sendable {
    @RequiredNullable public var membership: NativeMembershipEntitlement?
    public let sources: [NativeMembershipSource]
    public let pending: NativeMembershipPending
    @RequiredNullable public var management: NativeMembershipManagement?

    public init(
        membership: NativeMembershipEntitlement? = nil,
        sources: [NativeMembershipSource] = [],
        pending: NativeMembershipPending = .empty,
        management: NativeMembershipManagement? = nil
    ) {
        _membership = RequiredNullable(wrappedValue: membership)
        self.sources = sources
        self.pending = pending
        _management = RequiredNullable(wrappedValue: management)
    }

    public init(from decoder: Decoder) throws {
        let container = try decoder.container(keyedBy: CodingKeys.self)
        _membership = try container.decode(
            RequiredNullable<NativeMembershipEntitlement>.self,
            forKey: .membership
        )
        sources = try container.decodeIfPresent([NativeMembershipSource].self, forKey: .sources) ?? []
        pending = try container.decodeIfPresent(NativeMembershipPending.self, forKey: .pending) ?? .empty
        if container.contains(.management) {
            _management = try container.decode(
                RequiredNullable<NativeMembershipManagement>.self,
                forKey: .management
            )
        } else {
            _management = RequiredNullable(wrappedValue: nil)
        }
    }

    public func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(_membership, forKey: .membership)
        try container.encode(sources, forKey: .sources)
        try container.encode(pending, forKey: .pending)
        try container.encode(_management, forKey: .management)
    }

    public var visiblePlan: String {
        membership?.plan ?? "free"
    }

    /// An owned or store-active subscription cannot start another purchase until replacement mode exists.
    public var blocksStoreReplacement: Bool {
        if membership != nil {
            return true
        }
        return sources.contains { $0.isEffective || $0.status == "active" }
    }

    private enum CodingKeys: String, CodingKey {
        case membership
        case sources
        case pending
        case management
    }
}

public struct NativeMembershipEntitlement: Codable, Sendable {
    public let id: String?
    public let plan: String
    public let status: String
    public let cancelAtPeriodEnd: Bool?

    public init(id: String? = nil, plan: String, status: String, cancelAtPeriodEnd: Bool? = nil) {
        self.id = id
        self.plan = plan
        self.status = status
        self.cancelAtPeriodEnd = cancelAtPeriodEnd
    }
}

public struct NativeMembershipManagement: Codable, Sendable {
    public let destination: String
    public let provider: String
}
