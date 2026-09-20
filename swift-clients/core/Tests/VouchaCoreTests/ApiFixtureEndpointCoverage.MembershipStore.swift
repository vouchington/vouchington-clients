@testable import VouchaAPI

private let membershipProductId = "00000000-0000-7000-8000-000000000701"

private func membershipPurchaseIntent(
    idempotencySuffix: String,
    provider: String
) -> Endpoint {
    Endpoint.createMembershipPurchaseIntent(
        idempotencyKey: "00000000-0000-7000-8000-000000000\(idempotencySuffix)",
        productId: membershipProductId,
        provider: provider
    )
}

let membershipStoreFixtureEndpoints: [String: Endpoint] = [
    "native.memberships.me.default": Endpoint.membershipMe,
    "native.memberships.me.lifecycle.default": Endpoint.membershipMe,
    "native.memberships.purchase-intent.apple.default": membershipPurchaseIntent(
        idempotencySuffix: "803",
        provider: "apple_app_store"
    ),
    "native.memberships.purchase-intent.google.default": membershipPurchaseIntent(
        idempotencySuffix: "804",
        provider: "google_play"
    ),
    "native.memberships.purchase-intent.microsoft.default": membershipPurchaseIntent(
        idempotencySuffix: "805",
        provider: "microsoft_store"
    ),
    "native.memberships.purchase-intent.stripe.default": membershipPurchaseIntent(
        idempotencySuffix: "806",
        provider: "stripe"
    ),
    "native.memberships.purchase-intent.conflict.default": membershipPurchaseIntent(
        idempotencySuffix: "811",
        provider: "apple_app_store"
    ),
    "native.memberships.verification.pending.default": Endpoint.createMembershipVerification(
        signedTransaction: "fixture-signed-transaction",
        idempotencyKey: "00000000-0000-7000-8000-000000000807",
        provider: "apple_app_store",
        purchaseIntentId: "00000000-0000-7000-8000-000000000803"
    ),
    "native.memberships.verification-status.pending.default": Endpoint.membershipVerification(
        id: "00000000-0000-7000-8000-000000000807"
    ),
    "native.memberships.verification-status.rejected.default": Endpoint.membershipVerification(
        id: "00000000-0000-7000-8000-000000000810"
    ),
    "native.memberships.verification-status.conflict.default": Endpoint.membershipVerification(
        id: "00000000-0000-7000-8000-000000000809"
    ),
    "native.memberships.verification-status.verified.default": Endpoint.membershipVerification(
        id: "00000000-0000-7000-8000-000000000808"
    )
]
