import Crypto
import Foundation
import VouchaAPI

private struct AppAttestChallengeResponse: Decodable {
    let challengeId: String
    let challenge: String
}

private actor AppAttestationKeyCoordinator {
    private var activeAttestation: Task<String, Error>?

    func attestedKey(
        cachedKey: @Sendable () -> String?,
        createKey: @Sendable @escaping () async throws -> String
    ) async throws -> String {
        if let keyId = cachedKey() {
            return keyId
        }
        if let activeAttestation {
            return try await activeAttestation.value
        }
        let task = Task { try await createKey() }
        activeAttestation = task
        do {
            let keyId = try await task.value
            activeAttestation = nil
            return keyId
        } catch {
            activeAttestation = nil
            throw error
        }
    }
}

private func sha256(_ data: Data) -> Data {
    Data(SHA256.hash(data: data))
}

/// Hardware-backed device identity via Apple App Attest. On supported devices this lets protected
/// requests attach a signed assertion instead of running the Turnstile CAPTCHA WebView challenge.
public struct AppAttestationService: Sendable {
    private let client: APIClient
    private let provider: any AppAttestProviding
    private let keyStore: AppAttestKeyStore
    private let keyCoordinator: AppAttestationKeyCoordinator

    public init(
        client: APIClient,
        provider: any AppAttestProviding,
        keyStore: AppAttestKeyStore = AppAttestKeyStore()
    ) {
        self.client = client
        self.provider = provider
        self.keyStore = keyStore
        keyCoordinator = AppAttestationKeyCoordinator()
    }

    /// Whether this device/OS can perform App Attest. Callers should fall back to
    /// `NativeTurnstileChallengeView` when this is `false` (Simulator, Intel Mac w/o T2, old OS).
    public var isSupported: Bool {
        provider.isSupported
    }

    /// Stable App Attest device identifier for this install. Returns `nil` when unsupported,
    /// otherwise reuses the attested App Attest key ID path and generates/attests/stores the key
    /// on first use.
    public func deviceId() async throws -> String? {
        guard isSupported else { return nil }
        return try await ensureAttestedKey()
    }

    /// Shared composition helper for view models that self-construct an App Attest dependency
    /// rather than threading it through their initializer. Returns `nil` without `client` or on
    /// platforms where `DeviceCheck` isn't available (e.g. Linux CI, non-Darwin test hosts).
    public static func makeDefault(
        client: APIClient?,
        environment: [String: String] = ProcessInfo.processInfo.environment
    ) -> AppAttestationService? {
        guard let client else { return nil }
        #if DEBUG
            guard environment["VOUCHA_DISABLE_APP_ATTEST"] != "1" else { return nil }
        #endif
        #if canImport(DeviceCheck)
            return AppAttestationService(client: client, provider: DeviceCheckAppAttestProvider())
        #else
            return nil
        #endif
    }

    /// Headers to attach to a protected request in place of a Turnstile token, attesting a key
    /// first if this is the device's first protected request. Returns `nil` when unsupported.
    ///
    /// `actionTag` binds the assertion to one gated endpoint so a captured assertion can't be
    /// replayed against a different one; the single-use challenge nonce is the replay defense,
    /// so the signed payload is a fixed `"challengeId:actionTag"` string rather than a hash of
    /// the request body — matching `verifyCaptchaOrAttestation` on the backend, which passes
    /// that same unhashed string to `node-app-attest` (it SHA-256s the payload internally once).
    public func assertionHeaders(actionTag: AppAttestActionTag) async throws -> [String: String]? {
        guard isSupported else { return nil }
        var keyId = try await ensureAttestedKey()
        let challengeResponse: AppAttestChallengeResponse = try await client.send(
            .appAttestationChallenge(type: "assertion")
        )
        let payload = "\(challengeResponse.challengeId):\(actionTag.rawValue)"
        let clientDataHash = sha256(Data(payload.utf8))
        let assertion: Data
        do {
            assertion = try await provider.generateAssertion(keyId, clientDataHash: clientDataHash)
        } catch {
            // `keyId` may reference a Secure Enclave key that no longer exists locally (e.g. after
            // a Keychain reset wiped the key but not this cached ID) — clear it, re-attest once
            // with a freshly generated key, and retry the assertion against the same challenge.
            keyStore.clear()
            keyId = try await ensureAttestedKey()
            assertion = try await provider.generateAssertion(keyId, clientDataHash: clientDataHash)
        }
        return [
            "x-app-attest-key-id": keyId,
            "x-app-attest-assertion": assertion.base64EncodedString(),
            "x-app-attest-challenge-id": challengeResponse.challengeId
        ]
    }

    /// Clears any cached attested key. Callers should invoke this when the backend rejects an
    /// assertion for a reason that means the key itself is no longer valid for this caller
    /// (`VouchaError.attestationRejected` — e.g. the key is bound to a `did` a device-token
    /// rotation has since replaced), as opposed to the bypass being disabled entirely
    /// (`VouchaError.bypassDisabled`, which says nothing about the key). Without this, a rejected
    /// key would be retried indefinitely on every subsequent protected request.
    public func forgetCachedKey() {
        keyStore.clear()
    }

    private func ensureAttestedKey() async throws -> String {
        try await keyCoordinator.attestedKey(
            cachedKey: { keyStore.loadKeyId() },
            createKey: { try await attestFreshKey() }
        )
    }

    private func attestFreshKey() async throws -> String {
        let keyId = try await provider.generateKey()
        let challengeResponse: AppAttestChallengeResponse = try await client.send(
            .appAttestationChallenge(type: "attestation")
        )
        let clientDataHash = sha256(Data(challengeResponse.challenge.utf8))
        let attestation = try await provider.attestKey(keyId, clientDataHash: clientDataHash)
        let _: EmptyResponse = try await client.send(
            .appAttestationAttest(
                keyId: keyId,
                attestation: attestation.base64EncodedString(),
                challengeId: challengeResponse.challengeId
            )
        )
        keyStore.save(keyId: keyId)
        return keyId
    }
}
