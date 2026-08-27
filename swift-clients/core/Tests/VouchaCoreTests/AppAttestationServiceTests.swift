import CryptoKit
import Foundation
@testable import VouchaAPI
@testable import VouchaAuth
@testable import VouchaCore
import XCTest

private final class StubAppAttestProvider: AppAttestProviding, @unchecked Sendable {
    var isSupported = true
    var generateKeyResult: Result<String, Error> = .success("stub-key-id")
    var attestKeyResult: Result<Data, Error> = .success(Data("attestation-blob".utf8))
    var generateAssertionResult: Result<Data, Error> = .success(Data("assertion-blob".utf8))
    var generateKeyGate: GenerateKeyGate?
    /// Number of leading `generateAssertion` calls that throw `StubError` before falling back to
    /// `generateAssertionResult` — lets tests simulate a stale Secure Enclave key that fails once
    /// and then succeeds after `AppAttestationService` clears and re-attests.
    var generateAssertionFailureCount = 0
    private(set) var generateKeyCallCount = 0
    private(set) var generateAssertionCallCount = 0
    private(set) var lastAttestKeyId: String?
    private(set) var lastAttestClientDataHash: Data?
    private(set) var lastAssertionKeyId: String?
    private(set) var lastAssertionClientDataHash: Data?

    func generateKey() async throws -> String {
        generateKeyCallCount += 1
        if let generateKeyGate {
            await generateKeyGate.signalEntered()
            await generateKeyGate.waitUntilReleased()
        }
        return try generateKeyResult.get()
    }

    func attestKey(_ keyId: String, clientDataHash: Data) async throws -> Data {
        lastAttestKeyId = keyId
        lastAttestClientDataHash = clientDataHash
        return try attestKeyResult.get()
    }

    func generateAssertion(_ keyId: String, clientDataHash: Data) async throws -> Data {
        generateAssertionCallCount += 1
        lastAssertionKeyId = keyId
        lastAssertionClientDataHash = clientDataHash
        if generateAssertionCallCount <= generateAssertionFailureCount {
            throw StubError()
        }
        return try generateAssertionResult.get()
    }
}

private struct StubError: Error {}

private actor GenerateKeyGate {
    private var isEntered = false
    private var isReleased = false
    private var enteredWaiters: [CheckedContinuation<Void, Never>] = []
    private var releasedWaiters: [CheckedContinuation<Void, Never>] = []

    func signalEntered() {
        isEntered = true
        let waiters = enteredWaiters
        enteredWaiters.removeAll()
        waiters.forEach { $0.resume() }
    }

    func waitUntilEntered() async {
        if isEntered {
            return
        }
        await withCheckedContinuation { continuation in
            enteredWaiters.append(continuation)
        }
    }

    func release() {
        isReleased = true
        let waiters = releasedWaiters
        releasedWaiters.removeAll()
        waiters.forEach { $0.resume() }
    }

    func waitUntilReleased() async {
        if isReleased {
            return
        }
        await withCheckedContinuation { continuation in
            releasedWaiters.append(continuation)
        }
    }
}

final class AppAttestationServiceTests: XCTestCase {
    private var provider: StubAppAttestProvider!
    private var keyStore: AppAttestKeyStore!

