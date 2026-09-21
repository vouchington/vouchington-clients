import Foundation

private struct MembershipPurchaseIntentBody: Encodable {
    let idempotencyKey: String
    let productId: String
    let provider: String
}

private struct MembershipVerificationEvidence: Encodable {
    let signedTransaction: String
}

private struct MembershipVerificationBody: Encodable {
    let evidence: MembershipVerificationEvidence
    let idempotencyKey: String
    let provider: String
    let purchaseIntentId: String
}

private struct EmptyJSONObjectBody: Encodable {}

public struct MembershipRefundBody: Encodable {
    public let cancel: Bool
    public let chargeId: String
    public let idempotencyKey: String
    public let invoiceId: String
    public let reason: String
    public let userId: String

    public init(
        cancel: Bool,
        chargeId: String,
        idempotencyKey: String,
        invoiceId: String,
        reason: String,
        userId: String
    ) {
        self.cancel = cancel
        self.chargeId = chargeId
        self.idempotencyKey = idempotencyKey
        self.invoiceId = invoiceId
        self.reason = reason
        self.userId = userId
    }
}

public extension Endpoint {
    static func createMembershipPurchaseIntent(
        idempotencyKey: String,
        productId: String,
        provider: String
    ) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/membership-purchase-intents",
            body: MembershipPurchaseIntentBody(
                idempotencyKey: idempotencyKey,
                productId: productId,
                provider: provider
            )
        )
    }

    static func createMembershipVerification(
        signedTransaction: String,
        idempotencyKey: String,
        provider: String,
        purchaseIntentId: String
    ) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/membership-verifications",
            body: MembershipVerificationBody(
                evidence: MembershipVerificationEvidence(signedTransaction: signedTransaction),
                idempotencyKey: idempotencyKey,
                provider: provider,
                purchaseIntentId: purchaseIntentId
            )
        )
    }

    static func membershipVerification(id: String) -> Endpoint {
        Endpoint(.GET, path: "/api/v1/membership-verifications/\(pathSegment(id))")
    }

    static var microsoftStoreServiceTickets: Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/memberships/microsoft-store/service-tickets",
            body: EmptyJSONObjectBody()
        )
    }

    static func createMembershipRefund(_ body: MembershipRefundBody) -> Endpoint {
        Endpoint(.POST, path: "/api/v1/memberships/refunds", body: body)
    }
}
