/// Returned by sign-in endpoints when multi-factor authentication is required.
public struct MFAChallenge: Sendable {
    /// The login attempt identifier passed to the MFA verification endpoint.
    public let loginAttemptId: String

    /// Creates a new MFA challenge with the given login attempt identifier.
    public init(loginAttemptId: String) {
        self.loginAttemptId = loginAttemptId
    }
}