    override func setUp() {
        super.setUp()
        provider = StubAppAttestProvider()
        // Unique service name prevents cross-test keychain pollution.
        keyStore = AppAttestKeyStore(serviceName: "ai.voucha.test.\(UUID().uuidString)")
        CapturingURLProtocol.responseData = Data(#"{"challengeId":"chal-1","challenge":"nonce-abc"}"#.utf8)
        CapturingURLProtocol.responseStatusCode = 200
        CapturingURLProtocol.lastRequestURL = nil
    }

    override func tearDown() {
        keyStore.clear()
        CapturingURLProtocol.responseData = Data("{}".utf8)
        CapturingURLProtocol.responseStatusCode = 200
        CapturingURLProtocol.lastRequestURL = nil
        super.tearDown()
    }

    private func makeService() throws -> AppAttestationService {
        let client = try APIClient(
            config: AppConfig(
                baseURL: XCTUnwrap(URL(string: "http://localhost:2999")),
                turnstileSiteKey: "test-site-key"
            ),
            cookieStorage: HTTPCookieStorage(),
            protocolClasses: [CapturingURLProtocol.self]
        )
        return AppAttestationService(client: client, provider: provider, keyStore: keyStore)
    }

    func testIsSupportedReflectsProvider() throws {
        provider.isSupported = false
        XCTAssertFalse(try makeService().isSupported)

        provider.isSupported = true
        XCTAssertTrue(try makeService().isSupported)
    }

    func testMakeDefaultDisablesAppAttestForUITestingEnvironment() throws {
        let client = try APIClient(
            config: AppConfig(baseURL: XCTUnwrap(URL(string: "https://ui-testing.voucha.invalid"))),
            cookieStorage: HTTPCookieStorage()
        )

        XCTAssertNil(AppAttestationService.makeDefault(
            client: client,
            environment: ["VOUCHA_DISABLE_APP_ATTEST": "1"]
        ))
    }

    func testAssertionHeadersReturnsNilWhenUnsupported() async throws {
        provider.isSupported = false
        let service = try makeService()

        let headers = try await service.assertionHeaders(actionTag: .postsCreate)

        XCTAssertNil(headers)
        XCTAssertEqual(provider.generateKeyCallCount, 0)
        XCTAssertNil(CapturingURLProtocol.lastRequestURL)
    }

    func testDeviceIdReturnsNilWhenUnsupported() async throws {
        provider.isSupported = false
        let service = try makeService()

        let deviceId = try await service.deviceId()

        XCTAssertNil(deviceId)
        XCTAssertEqual(provider.generateKeyCallCount, 0)
        XCTAssertNil(CapturingURLProtocol.lastRequestURL)
    }

    func testDeviceIdReusesCachedKeyWithoutGenerating() async throws {
        keyStore.save(keyId: "existing-key-id")
        let service = try makeService()

        let deviceId = try await service.deviceId()

        XCTAssertEqual(deviceId, "existing-key-id")
        XCTAssertEqual(provider.generateKeyCallCount, 0)
        XCTAssertNil(provider.lastAttestKeyId)
        XCTAssertEqual(keyStore.loadKeyId(), "existing-key-id")
    }

    func testDeviceIdGeneratesAttestsStoresAndReturnsKeyOnFirstUse() async throws {
        let service = try makeService()

        let deviceId = try await service.deviceId()

        XCTAssertEqual(deviceId, "stub-key-id")
        XCTAssertEqual(provider.generateKeyCallCount, 1)
        XCTAssertEqual(provider.lastAttestKeyId, "stub-key-id")
        XCTAssertEqual(provider.lastAttestClientDataHash, Data(SHA256.hash(data: Data("nonce-abc".utf8))))
        XCTAssertEqual(keyStore.loadKeyId(), "stub-key-id")
    }

    func testDeviceIdReusesStoredKeyAcrossCalls() async throws {
        let service = try makeService()

        let firstDeviceId = try await service.deviceId()
        let secondDeviceId = try await service.deviceId()

        XCTAssertEqual(firstDeviceId, "stub-key-id")
        XCTAssertEqual(secondDeviceId, "stub-key-id")
        XCTAssertEqual(provider.generateKeyCallCount, 1)
        XCTAssertEqual(provider.lastAttestKeyId, "stub-key-id")
        XCTAssertEqual(keyStore.loadKeyId(), "stub-key-id")
    }

    func testConcurrentFirstUseCoalescesAttestationKeyCreation() async throws {
        let generateKeyGate = GenerateKeyGate()
        provider.generateKeyGate = generateKeyGate
        let service = try makeService()

        async let deviceId = service.deviceId()
        await generateKeyGate.waitUntilEntered()
        async let headers = service.assertionHeaders(actionTag: .postsCreate)
        await Task.yield()

        XCTAssertEqual(provider.generateKeyCallCount, 1)
        await generateKeyGate.release()
        let (resolvedDeviceId, resolvedHeaders) = try await (deviceId, headers)

        XCTAssertEqual(resolvedDeviceId, "stub-key-id")
        XCTAssertEqual(resolvedHeaders?["x-app-attest-key-id"], "stub-key-id")
        XCTAssertEqual(provider.generateKeyCallCount, 1)
        XCTAssertEqual(provider.lastAttestKeyId, "stub-key-id")
        XCTAssertEqual(keyStore.loadKeyId(), "stub-key-id")
    }

    func testAssertionHeadersGeneratesKeyAttestsAndReturnsHeadersOnFirstUse() async throws {
        let service = try makeService()

        let headers = try await service.assertionHeaders(actionTag: .postsCreate)

        XCTAssertEqual(headers?["x-app-attest-key-id"], "stub-key-id")
        XCTAssertEqual(headers?["x-app-attest-assertion"], Data("assertion-blob".utf8).base64EncodedString())
        XCTAssertEqual(headers?["x-app-attest-challenge-id"], "chal-1")

        XCTAssertEqual(provider.generateKeyCallCount, 1)
        XCTAssertEqual(provider.lastAttestKeyId, "stub-key-id")
        XCTAssertEqual(provider.lastAttestClientDataHash, Data(SHA256.hash(data: Data("nonce-abc".utf8))))

        let expectedAssertionPayload = Data("chal-1:posts.create".utf8)
        XCTAssertEqual(provider.lastAssertionKeyId, "stub-key-id")
        XCTAssertEqual(provider.lastAssertionClientDataHash, Data(SHA256.hash(data: expectedAssertionPayload)))

        XCTAssertEqual(keyStore.loadKeyId(), "stub-key-id")
    }

    func testAssertionHeadersReusesExistingKeyWithoutReattesting() async throws {
        keyStore.save(keyId: "existing-key-id")
        let service = try makeService()

        let headers = try await service.assertionHeaders(actionTag: .postsCreate)

        XCTAssertEqual(headers?["x-app-attest-key-id"], "existing-key-id")
        XCTAssertEqual(provider.generateKeyCallCount, 0)
        XCTAssertNil(provider.lastAttestKeyId)
        XCTAssertEqual(provider.lastAssertionKeyId, "existing-key-id")
    }

    func testAssertionHeadersPropagatesProviderError() async throws {
        provider.generateKeyResult = .failure(StubError())
        let service = try makeService()

        do {
            _ = try await service.assertionHeaders(actionTag: .postsCreate)
            XCTFail("Expected provider error to propagate")
        } catch is StubError {
            // expected
        }
    }

    func testAssertionHeadersPropagatesNetworkError() async throws {
        CapturingURLProtocol.responseStatusCode = 500
        let service = try makeService()

        do {
            _ = try await service.assertionHeaders(actionTag: .postsCreate)
            XCTFail("Expected network error to propagate")
        } catch is VouchaError {
            // expected
        }
    }

    func testAssertionHeadersRetriesAfterClearingStaleKeyWhenAssertionFails() async throws {
        keyStore.save(keyId: "stale-key-id")
        provider.generateAssertionFailureCount = 1
        let service = try makeService()

        let headers = try await service.assertionHeaders(actionTag: .postsCreate)

        XCTAssertEqual(headers?["x-app-attest-key-id"], "stub-key-id")
        XCTAssertEqual(provider.generateKeyCallCount, 1)
        XCTAssertEqual(provider.lastAttestKeyId, "stub-key-id")
        XCTAssertEqual(provider.generateAssertionCallCount, 2)
        XCTAssertEqual(provider.lastAssertionKeyId, "stub-key-id")
        XCTAssertEqual(keyStore.loadKeyId(), "stub-key-id")
    }

    func testForgetCachedKeyClearsKeyStore() throws {
        keyStore.save(keyId: "existing-key-id")
        let service = try makeService()

        service.forgetCachedKey()

        XCTAssertNil(keyStore.loadKeyId())
    }

    func testAssertionHeadersPropagatesErrorWhenRetryAlsoFails() async throws {
        keyStore.save(keyId: "stale-key-id")
        provider.generateAssertionFailureCount = 2
        let service = try makeService()

        do {
            _ = try await service.assertionHeaders(actionTag: .postsCreate)
            XCTFail("Expected provider error to propagate")
        } catch is StubError {
            XCTAssertEqual(provider.generateAssertionCallCount, 2)
        }
    }
}
