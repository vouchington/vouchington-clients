import Crypto
import Foundation
@testable import VouchaAPI
@testable import VouchaAuth
import XCTest

private final class StubProvider: AppAttestProviding, @unchecked Sendable {
    var isSupported = true
    var assertionResult: Result<Data, Error> = .success(Data("stub-assertion".utf8))
    private(set) var lastAssertionKeyId: String?
    private(set) var lastAssertionClientDataHash: Data?
    private(set) var assertionCallCount = 0

    func generateKey() async throws -> String {
        "stub-key"
    }

    func attestKey(_: String, clientDataHash _: Data) async throws -> Data {
        Data()
    }

    func generateAssertion(_ keyId: String, clientDataHash: Data) async throws -> Data {
        assertionCallCount += 1
        lastAssertionKeyId = keyId
        lastAssertionClientDataHash = clientDataHash
        return try assertionResult.get()
    }
}

private struct StubError: Error {}

final class AppAttestRequestSignerTests: XCTestCase {
    private var provider: StubProvider!
    private var keyStore: AppAttestKeyStore!

    override func setUp() {
        super.setUp()
        provider = StubProvider()
        keyStore = AppAttestKeyStore(serviceName: "ai.voucha.test.\(UUID().uuidString)")
        keyStore.save(keyId: "test-key-id")
    }

    override func tearDown() {
        keyStore.clear()
        super.tearDown()
    }

    private func makeSigner(
        isEnabled: Bool = true,
        now: Date = Date(timeIntervalSince1970: 1_700_000_000),
        nonce: String = "fixed-nonce"
    ) -> AppAttestRequestSigner {
        AppAttestRequestSigner(
            provider: provider,
            keyStore: keyStore,
            isEnabled: { isEnabled },
            now: { now },
            makeNonce: { nonce }
        )
    }

    func testEnabledWithKeyReturnsAllFourHeaders() async throws {
        let headers = try await makeSigner().signingHeaders(method: "GET", path: "/v1/posts", body: nil)

        let h = try XCTUnwrap(headers)
        XCTAssertEqual(h["x-app-attest-key-id"], "test-key-id")
        XCTAssertEqual(h["x-app-attest-assertion"], Data("stub-assertion".utf8).base64EncodedString())
        XCTAssertEqual(h["x-app-attest-timestamp"], "1700000000")
        XCTAssertEqual(h["x-app-attest-nonce"], "fixed-nonce")
    }

    func testDisabledReturnsNil() async throws {
        let headers = try await makeSigner(isEnabled: false).signingHeaders(method: "GET", path: "/", body: nil)
        XCTAssertNil(headers)
        XCTAssertEqual(provider.assertionCallCount, 0)
    }

    func testNoStoredKeyReturnsNil() async throws {
        keyStore.clear()
        let headers = try await makeSigner().signingHeaders(method: "GET", path: "/", body: nil)
        XCTAssertNil(headers)
        XCTAssertEqual(provider.assertionCallCount, 0)
    }

    func testUnsupportedProviderReturnsNil() async throws {
        provider.isSupported = false
        let headers = try await makeSigner().signingHeaders(method: "GET", path: "/", body: nil)
        XCTAssertNil(headers)
        XCTAssertEqual(provider.assertionCallCount, 0)
    }

    func testEnclaveErrorReturnsNilWithoutRethrow() async throws {
        provider.assertionResult = .failure(StubError())
        let headers = try await makeSigner().signingHeaders(method: "PUT", path: "/v1/x", body: nil)
        XCTAssertNil(headers)
    }

    func testAssertionClientDataHashBindsCanonicalString() async throws {
        let body = Data("{}".utf8)
        let bodyHash = CanonicalRequestString.bodyHash(from: body)
        let canonical = CanonicalRequestString.build(
            method: "POST",
            path: "/v1/posts",
            bodyHash: bodyHash,
            timestamp: 1_700_000_000,
            nonce: "fixed-nonce"
        )
        let expectedHash = Data(SHA256.hash(data: Data(canonical.utf8)))

        _ = try await makeSigner().signingHeaders(method: "POST", path: "/v1/posts", body: body)

        XCTAssertEqual(provider.lastAssertionClientDataHash, expectedHash)
        XCTAssertEqual(provider.lastAssertionKeyId, "test-key-id")
    }

    func testTwoCallsProduceDifferentNonces() async throws {
        let signer = AppAttestRequestSigner(
            provider: provider,
            keyStore: keyStore,
            isEnabled: { true }
        )

        let h1 = try await signer.signingHeaders(method: "GET", path: "/v1/posts", body: nil)
        let h2 = try await signer.signingHeaders(method: "GET", path: "/v1/posts", body: nil)

        let nonce1 = try XCTUnwrap(h1?["x-app-attest-nonce"])
        let nonce2 = try XCTUnwrap(h2?["x-app-attest-nonce"])
        XCTAssertNotEqual(nonce1, nonce2)
    }
}
