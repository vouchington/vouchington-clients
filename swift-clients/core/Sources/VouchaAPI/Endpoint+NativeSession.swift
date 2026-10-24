import Foundation

private struct NativeEmailTokenBody: Encodable {
    let emailAddress: String
}

private struct NativeEmailLoginBody: Encodable {
    let emailAddress: String
    let token: String
}

private struct NativeSessionMCPTokenBody: Encodable {
    let clientApp: String

    private enum CodingKeys: String, CodingKey {
        case clientApp = "client_app"
    }
}

/// Native (body-token) email OTP login and session-to-MCP token mint. These builders model the
/// published contract only; no sign-in or settings flow calls them yet.
public extension Endpoint {
    static func requestNativeEmailOTP(email: String) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/auth/native/email-address/tokens",
            body: NativeEmailTokenBody(emailAddress: email),
            bodyKeyEncodingStrategy: .useDefaultKeys
        )
    }

    static func verifyNativeEmailOTP(email: String, code: String) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/auth/native/email-address/login",
            body: NativeEmailLoginBody(emailAddress: email, token: code),
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
