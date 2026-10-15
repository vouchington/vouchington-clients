import Foundation
import VouchaAuth

/// UI-test double for `AppAttestProviding`.
/// Core tests keep separate doubles because those record call counts and synchronization gates.
final class StubAppAttestProvider: AppAttestProviding, @unchecked Sendable {
    var isSupported = true
    var generateKeyResult: Result<String, Error> = .success("stub-key-id")
    var attestKeyResult: Result<Data, Error> = .success(Data("attestation-blob".utf8))
    var generateAssertionResult: Result<Data, Error> = .success(Data("assertion-blob".utf8))

    func generateKey() async throws -> String {
        try generateKeyResult.get()
    }

    func attestKey(_: String, clientDataHash _: Data) async throws -> Data {
        try attestKeyResult.get()
    }

    func generateAssertion(_: String, clientDataHash _: Data) async throws -> Data {
        try generateAssertionResult.get()
    }
}
