@testable import VouchaAPI

private let reAuthFlowId = "00000000-0000-7000-8000-000000000742"
private let reAuthPasskeyId = "00000000-0000-7000-8000-000000000741"

struct ReAuthPasskeyAssertion: Encodable {
    struct Inner: Encodable {
        let authenticatorData: String
        let clientDataJSON: String
        let signature: String
    }

    struct Extensions: Encodable {}

    let clientExtensionResults = Extensions()
    let id = "dm91Y2hhLXN5bnRoZXRpYy1wYXNza2V5LWNyZWRlbnRpYWw"
    let rawId = "dm91Y2hhLXN5bnRoZXRpYy1wYXNza2V5LWNyZWRlbnRpYWw"
    let response = Inner(
        authenticatorData: "AQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQ",
        clientDataJSON: "eyJ0eXBlIjoid2ViYXV0aG4uZ2V0IiwiY2hhbGxlbmdlIjoiUWtKQ1FrSkNRa0pDUWtKQ1FrSkNRa0pDUWtKQ1FrSkNRa0pDUWtKQ1FrSSIsIm9yaWdpbiI6Imh0dHBzOi8vdm91Y2hhLmFpIn0",
        signature: "AgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAg"
    )
    let type = "public-key"
}

let nativeReAuthFixtureEndpoints: [String: Endpoint] = [
    "native.auth.re-auth.methods.default": .reAuthMethods,
    "native.auth.re-auth.email.tokens": .requestReAuthEmailToken,
    "native.auth.re-auth.email.verification": .verifyReAuthEmailCode("ABCDEFGHJK"),
    "native.auth.re-auth.totp.verification": .verifyReAuthTOTP("123456"),
    "native.auth.re-auth.passkeys.options": .reAuthPasskeyOptions(passkeyId: reAuthPasskeyId),
    "native.auth.re-auth.passkeys.verification": .verifyReAuthPasskey(
        passkeyId: reAuthPasskeyId, flowId: reAuthFlowId, response: ReAuthPasskeyAssertion()
    ),
    "native.auth.re-auth.providers.challenge.google": .reAuthProviderChallenge(provider: "google"),
    "native.auth.re-auth.providers.verification.google": .verifyReAuthProvider(
        provider: "google", flowId: reAuthFlowId, credential: "fixture-provider-credential"
    ),
    "native.auth.re-auth.providers.authorization.github": .beginReAuthProviderAuthorization(
        provider: "github", completionProofChallenge: String(repeating: "Z", count: 43)
    ),
    "native.auth.re-auth.providers.proof.github": .proveReAuthProvider(provider: "github", flowId: reAuthFlowId),
    "native.auth.session.refresh.rotated": .refreshSession(
        deviceToken: "fixture-previous-device-jwt", sessionToken: "fixture-previous-session-jwt"
    ),
    "native.bot-challenge.required": .shareRssFeedItemWithFollowers(
        rssFeedItemId: "00000000-0000-7000-8000-000000000743"
    )
]
