import Foundation
import VouchaModels

private struct MembershipGrantBody: Encodable {
    let userId: String
    let plan: MembershipPlanSlug
    let skuId: String
    let durationDays: Int
}

public extension Endpoint {
    static var membershipPlans: Endpoint {
        Endpoint(.GET, path: "/api/v1/memberships/plans")
    }

    static func grantMembership(
        userId: String,
        plan: MembershipPlanSlug,
        skuId: String,
        durationDays: Int
    ) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/membership-grants",
            body: MembershipGrantBody(userId: userId, plan: plan, skuId: skuId, durationDays: durationDays)
        )
    }
}
