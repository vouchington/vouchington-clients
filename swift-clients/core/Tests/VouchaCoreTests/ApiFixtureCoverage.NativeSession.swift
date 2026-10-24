import VouchaModels

let nativeSessionFixtureCoverage: [RegisteredFixture] = [
    RegisteredFixture(id: "native.auth.email-address.tokens") {
        try assertFixtureCoversDTO($0, as: NativeEmailTokenResponse.self)
    },
    RegisteredFixture(id: "native.auth.email-address.login.authenticated") {
        try assertFixtureCoversDTO($0, as: NativeEmailLoginAuthenticatedResponse.self)
    },
    RegisteredFixture(id: "native.auth.email-address.login.mfa") {
        try assertFixtureCoversDTO($0, as: NativeEmailLoginMFAResponse.self)
    },
    RegisteredFixture(id: "native.oauth.session-mcp-token.ios") {
        try assertFixtureCoversDTO($0, as: NativeSessionMCPTokenResponse.self)
    }
]
