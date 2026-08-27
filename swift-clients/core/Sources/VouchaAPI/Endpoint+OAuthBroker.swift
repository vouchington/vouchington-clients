import VouchaCore

private struct BeginNativeOAuthAuthorizationBody: Encodable {
    let purpose: NativeOAuthAuthorizationPurpose
    let callbackMode = "native"
    let completionProofChallenge: String
}

private struct CompleteNativeOAuthAuthorizationBody: Encodable {
    let completionToken: String
    let completionProofVerifier: String
}

public extension Endpoint {
    static var oauthProviders: Endpoint {
        Endpoint(.GET, path: "/api/v1/auth/oauth/providers")
    }

    static func beginNativeOAuthAuthorization(
        provider: NativeOAuthProvider,
        purpose: NativeOAuthAuthorizationPurpose,
        completionProofChallenge: String
    ) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/auth/oauth/\(pathSegment(provider.rawValue))/authorizations",
            body: BeginNativeOAuthAuthorizationBody(
                purpose: purpose,
                completionProofChallenge: completionProofChallenge
            )
        )
    }

    static func completeNativeOAuthAuthorization(
        flowId: String,
        completionToken: String,
        completionProofVerifier: String
    ) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/auth/oauth/authorizations/\(pathSegment(flowId))/complete",
            body: CompleteNativeOAuthAuthorizationBody(
                completionToken: completionToken,
                completionProofVerifier: completionProofVerifier
            )
        )
    }

    static func disconnectOAuthAccount(provider: NativeOAuthProvider) -> Endpoint {
        Endpoint(
            .DELETE,
            path: "/api/v1/auth/oauth/\(pathSegment(provider.rawValue))/connect"
        )
    }
}
