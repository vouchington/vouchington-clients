import VouchaAPI

extension EndpointManifestCoverage {
    static let membershipStoreEndpoints: [ManifestRegisteredEndpoint] =
        membershipStoreFixtureEndpoints.map { id, endpoint in
            ManifestRegisteredEndpoint(id: id) { endpoint }
        } + [
            ManifestRegisteredEndpoint(id: "native.memberships.microsoft.service-tickets.default") {
                Endpoint.microsoftStoreServiceTickets
            },
            ManifestRegisteredEndpoint(id: "web.memberships.refund.completed") {
                membershipRefundManifestEndpoint
            },
            ManifestRegisteredEndpoint(id: "web.memberships.refund.reconciling") {
                membershipRefundManifestEndpoint
            }
        ]
}

private let membershipRefundManifestEndpoint = Endpoint.createMembershipRefund(
    MembershipRefundBody(
        cancel: false,
        chargeId: "ch_fixture_refund",
        idempotencyKey: "00000000-0000-7000-8000-000000000901",
        invoiceId: "in_fixture_refund",
        reason: "goodwill",
        userId: "019fafb8-a44c-73e2-890a-497ff3dd27a6"
    )
)
