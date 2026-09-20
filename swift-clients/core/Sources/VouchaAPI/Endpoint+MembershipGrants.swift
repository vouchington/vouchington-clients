import Foundation

private struct MembershipGrantRevokeBody: Encodable {
    let reason: String
}

public extension Endpoint {
    static func revokeMembershipGrant(grantId: String, reason: String) -> Endpoint {
        Endpoint(
            .DELETE,
            path: "/api/v1/membership-grants/\(pathSegment(grantId))",
            body: MembershipGrantRevokeBody(reason: reason)
        )
    }
}
