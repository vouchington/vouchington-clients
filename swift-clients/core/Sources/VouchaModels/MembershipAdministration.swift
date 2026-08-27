/// The supported plans for an administrator-issued membership.
public enum MembershipPlanSlug: String, Codable, CaseIterable, Identifiable, Sendable {
    case plus
    case pro

    public var id: String {
        rawValue
    }
}

public struct MembershipGrantResponse: Codable, Sendable {
    public let membership: MembershipGrant
}

public struct MembershipGrant: Codable, Identifiable, Sendable {
    public let id: String
}

/// A user returned by the administrator-only membership grant search.
///
/// Administrator searches include private users, whose username may be absent.
/// This deliberately remains separate from `PublicUser`, whose username is required.
public struct MembershipGrantUser: Codable, Identifiable, Sendable {
    public let id: String
    public let username: String?
    public let name: String?

    public var displayLabel: String {
        guard let username = username?.trimmingCharacters(in: .whitespacesAndNewlines), !username.isEmpty else {
            return id
        }
        return "@\(username)"
    }
}
