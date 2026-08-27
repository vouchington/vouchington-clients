import Foundation

/// A user returned by `GET /api/v1/users?q=`.
///
/// Administrators receive private extras (`email_address`, `suspended_at`). Public search
/// omits those keys. Username may be absent on private users, so this stays separate from
/// `PublicUser`.
public struct UserSearchResult: Codable, Identifiable, Sendable {
    public let id: String
    public let username: String?
    public let name: String?
    public let emailAddress: String?
    public let suspendedAt: Date?
    public let suspendedReason: String?
    public let suspendedById: String?
}
