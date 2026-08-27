import VouchaAPI
import VouchaCore
import VouchaModels
#if canImport(AuthenticationServices)
    import AuthenticationServices
#endif

// MARK: - Response models (private)

private struct AuthResponse: Decodable {
    let mfaRequired: Bool?
    let loginAttemptId: String?
}

/// Service for all authentication flows: email OTP, Apple Sign In, and MFA TOTP.
public struct SignInService: Sendable {
    private let client: APIClient
    private let sessionManager: SessionManager
    private let appAttestationService: AppAttestationService?

    /// Creates a sign-in service backed by the given client and session manager.
    public init(
        client: APIClient,
        sessionManager: SessionManager,
        appAttestationService: AppAttestationService? = nil
    ) {
        self.client = client
        self.sessionManager = sessionManager
        self.appAttestationService = appAttestationService ?? AppAttestationService.makeDefault(client: client)
    }

    // MARK: - Email OTP

    /// Whether App Attest can stand in for a Turnstile token on `requestEmailOTP`. UI layers use
    /// this to relax the "Verification token required" gate on supported devices.
    public var canBypassTurnstileWithAppAttest: Bool {
        appAttestationService?.isSupported == true
    }

    /// Step 1: request a one-time code sent to the user's email. Tries App Attest first when
    /// supported; falls back to `turnstileToken` when App Attest is unsupported, attestation/
    /// assertion fails, or the server rejects the bypass (e.g. disabled server-side).
    public func requestEmailOTP(email: String, turnstileToken: String?, uiLocale: String? = nil) async throws {
        if let appAttestationService,
           let headers = try? await appAttestationService.assertionHeaders(actionTag: .authEmailAddressTokens) {
            do {
                let _: EmptyResponse = try await client.send(
                    .requestEmailOTP(email: email, uiLocale: uiLocale).withHeaders(headers)
                )
                return
            } catch let error as VouchaError where error.isRecoverableByTurnstileFallback {
                if error.isAttestationKeyRejected {
                    appAttestationService.forgetCachedKey()
                }
                // Bypass disabled, key unknown/development-only, or sign-count regression;
                // fall through to Turnstile.
            }
        }
        guard let turnstileToken else {
            throw VouchaError.api(statusCode: 0, preconditionCode: VouchaError.turnstileTokenRequired)
        }
        let _: EmptyResponse = try await client.send(
            .requestEmailOTP(email: email, turnstileToken: turnstileToken, uiLocale: uiLocale)
        )
    }

    /// Step 2: verify the OTP code.
    ///
    /// Returns `nil` on success (session cookies set).
    /// Returns an `MFAChallenge` when the account requires TOTP verification.
    @MainActor
    public func verifyEmailOTP(email: String, code: String) async throws -> MFAChallenge? {
        let response: AuthResponse = try await client.send(.verifyEmailOTP(email: email, code: code))
        return try await handleAuthResponse(response)
    }

    // MARK: - Apple Sign In

    /// Sign in with an Apple identity token.
    ///
    /// Returns `nil` on success (session cookies set).
    /// Returns an `MFAChallenge` when the account requires TOTP verification.
    @MainActor
    public func signInWithApple(
        identityToken: String,
        nonce: String?,
        userName: String?
    ) async throws -> MFAChallenge? {
        let response: AuthResponse = try await client.send(
            .appleSignIn(token: identityToken, nonce: nonce, userName: userName)
        )
        return try await handleAuthResponse(response)
    }

    // swiftformat:disable indent
    #if canImport(AuthenticationServices)
    @MainActor
    public func signInWithPasskey(presentationAnchor: ASPresentationAnchor) async throws -> MFAChallenge? {
        let optionsResponse: PasskeyAuthenticationOptionsResponse = try await client.send(.passkeyAuthOptions)
        let assertion = try await PasskeyAssertionController(presentationAnchor: presentationAnchor)
            .assertion(for: optionsResponse.options)
        let response: AuthResponse = try await client.send(.passkeyAuthVerify(response: assertion))
        return try await handleAuthResponse(response)
    }
    #endif
    // swiftformat:enable indent

    // MARK: - MFA TOTP

    /// Complete MFA verification using a TOTP code.
    @MainActor
    public func verifyMFATOTP(loginAttemptId: String, code: String) async throws {
        let _: EmptyResponse = try await client.send(.verifyMFATOTP(loginAttemptId: loginAttemptId, code: code))
        await sessionManager.refresh()
        guard sessionManager.isSignedIn else {
            throw VouchaError.unauthorized
        }
    }

    // MARK: - Private helpers

    /// Parses an `AuthResponse` and either completes sign-in or surfaces an MFA challenge.
    @MainActor
    private func handleAuthResponse(_ response: AuthResponse) async throws -> MFAChallenge? {
        if response.mfaRequired == true {
            guard let attemptId = response.loginAttemptId else {
                throw VouchaError.api(statusCode: 0, preconditionCode: "MISSING_LOGIN_ATTEMPT_ID")
            }
            return MFAChallenge(loginAttemptId: attemptId)
        }
        await sessionManager.refresh()
        guard sessionManager.isSignedIn else {
            throw VouchaError.unauthorized
        }
        return nil
    }
}
