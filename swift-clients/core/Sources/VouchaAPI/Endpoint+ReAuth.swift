import Foundation
import VouchaCore

private struct ReAuthCodeBody: Encodable {
    let code: String
}

private struct ReAuthPasskeyOptionsBody: Encodable {
    let passkeyId: String
}

private struct ReAuthPasskeyVerificationBody<Response: Encodable & Sendable>: Encodable {
    let passkeyId: String
    let flowId: String
    let response: Response

    private enum CodingKeys: String, CodingKey {
        case passkeyId = "passkey_id"
        case flowId = "flow_id"
        case response
    }
}

private struct ReAuthProviderVerificationBody: Encodable {
    let flowId: String
    let credential: String
}

private struct ReAuthProviderAuthorizationBody: Encodable {
    let callbackMode: String
    let completionProofChallenge: String
}

private struct ReAuthProviderProofBody: Encodable {
    let flowId: String
}

private struct RefreshSessionBody: Encodable {
    let deviceToken: String
    let sessionToken: String

    private enum CodingKeys: String, CodingKey {
        case deviceToken = "dt"
        case sessionToken = "st"
    }
}

/// Fresh account confirmation (re-auth) and rotating session refresh. These builders model the
/// published contract only; no settings or session flow calls them yet.
public extension Endpoint {
    static var reAuthMethods: Endpoint {
        Endpoint(.GET, path: "/api/v1/auth/re-auth/methods")
    }

    static var requestReAuthEmailToken: Endpoint {
        Endpoint(.POST, path: "/api/v1/auth/mfa/re-auth/email/tokens")
    }

    static func verifyReAuthEmailCode(_ code: String) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/auth/mfa/re-auth/email/verification",
            body: ReAuthEmailCodeBody(code: EncodedEmailVerificationCode(code))
        )
    }

    static func verifyReAuthTOTP(_ code: String) -> Endpoint {
        Endpoint(.POST, path: "/api/v1/auth/mfa/re-auth/totp/verification", body: ReAuthCodeBody(code: code))
    }

    static func reAuthPasskeyOptions(passkeyId: String) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/auth/re-auth/passkeys/options",
            body: ReAuthPasskeyOptionsBody(passkeyId: passkeyId)
        )
    }

    /// `response` is the platform WebAuthn assertion; it keeps its own key spelling.
    static func verifyReAuthPasskey(
        passkeyId: String,
        flowId: String,
        response: some Encodable & Sendable
    ) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/auth/re-auth/passkeys/verification",
            body: ReAuthPasskeyVerificationBody(passkeyId: passkeyId, flowId: flowId, response: response),
            bodyKeyEncodingStrategy: .useDefaultKeys
        )
    }

    static func reAuthProviderChallenge(provider: String) -> Endpoint {
        Endpoint(.POST, path: "/api/v1/auth/re-auth/providers/\(pathSegment(provider))/challenges")
    }

    static func verifyReAuthProvider(provider: String, flowId: String, credential: String) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/auth/re-auth/providers/\(pathSegment(provider))/verification",
            body: ReAuthProviderVerificationBody(flowId: flowId, credential: credential)
        )
    }

    static func beginReAuthProviderAuthorization(provider: String, completionProofChallenge: String) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/auth/re-auth/providers/\(pathSegment(provider))/authorizations",
            body: ReAuthProviderAuthorizationBody(
                callbackMode: "native",
                completionProofChallenge: completionProofChallenge
            )
        )
    }

    static func proveReAuthProvider(provider: String, flowId: String) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/auth/re-auth/providers/\(pathSegment(provider))/proofs",
            body: ReAuthProviderProofBody(flowId: flowId)
        )
    }

    static func refreshSession(deviceToken: String, sessionToken: String) -> Endpoint {
        Endpoint(
            .PATCH,
            path: "/api/v1/session",
            body: RefreshSessionBody(deviceToken: deviceToken, sessionToken: sessionToken)
        )
    }
}

private struct ReAuthEmailCodeBody: Encodable {
    let code: EncodedEmailVerificationCode
}
