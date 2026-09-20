import Foundation
import VouchaModels

private struct MembershipGrantBody: Encodable {
    let userId: String
    let plan: MembershipPlanSlug
    let skuId: String
    let durationDays: Int
}

private struct MembershipGrantRevokeBody: Encodable {
    let reason: String
}

public extension Endpoint {
    static func grantMembership(
        userId: String,
        plan: MembershipPlanSlug,
        skuId: String,
        durationDays: Int = 30
    ) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/membership-grants",
            body: MembershipGrantBody(userId: userId, plan: plan, skuId: skuId, durationDays: durationDays)
        )
    }

    static func revokeMembershipGrant(grantId: String, reason: String) -> Endpoint {
        Endpoint(
            .DELETE,
            path: "/api/v1/membership-grants/\(pathSegment(grantId))",
            body: MembershipGrantRevokeBody(reason: reason)
        )
    }
}
