import Foundation

private struct IdentityVerificationAttemptGrantBody: Encodable {
    let note: String
}

public extension Endpoint {
    static func grantIdentityVerificationAttempt(userId: String, note: String) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/admin/users/\(pathSegment(userId))/identity-verification-attempts",
            body: IdentityVerificationAttemptGrantBody(note: note)
        )
    }
}
