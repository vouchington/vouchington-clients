private struct NativeEmailTokenBody: Encodable {
    let emailAddress: String
}

private struct NativeEmailLoginBody: Encodable {
    let emailAddress: String
    let token: String
}

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

    static func mintNativeSessionMCPToken(clientApp: String) -> Endpoint {
        Endpoint(
            .POST, path: "/api/v1/oauth/native-session-tokens",
            headers: ["x-voucha-native-mcp-mint": "1"], body: ["client_app": clientApp]
        )
    }
}
