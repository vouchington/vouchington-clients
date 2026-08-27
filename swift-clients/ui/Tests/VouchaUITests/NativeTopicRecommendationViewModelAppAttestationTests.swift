import Foundation
@testable import VouchaAPI
@testable import VouchaAuth
@testable import VouchaFeatures
import XCTest

private final class StubAppAttestProvider: AppAttestProviding, @unchecked Sendable {
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

@MainActor
final class NativeTopicRecommendationViewModelAppAttestationTests: NativeRouteSurfaceViewModelTestCase {
    private var provider: StubAppAttestProvider!
    private var keyStore: AppAttestKeyStore!

    override func setUp() {
        super.setUp()
        CannedFeedURLProtocol.queuedHandlers = [:]
        provider = StubAppAttestProvider()
        // Unique service name prevents cross-test keychain pollution.
        keyStore = AppAttestKeyStore(serviceName: "ai.voucha.test.\(UUID().uuidString)")
        CannedFeedURLProtocol.handlers["/api/v1/app-attestation/challenge"] =
            (Data(#"{"challengeId":"chal-1","challenge":"nonce-abc"}"#.utf8), 200)
        CannedFeedURLProtocol.handlers["/api/v1/app-attestation/attest"] = (Data("{}".utf8), 200)
    }

    override func tearDown() {
        keyStore.clear()
        super.tearDown()
    }

    private func makeViewModel() throws -> NativeTopicRecommendationViewModel {
        let client = try makeClient()
        let service = AppAttestationService(client: client, provider: provider, keyStore: keyStore)
        return NativeTopicRecommendationViewModel(client: client, appAttestationService: service)
    }

    func testSubmitUsesAppAttestHeadersInsteadOfTurnstile() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/topic-recommendations"] = (
            Data(#"{"post":{"id":"topic-rec-1"}}"#.utf8),
            201
        )
        let viewModel = try makeViewModel()
        viewModel.topicTitle = "Native Topic"
        viewModel.topicSlug = "native-topic"
        viewModel.bodyText = "Please add this topic."

        await viewModel.submit()

        XCTAssertEqual(viewModel.savedPostId, "topic-rec-1")
        XCTAssertNil(viewModel.turnstileToken)
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.contains { $0.path == "/api/v1/topic-recommendations" })
        if case .loaded = viewModel.state {
            return
        }
        XCTFail("Expected loaded state")
    }

    func testSubmitFallsBackToTurnstileRequiredWhenAttestationFails() async throws {
        provider.attestKeyResult = .failure(NSError(domain: "test", code: 1))
        let viewModel = try makeViewModel()
        viewModel.topicTitle = "Native Topic"
        viewModel.topicSlug = "native-topic"
        viewModel.bodyText = "Please add this topic."

        await viewModel.submit()

        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.allSatisfy { $0.path != "/api/v1/topic-recommendations" })
        if case .required = viewModel.state {
            return
        }
        XCTFail("Expected turnstile-required state")
    }

    func testSubmitFallsBackToTurnstileRequiredWhenAppAttestIsUnsupported() async throws {
        provider.isSupported = false
        let viewModel = try makeViewModel()
        viewModel.topicTitle = "Native Topic"
        viewModel.topicSlug = "native-topic"
        viewModel.bodyText = "Please add this topic."

        await viewModel.submit()

        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.isEmpty)
        if case .required = viewModel.state {
            return
        }
        XCTFail("Expected turnstile-required state")
    }

    func testSubmitRetriesWithTurnstileWhenServerRejectsAppAttestBypass() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/topic-recommendations"] = [
            (Data(#"{"code":"BYPASS_DISABLED"}"#.utf8), 403, 0),
            (Data(#"{"post":{"id":"topic-rec-1"}}"#.utf8), 201, 0)
        ]
        let viewModel = try makeViewModel()
        viewModel.topicTitle = "Native Topic"
        viewModel.topicSlug = "native-topic"
        viewModel.bodyText = "Please add this topic."
        viewModel.turnstileToken = "turnstile-token"

        await viewModel.submit()

        XCTAssertEqual(viewModel.savedPostId, "topic-rec-1")
        let attempts = CannedFeedURLProtocol.capturedURLs.filter { $0.path == "/api/v1/topic-recommendations" }
        XCTAssertEqual(attempts.count, 2)
        if case .loaded = viewModel.state {
            return
        }
        XCTFail("Expected loaded state")
    }

    func testSubmitRequiresTurnstileWhenServerRejectsBypassAndNoTokenAvailable() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/topic-recommendations"] = [
            (Data(#"{"code":"BYPASS_DISABLED"}"#.utf8), 403, 0)
        ]
        let viewModel = try makeViewModel()
        viewModel.topicTitle = "Native Topic"
        viewModel.topicSlug = "native-topic"
        viewModel.bodyText = "Please add this topic."

        await viewModel.submit()

        let attempts = CannedFeedURLProtocol.capturedURLs.filter { $0.path == "/api/v1/topic-recommendations" }
        XCTAssertEqual(attempts.count, 1)
        if case .required = viewModel.state {
            return
        }
        XCTFail("Expected turnstile-required state")
    }

    func testSubmitForgetsCachedKeyWhenServerRejectsAssertion() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/topic-recommendations"] = [
            (Data(#"{"code":"ATTESTATION_REJECTED"}"#.utf8), 403, 0),
            (Data(#"{"post":{"id":"topic-rec-1"}}"#.utf8), 201, 0)
        ]
        let viewModel = try makeViewModel()
        viewModel.topicTitle = "Native Topic"
        viewModel.topicSlug = "native-topic"
        viewModel.bodyText = "Please add this topic."
        viewModel.turnstileToken = "turnstile-token"

        await viewModel.submit()

        XCTAssertEqual(viewModel.savedPostId, "topic-rec-1")
        XCTAssertNil(
            keyStore.loadKeyId(),
            "a rejected assertion must clear the cached key so the next attempt re-attests"
        )
    }

    func testSubmitSurfacesErrorInsteadOfRetryingWhenServerRejectsForPermissionReasons() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/topic-recommendations"] = [
            (Data(#"{"code":"IDENTITY_REQUIRED"}"#.utf8), 403, 0),
            (Data(#"{"post":{"id":"topic-rec-1"}}"#.utf8), 201, 0)
        ]
        let viewModel = try makeViewModel()
        viewModel.topicTitle = "Native Topic"
        viewModel.topicSlug = "native-topic"
        viewModel.bodyText = "Please add this topic."
        viewModel.turnstileToken = "turnstile-token"

        await viewModel.submit()

        let attempts = CannedFeedURLProtocol.capturedURLs.filter { $0.path == "/api/v1/topic-recommendations" }
        XCTAssertEqual(attempts.count, 1, "a real permission 403 must not be retried with Turnstile")
        if case .error = viewModel.state {
            return
        }
        XCTFail("Expected error state")
    }
}
