/// The current user's editable profile — bio text returned by GET /api/v1/my/profile.
/// Avatar and username live on `PrivateUser` (from GET /api/v1/my/identity).
public struct Profile: Codable, Identifiable, Sendable {
    public let id: String
    public let markdown: String
}
