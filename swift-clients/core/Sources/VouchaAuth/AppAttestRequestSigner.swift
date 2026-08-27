import Foundation
import VouchaAPI
#if canImport(CryptoKit)
    import CryptoKit
#endif
#if canImport(Security)
    import Security
#endif

/// Per-request App Attest signer. Calls `provider.generateAssertion` directly (network-free,
/// local Secure Enclave only) to avoid a challenge round-trip inside the `APIClient` actor.
/// Returns `nil` (passthrough — request is sent unsigned) when disabled, unsupported,
/// no key stored, or on any enclave error.
public struct AppAttestRequestSigner: RequestSigning {
    let provider: any AppAttestProviding
    let keyStore: AppAttestKeyStore
    let isEnabled: @Sendable () -> Bool
    let now: @Sendable () -> Date
    let makeNonce: @Sendable () -> String

    public init(
        provider: any AppAttestProviding,
        keyStore: AppAttestKeyStore,
        isEnabled: @escaping @Sendable () -> Bool,
        now: @escaping @Sendable () -> Date = { Date() },
        makeNonce: @escaping @Sendable () -> String = {
            #if canImport(Security)
                var bytes = [UInt8](repeating: 0, count: 16)
                _ = SecRandomCopyBytes(kSecRandomDefault, bytes.count, &bytes)
                return bytes.map { String(format: "%02x", $0) }.joined()
            #else
                return UUID().uuidString
            #endif
        }
    ) {
        self.provider = provider
        self.keyStore = keyStore
        self.isEnabled = isEnabled
        self.now = now
        self.makeNonce = makeNonce
    }

    public func signingHeaders(
        method: String,
        path: String,
        body: Data?
    ) async throws -> [String: String]? {
        guard isEnabled() else { return nil }
        guard provider.isSupported else { return nil }
        guard let keyId = keyStore.loadKeyId() else { return nil }

        let timestamp = Int(now().timeIntervalSince1970)
        let nonce = makeNonce()
        let bodyHash = CanonicalRequestString.bodyHash(from: body)
        let canonical = CanonicalRequestString.build(
            method: method,
            path: path,
            bodyHash: bodyHash,
            timestamp: timestamp,
            nonce: nonce
        )

        guard let canonicalData = canonical.data(using: .utf8) else { return nil }

        do {
            let assertion = try await provider.generateAssertion(keyId, clientDataHash: sha256(canonicalData))
            return [
                RequestSignatureHeader.keyId: keyId,
                RequestSignatureHeader.assertion: assertion.base64EncodedString(),
                RequestSignatureHeader.timestamp: String(timestamp),
                RequestSignatureHeader.nonce: nonce
            ]
        } catch {
            return nil
        }
    }
}

private func sha256(_ data: Data) -> Data {
    #if canImport(CryptoKit)
        return Data(SHA256.hash(data: data))
    #else
        preconditionFailure("App Attest requires CryptoKit, which is unavailable on this platform")
    #endif
}
