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

/// Stores an idempotency UUID by a caller-supplied canonical draft intent.
/// Call `complete` only after a successful create; challenge values are deliberately not input.
public actor ContributionRequestIdentity {
    private var keys: [String: UUID] = [:]

    public init() {}

    public func key(surface: String, canonicalIntent: String) -> String {
        let scope = surface + "\u{001F}" + canonicalIntent
        if let existing = keys[scope] {
            return existing.uuidString.lowercased()
        }
        let created = UUID()
        keys[scope] = created
        return created.uuidString.lowercased()
    }

    public func complete(surface: String, canonicalIntent: String) {
        keys.removeValue(forKey: surface + "\u{001F}" + canonicalIntent)
    }
}

public struct ContributionAdmissionFailure: Error, Sendable {
    public let statusCode: Int
    public let code: String?
    public let responseBody: Data
    public let retryAfter: TimeInterval?
}
