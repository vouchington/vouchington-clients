import VouchaModels

private let passkeyOptionsIgnoredPaths: Set<String> = [
    "options.rpId", "options.userVerification", "options.allowCredentials",
    "options.allowCredentials.id", "options.allowCredentials.transports", "options.allowCredentials.type"
]

let nativeReAuthFixtureCoverage: [RegisteredFixture] = [
    RegisteredFixture(id: "native.auth.re-auth.methods.default") {
        try assertFixtureCoversDTO($0, as: ReAuthMethodsResponse.self)
    },
    RegisteredFixture(id: "native.auth.re-auth.email.tokens") {
        try assertFixtureCoversDTO($0, as: NativeEmailTokenResponse.self)
    },
    RegisteredFixture(id: "native.auth.re-auth.email.verification") {
        try assertFixtureCoversDTO($0, as: ReAuthTokenResponse.self)
    },
    RegisteredFixture(id: "native.auth.re-auth.totp.verification") {
        try assertFixtureCoversDTO($0, as: ReAuthTokenResponse.self)
    },
    RegisteredFixture(id: "native.auth.re-auth.passkeys.verification") {
        try assertFixtureCoversDTO($0, as: ReAuthTokenResponse.self)
    },
    RegisteredFixture(id: "native.auth.re-auth.providers.verification.google") {
        try assertFixtureCoversDTO($0, as: ReAuthTokenResponse.self)
    },
    RegisteredFixture(id: "native.auth.re-auth.providers.proof.github") {
        try assertFixtureCoversDTO($0, as: ReAuthTokenResponse.self)
    },
    RegisteredFixture(id: "native.auth.re-auth.passkeys.options") {
        try assertFixtureCoversDTO($0, as: ReAuthPasskeyOptionsResponse.self, ignoring: passkeyOptionsIgnoredPaths)
    },
    RegisteredFixture(id: "native.auth.re-auth.providers.challenge.google") {
        try assertFixtureCoversDTO($0, as: ReAuthProviderChallengeResponse.self)
    },
    RegisteredFixture(id: "native.auth.re-auth.providers.authorization.github") {
        try assertFixtureCoversDTO($0, as: BeginNativeOAuthAuthorizationResponse.self)
    },
    RegisteredFixture(id: "native.auth.session.refresh.rotated") {
        try assertFixtureCoversDTO($0, as: NativeSessionRefreshResponse.self)
    },
    RegisteredFixture(id: "native.bot-challenge.required") {
        try assertFixtureCoversDTO($0, as: BotChallengeRequiredResponse.self)
    }
]
