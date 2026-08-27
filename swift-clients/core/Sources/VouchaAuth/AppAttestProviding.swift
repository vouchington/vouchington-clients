import Foundation

/// Abstraction over `DCAppAttestService` so `AppAttestationService` is testable without real
/// attestation hardware. `DeviceCheckAppAttestProvider` below is the only conformer allowed to
/// import DeviceCheck (enforced by ast-grep-rules/swift-devicecheck-in-auth-only.yml).
public protocol AppAttestProviding: Sendable {
    var isSupported: Bool { get }
    func generateKey() async throws -> String
    func attestKey(_ keyId: String, clientDataHash: Data) async throws -> Data
    func generateAssertion(_ keyId: String, clientDataHash: Data) async throws -> Data
}

#if canImport(DeviceCheck)
    import DeviceCheck

    public struct DeviceCheckAppAttestProvider: AppAttestProviding {
        public init() {}

        public var isSupported: Bool {
            DCAppAttestService.shared.isSupported
        }

        public func generateKey() async throws -> String {
            try await DCAppAttestService.shared.generateKey()
        }

        public func attestKey(_ keyId: String, clientDataHash: Data) async throws -> Data {
            try await DCAppAttestService.shared.attestKey(keyId, clientDataHash: clientDataHash)
        }

        public func generateAssertion(_ keyId: String, clientDataHash: Data) async throws -> Data {
            try await DCAppAttestService.shared.generateAssertion(keyId, clientDataHash: clientDataHash)
        }
    }
#endif
