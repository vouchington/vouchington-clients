import VouchaModels

let credentialFixtureCoverage: [RegisteredFixture] = [
    RegisteredFixture(id: "shared.scopes.catalog") {
        try assertFixtureCoversDTO($0, as: ScopeCatalogResponse.self)
    },
    RegisteredFixture(id: "native.my.api-keys.create") {
        try assertFixtureCoversDTO(
            $0,
            as: ApiKeyCreationResponse.self,
            ignoring: ["api_key.last_used_at", "api_key.revoked_at"]
        )
    },
    RegisteredFixture(id: "native.my.oauth-grants.paginated") {
        try assertFixtureCoversDTO($0, as: Page<OAuthGrant>.self)
    }
]
