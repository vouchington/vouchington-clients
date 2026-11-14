@testable import VouchaAPI

let nativeSessionFixtureEndpoints: [String: Endpoint] = nativeSessionBaseFixtureEndpoints
    .merging(nativeReAuthFixtureEndpoints) { _, replacement in replacement }

private let nativeSessionBaseFixtureEndpoints: [String: Endpoint] = [
    "native.auth.email-address.tokens": .requestNativeEmailOTP(email: "fixture@example.test"),
    "native.auth.email-address.login.authenticated": .verifyNativeEmailOTP(
        email: "fixture@example.test", code: "ABCDEFGHJK"
    ),
    "native.auth.email-address.login.mfa": .verifyNativeEmailOTP(
        email: "fixture@example.test", code: "ABCDEFGHJK"
    ),
    "native.oauth.session-mcp-token.ios": .mintNativeSessionMCPToken(app: .ios)
]
