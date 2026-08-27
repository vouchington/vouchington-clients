import Foundation

public enum VouchaError: LocalizedError, Sendable {
    case network(URLError)
    case unauthorized
    case forbidden(preconditionCode: String?)
    case notFound
    case api(statusCode: Int, preconditionCode: String?)
    case apiMessage(statusCode: Int, preconditionCode: String?, message: String)
    case decodingFailed(DecodingError)
    case unexpected(String)
    case sessionExpired
    case cancelled

    public var errorDescription: String? {
        switch self {
        case let .network(err): err.localizedDescription
        case .unauthorized: "Sign in to continue."
        case .forbidden: "You don't have permission to do this."
        case .notFound: "Not found."
        case let .api(_, code): code.map { "API error: \($0)" } ?? "An error occurred."
        case let .apiMessage(_, _, message): message
        case let .decodingFailed(err): "Failed to decode response: \(err.localizedDescription)"
        case let .unexpected(message): message
        case .sessionExpired: "Your session has expired. Please sign in again."
        case .cancelled: "Cancelled."
        }
    }

    public static let identityRequired = "IDENTITY_REQUIRED"
    public static let emailVerificationRequired = "EMAIL_VERIFICATION_REQUIRED"
    public static let mfaReauthRequired = "MFA_REAUTH_REQUIRED"
    /// Backend code on the 403 that `verifyCaptchaOrAttestation` throws when the App Attest
    /// Turnstile bypass itself is rejected (e.g. disabled via config) — see
    /// `backend/services/captcha/verify-or-attestation.mts`. Callers use this to distinguish a
    /// bypass rejection (safe to retry with Turnstile) from an unrelated permission 403 (e.g.
    /// `IDENTITY_REQUIRED`), which must surface as a real error instead.
    public static let bypassDisabled = "BYPASS_DISABLED"
    /// Backend code that `verifyAssertion` throws (403, or 409 for a sign-count race) when an
    /// App Attest assertion is cryptographically invalid, replayed, bound to a different device
    /// than the one that attested the key, or otherwise rejected — see
    /// `backend/services/app-attestation/assertion.mts`. Matched by code rather than by raw
    /// status so both the 403 and 409 rejection paths are covered without also matching an
    /// unrelated 401/403/409 from a different endpoint.
    public static let attestationRejected = "ATTESTATION_REJECTED"
    /// Client-synthesized code (no such response ever comes from the backend) that
    /// `SignInService.requestEmailOTP` throws when App Attest is unsupported, the bypass ceremony
    /// fails, or the server rejects it, and no `turnstileToken` was available to fall back to.
    /// Callers use this to show the same "Verification token required" prompt as the pre-flight
    /// gate instead of a raw API error string.
    public static let turnstileTokenRequired = "TURNSTILE_TOKEN_REQUIRED"
    /// Backend code on the 403 that `assertWithinTagAddLimit` throws when a user has hit their
    /// plan's cap on manually-added tags for a subject — see `backend/services/tag-limits/assert.mts`.
    public static let tagLimitReached = "TAG_LIMIT_REACHED"

    /// Whether an App Attest submission attempt failed for a reason that a Turnstile retry can
    /// recover from, rather than a real permission failure: the bypass was rejected server-side
    /// (`bypassDisabled`), or the assertion itself was rejected (`attestationRejected`, covering
    /// invalid/replayed/device-mismatched assertions and sign-count races). Any other
    /// `.forbidden`/`.api` code (e.g. `identityRequired`) is a genuine permission failure and
    /// must not match here.
    public var isRecoverableByTurnstileFallback: Bool {
        switch self {
        case let .forbidden(preconditionCode):
            preconditionCode == VouchaError.bypassDisabled
                || preconditionCode == VouchaError.attestationRejected
        case let .api(_, preconditionCode):
            preconditionCode == VouchaError.attestationRejected
        case let .apiMessage(_, preconditionCode, _):
            preconditionCode == VouchaError.attestationRejected
        default: false
        }
    }

    /// Whether the code means the App Attest key itself was rejected, as opposed to the bypass
    /// being disabled entirely (`bypassDisabled` says nothing about the key's validity). Callers
    /// use this to decide whether to forget the cached key so the next attempt re-attests a fresh
    /// one, rather than retrying a key the server will keep rejecting (e.g. after a device-token
    /// rotation rebinds the `did` the key must match).
    ///
    /// A 409 `attestationRejected` is excluded: per `backend/services/app-attestation/assertion.mts`,
    /// every explicit key-invalid rejection (unknown key, `did` mismatch, disallowed dev key, a
    /// cryptographically invalid or replayed assertion) throws 403. The 409 only fires from
    /// `bumpAttestationSignCount` *after* the assertion has already verified as valid — it means a
    /// concurrent request from the same healthy key committed a higher sign count first, not that
    /// this key is invalid. Clearing the key on a 409 would force needless re-attestation on every
    /// ordering race.
    public var isAttestationKeyRejected: Bool {
        switch self {
        case let .forbidden(preconditionCode): preconditionCode == VouchaError.attestationRejected
        case let .api(statusCode, preconditionCode):
            statusCode != 409 && preconditionCode == VouchaError.attestationRejected
        case let .apiMessage(statusCode, preconditionCode, _):
            statusCode != 409 && preconditionCode == VouchaError.attestationRejected
        default: false
        }
    }

    /// Whether this is the client-synthesized "no Turnstile token and no working App Attest
    /// bypass" failure (`VouchaError.turnstileTokenRequired`), as opposed to a real backend error.
    public var isTurnstileTokenRequired: Bool {
        switch self {
        case let .api(_, preconditionCode): preconditionCode == VouchaError.turnstileTokenRequired
        case let .apiMessage(_, preconditionCode, _): preconditionCode == VouchaError.turnstileTokenRequired
        default: false
        }
    }

    public var isEmailVerificationRequired: Bool {
        switch self {
        case let .forbidden(preconditionCode): preconditionCode == VouchaError.emailVerificationRequired
        case let .api(_, preconditionCode): preconditionCode == VouchaError.emailVerificationRequired
        case let .apiMessage(_, preconditionCode, _): preconditionCode == VouchaError.emailVerificationRequired
        default: false
        }
    }

    /// Whether this is the 403 a user gets from adding a tag past their plan's manual tag cap
    /// (`VouchaError.tagLimitReached`), as opposed to a generic permission failure.
    public var isTagLimitReached: Bool {
        switch self {
        case let .forbidden(preconditionCode): preconditionCode == VouchaError.tagLimitReached
        case let .api(_, preconditionCode): preconditionCode == VouchaError.tagLimitReached
        case let .apiMessage(_, preconditionCode, _): preconditionCode == VouchaError.tagLimitReached
        default: false
        }
    }
}
