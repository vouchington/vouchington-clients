import Foundation
import VouchaCore

private struct NativeEmailTokenBody: Encodable {
    let emailAddress: String
    let cfTurnstileResponse: String?
    let uiLocale: String?

    private enum CodingKeys: String, CodingKey {
        case emailAddress
        case cfTurnstileResponse
        case uiLocale = "ui_locale"
    }
}

private struct NativeEmailLoginBody: Encodable {
    let emailAddress: String
    let token: EncodedEmailVerificationCode
}

private struct NativeSessionMCPTokenBody: Encodable {
    let clientApp: String

    private enum CodingKeys: String, CodingKey {
        case clientApp = "client_app"
    }
}

/// Native (body-token) email OTP login and session-to-MCP token mint. Native routes reject browser
/// `Origin` and `Sec-Fetch-*` headers; `APIClient` never adds them.
public extension Endpoint {
    /// `turnstileToken` is `nil` when the caller attaches App Attest headers via `withHeaders(_:)`.
    static func requestNativeEmailOTP(
        email: String,
        turnstileToken: String? = nil,
        uiLocale: String? = nil
    ) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/auth/native/email-address/tokens",
            body: NativeEmailTokenBody(
                emailAddress: email, cfTurnstileResponse: turnstileToken, uiLocale: uiLocale
            ),
            bodyKeyEncodingStrategy: .useDefaultKeys
        )
    }

    static func verifyNativeEmailOTP(email: String, code: String) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/auth/native/email-address/login",
            body: NativeEmailLoginBody(emailAddress: email, token: EncodedEmailVerificationCode(code)),
            bodyKeyEncodingStrategy: .useDefaultKeys
        )
    }

    static func mintNativeSessionMCPToken(app: MemberMCPApp) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/oauth/native-session-tokens",
            headers: ["x-voucha-native-mcp-mint": "1"],
            body: NativeSessionMCPTokenBody(clientApp: app.rawValue),
            bodyKeyEncodingStrategy: .useDefaultKeys
        )
    }
}
