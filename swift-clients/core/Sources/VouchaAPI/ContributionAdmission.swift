import Foundation

public struct ContributionAdmission: Decodable, Sendable {
    public let allowed: Bool
    public let reason: String?
    public let retryAfterSeconds: Int?
}

public struct ContributionStatus: Decodable, Sendable {
    public let allowed: Bool
    public let reason: String?
    public let gatedUntil: Date?
}

public struct ContributionDailyQuota: Decodable, Sendable {
    public let limit: Int
    public let used: Int
}

public struct ContributionStatusResponse: Decodable, Sendable {
    public let admission: ContributionAdmission
    public let contributionStatus: ContributionStatus
    public let dailyQuota: ContributionDailyQuota
    public let actionLimit: ContributionActionLimitStatus?
}

public struct ContributionActionLimitStatus: Decodable, Sendable {
    public let action: String
    public let allowed: Bool
    public let dailyWindow: ContributionLimitUsage
    public let shortWindow: ContributionLimitUsage
    public let tier: String
}

public struct ContributionLimitUsage: Decodable, Sendable {
    public let limit: Int
    public let used: Int
    public let windowSeconds: Int
}

/// Stores the current idempotency UUID for each caller-supplied draft scope.
/// Call `complete` only after a successful create; challenge values are deliberately not input.
public actor ContributionRequestIdentity {
    private struct CurrentIdentity {
        let canonicalIntent: String
        let key: UUID
    }

    private var identities: [String: CurrentIdentity] = [:]

    public init() {}

    public func key(surface: String, canonicalIntent: String) -> String {
        if let existing = identities[surface], existing.canonicalIntent == canonicalIntent {
            return existing.key.uuidString.lowercased()
        }
        let created = UUID()
        identities[surface] = CurrentIdentity(canonicalIntent: canonicalIntent, key: created)
        return created.uuidString.lowercased()
    }

    public func complete(surface: String, canonicalIntent: String) {
        guard identities[surface]?.canonicalIntent == canonicalIntent else { return }
        identities.removeValue(forKey: surface)
    }
}

public struct ContributionAdmissionFailure: Error, Sendable {
    public let statusCode: Int
    public let code: String?
    public let responseBody: Data
    public let retryAfter: TimeInterval?
}
