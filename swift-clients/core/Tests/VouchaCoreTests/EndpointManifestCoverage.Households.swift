import VouchaAPI

extension EndpointManifestCoverage {
    static let householdEndpoints: [ManifestRegisteredEndpoint] = [
        ManifestRegisteredEndpoint(id: "native.households.empty") { .households() },
        ManifestRegisteredEndpoint(id: "native.households.single-owned") { .households() },
        ManifestRegisteredEndpoint(id: "native.households.multiple") { .households() },
        ManifestRegisteredEndpoint(id: "native.households.owned") { .households(access: .owned, limit: 1) },
        ManifestRegisteredEndpoint(id: "native.households.member.default") {
            .households(access: .member, limit: 1)
        },
        ManifestRegisteredEndpoint(id: "native.households.member.page-2") {
            .households(access: .member, after: memberHouseholdCursor, limit: 1)
        },
        ManifestRegisteredEndpoint(id: "native.household-memberships.empty") {
            .householdMemberships(householdId: householdId)
        },
        ManifestRegisteredEndpoint(id: "native.household-memberships.single") {
            .householdMemberships(householdId: householdId)
        },
        ManifestRegisteredEndpoint(id: "native.household-memberships.multiple") {
            .householdMemberships(householdId: householdId)
        },
        ManifestRegisteredEndpoint(id: "native.household-memberships.page-1") {
            .householdMemberships(householdId: householdId, limit: 1)
        },
        ManifestRegisteredEndpoint(id: "native.household-memberships.page-2") {
            .householdMemberships(householdId: householdId, after: membershipCursor, limit: 1)
        },
        ManifestRegisteredEndpoint(id: "native.households.create.default") { .createHousehold },
        ManifestRegisteredEndpoint(id: "native.household-memberships.delete.default") {
            .deleteHouseholdMembership(householdId: householdId, membershipId: membershipId)
        }
    ]

    private static let householdId = "00000000-0000-7000-8000-000000000101"
    private static let membershipId = "00000000-0000-7000-8000-000000000201"
    private static let memberHouseholdCursor = "eyJ0aW1lc3RhbXAiOiIyMDI2LTA3LTAxVDAwOjAwOjAwLjAwMDAwMFoiLCJpZCI6IjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDEwMiIsInNjb3BlIjoiaG91c2Vob2xkczowMDAwMDAwMC0wMDAwLTcwMDAtODAwMC0wMDAwMDAwMDAwMDE6YWNjZXNzPW1lbWJlcjp1cGRhdGVkX2F0LWRlc2MsaWQtZGVzYyJ9"
    private static let membershipCursor = "eyJ0aW1lc3RhbXAiOiIyMDI2LTA3LTAyVDAwOjAwOjAwLjAwMDAwMFoiLCJpZCI6IjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDIwMSIsInNjb3BlIjoiaG91c2Vob2xkLW1lbWJlcnNoaXBzOjAwMDAwMDAwLTAwMDAtNzAwMC04MDAwLTAwMDAwMDAwMDEwMTp1cGRhdGVkX2F0LWRlc2MsaWQtZGVzYyJ9"
}
