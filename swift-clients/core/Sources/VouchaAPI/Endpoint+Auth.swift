private struct PasskeyAuthVerifyBody<Response: Encodable & Sendable>: Encodable {
    let response: Response
}

private struct RequestEmailOTPBody: Encodable {
    let emailAddress: String
    let cfTurnstileResponse: String?
    let uiLocale: String?

    private enum CodingKeys: String, CodingKey {
        case emailAddress
        case cfTurnstileResponse
        case uiLocale = "ui_locale"
    }
}

public extension Endpoint {
    /// `turnstileToken` is `nil` when the caller is instead attaching App Attest headers via
    /// `withHeaders(_:)` in place of the Turnstile challenge.
    static func requestEmailOTP(
        email: String,
        turnstileToken: String? = nil,
        uiLocale: String? = nil
    ) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/auth/email-address/tokens",
            body: RequestEmailOTPBody(
                emailAddress: email,
                cfTurnstileResponse: turnstileToken,
                uiLocale: uiLocale
            )
        )
    }

    static func verifyEmailOTP(email: String, code: String) -> Endpoint {
        Endpoint(.POST, path: "/api/v1/auth/email-address/login", body: ["emailAddress": email, "token": code])
    }

    static func verifyMFATOTP(loginAttemptId: String, code: String) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/auth/mfa/totp/verification",
            body: ["login_attempt_id": loginAttemptId, "code": code]
        )
    }

    static var passkeyAuthOptions: Endpoint {
        Endpoint(.POST, path: "/api/v1/auth/passkeys/authentication/options")
    }

    static var passkeyAuthVerify: Endpoint {
        Endpoint(.POST, path: "/api/v1/auth/passkeys/authentication/verify")
    }

    static func passkeyAuthVerify(response: some Encodable & Sendable) -> Endpoint {
        Endpoint(
            .POST,
            path: "/api/v1/auth/passkeys/authentication/verify",
            body: PasskeyAuthVerifyBody(response: response)
        )
    }
}
