/// Response returned after an administrator grants one identity-verification retry.
public struct IdentityVerificationAttemptGrantResponse: Codable, Sendable {
    public let granted: Bool
}
