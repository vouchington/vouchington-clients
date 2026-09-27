@testable import VouchaAPI

extension ApiFixtureEndpointCoverageTests {
    static let credentialFixtureEndpoints: [String: Endpoint] = [
        "shared.scopes.catalog": .scopeCatalog,
        "native.my.api-keys.create": .createMyApiKey(
            label: "Coding agent", type: .mcp, permissions: ["mcp.user:read", "mcp.user:write"]
        ),
        "native.my.oauth-grants.paginated": .myOAuthGrants(
            after: "fixture-owner-scoped-oauth-grant-cursor", limit: 1
        ),
        "native.my.oauth-grants.revoke": .revokeMyOAuthGrant(id: "00000000-0000-7000-8000-000000000711")
    ]
}
