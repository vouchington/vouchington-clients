@testable import VouchaAPI

let nativeSessionFixtureEndpoints: [String: Endpoint] = [
    "native.auth.email-address.tokens": .requestNativeEmailOTP(email: "fixture@example.test"),
    "native.auth.email-address.login.authenticated": .verifyNativeEmailOTP(
        email: "fixture@example.test", code: "123456"
    ),
    "native.auth.email-address.login.mfa": .verifyNativeEmailOTP(
        email: "fixture@example.test", code: "123456"
    ),
    "native.oauth.session-mcp-token.ios": .mintNativeSessionMCPToken(app: .ios)
]
