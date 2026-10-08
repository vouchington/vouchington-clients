import Foundation
@testable import VouchaAPI
@testable import VouchaAuth
@testable import VouchaCore
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
final class NativePostComposeViewModelAppAttestationTests: XCTestCase {
    private var provider: StubAppAttestProvider!
    private var keyStore: AppAttestKeyStore!

    override func setUp() {
        super.setUp()
        CannedFeedURLProtocol.handlers = [:]
        CannedFeedURLProtocol.queuedHandlers = [:]
        CannedFeedURLProtocol.capturedURLs = []
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

    private func makeClient() throws -> APIClient {
        try APIClient(
            config: AppConfig(
                baseURL: XCTUnwrap(URL(string: "http://localhost:2999")),
                turnstileSiteKey: "test-site-key"
            ),
            cookieStorage: HTTPCookieStorage(),
            protocolClasses: [CannedFeedURLProtocol.self]
        )
    }

    private func makePostEnvelope(id: String) -> Data {
        Data("""
        {
          "post": {
            "id": "\(id)",
            "slug": null,
            "post_type": "discussion",
            "title": "Native title",
            "markdown": "Native body",
            "html": null,
            "parent_post_id": null,
            "root_post_id": null,
            "created_by_id": "user-1",
            "created_at": "2026-01-01T00:00:00Z",
            "broadcast": "everyone",
            "privacy": "public",
            "is_anonymous": false,
            "community_id": null,
            "clearance_status": "pending",
            "deleted_at": null,
            "deleted_by_id": null,
            "locked_at": null,
            "locked_by_id": null,
            "archived_at": null,
            "archived_by_id": null,
            "clearance_reason": null,
            "clearance_updated_at": null,
            "spam_detection_created_at": null,
            "spam_detection_flagged": null,
            "spam_detection_results": null,
            "spam_detection_score": null,
            "updated_by_id": null
          }
        }
        """.utf8)
    }

    func testCanPublishIsTrueWithoutTurnstileTokenWhenAppAttestIsSupported() throws {
        let client = try makeClient()
        let service = AppAttestationService(client: client, provider: provider, keyStore: keyStore)
        let viewModel = NativePostComposeViewModel(client: client, appAttestationService: service)
        viewModel.title = "Native title"
        viewModel.bodyText = "Native body"

        XCTAssertNil(viewModel.turnstileToken)
        XCTAssertTrue(viewModel.canPublish)
    }

    func testCanPublishRequiresTurnstileTokenWhenAppAttestIsUnsupported() throws {
        provider.isSupported = false
        let client = try makeClient()
        let service = AppAttestationService(client: client, provider: provider, keyStore: keyStore)
        let viewModel = NativePostComposeViewModel(client: client, appAttestationService: service)
        viewModel.title = "Native title"
        viewModel.bodyText = "Native body"

        XCTAssertFalse(viewModel.canPublish)
        viewModel.turnstileToken = "turnstile-token"
        XCTAssertTrue(viewModel.canPublish)
    }

    func testPublishUsesAppAttestHeadersInsteadOfTurnstile() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/posts"] = (makePostEnvelope(id: "post-1"), 201)
        let client = try makeClient()
        let service = AppAttestationService(client: client, provider: provider, keyStore: keyStore)
        let viewModel = NativePostComposeViewModel(client: client, appAttestationService: service)
        viewModel.title = "Native title"
        viewModel.bodyText = "Native body"

        await viewModel.publish()

        XCTAssertEqual(viewModel.publishedPostId, "post-1")
        XCTAssertNil(viewModel.turnstileToken)
        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.contains { $0.path == "/api/v1/posts" })
        if case .loaded = viewModel.state {
            return
        }
        XCTFail("Expected loaded state")
    }

    func testPublishFallsBackToTurnstileRequiredWhenAttestationFails() async throws {
        provider.attestKeyResult = .failure(NSError(domain: "test", code: 1))
        let client = try makeClient()
        let service = AppAttestationService(client: client, provider: provider, keyStore: keyStore)
        let viewModel = NativePostComposeViewModel(client: client, appAttestationService: service)
        viewModel.title = "Native title"
        viewModel.bodyText = "Native body"

        await viewModel.publish()

        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.allSatisfy { $0.path != "/api/v1/posts" })
        if case .required(.turnstile) = viewModel.state {
            return
        }
        XCTFail("Expected turnstile-required state")
    }

    func testPublishRetriesWithTurnstileWhenServerRejectsAppAttestBypass() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/posts"] = [
            (Data(#"{"code":"BYPASS_DISABLED"}"#.utf8), 403, 0),
            (makePostEnvelope(id: "post-1"), 201, 0)
        ]
        let client = try makeClient()
        let service = AppAttestationService(client: client, provider: provider, keyStore: keyStore)
        let viewModel = NativePostComposeViewModel(client: client, appAttestationService: service)
        viewModel.title = "Native title"
        viewModel.bodyText = "Native body"
        viewModel.turnstileToken = "turnstile-token"

        await viewModel.publish()

        XCTAssertEqual(viewModel.publishedPostId, "post-1")
        let attempts = CannedFeedURLProtocol.capturedURLs.filter { $0.path == "/api/v1/posts" }
        XCTAssertEqual(attempts.count, 2)
        if case .loaded = viewModel.state {
            return
        }
        XCTFail("Expected loaded state")
    }

    func testVerificationFailureDuringTurnstileFallbackPreservesOneDraftWithoutReplay() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/posts"] = [
            (Data(#"{"code":"BYPASS_DISABLED"}"#.utf8), 403, 0),
            (Data(#"{"code":"EMAIL_VERIFICATION_REQUIRED"}"#.utf8), 403, 0)
        ]
        let client = try makeClient()
        let service = AppAttestationService(client: client, provider: provider, keyStore: keyStore)
        let viewModel = NativePostComposeViewModel(client: client, appAttestationService: service)
        viewModel.title = "Preserved title"
        viewModel.bodyText = "Preserved body"
        viewModel.turnstileToken = "turnstile-token"

        await viewModel.publish()

        let attempts = CannedFeedURLProtocol.capturedURLs.filter { $0.path == "/api/v1/posts" }
        XCTAssertEqual(attempts.count, 2)
        XCTAssertEqual(viewModel.drafts.count, 1)
        XCTAssertEqual(viewModel.drafts.first?.title, "Preserved title")
        XCTAssertEqual(viewModel.title, "Preserved title")
        XCTAssertEqual(viewModel.bodyText, "Preserved body")
        XCTAssertNil(viewModel.turnstileToken)
        XCTAssertTrue(viewModel.emailVerificationGate.isRecoveryPresented)

        viewModel.emailVerificationGate.dismissRecovery()

        XCTAssertFalse(viewModel.emailVerificationGate.isRecoveryPresented)
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.filter { $0.path == "/api/v1/posts" }.count, 2)
        XCTAssertEqual(viewModel.drafts.count, 1)
    }

    func testPublishRequiresTurnstileWhenServerRejectsBypassAndNoTokenAvailable() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/posts"] = [
            (Data(#"{"code":"BYPASS_DISABLED"}"#.utf8), 403, 0)
        ]
        let client = try makeClient()
        let service = AppAttestationService(client: client, provider: provider, keyStore: keyStore)
        let viewModel = NativePostComposeViewModel(client: client, appAttestationService: service)
        viewModel.title = "Native title"
        viewModel.bodyText = "Native body"

        await viewModel.publish()

        let attempts = CannedFeedURLProtocol.capturedURLs.filter { $0.path == "/api/v1/posts" }
        XCTAssertEqual(attempts.count, 1)
        if case .required(.turnstile) = viewModel.state {
            return
        }
        XCTFail("Expected turnstile-required state")
    }

    func testPublishForgetsCachedKeyWhenServerRejectsAssertion() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/posts"] = [
            (Data(#"{"code":"ATTESTATION_REJECTED"}"#.utf8), 403, 0),
            (makePostEnvelope(id: "post-1"), 201, 0)
        ]
        let client = try makeClient()
        let service = AppAttestationService(client: client, provider: provider, keyStore: keyStore)
        let viewModel = NativePostComposeViewModel(client: client, appAttestationService: service)
        viewModel.title = "Native title"
        viewModel.bodyText = "Native body"
        viewModel.turnstileToken = "turnstile-token"

        await viewModel.publish()

        XCTAssertEqual(viewModel.publishedPostId, "post-1")
        XCTAssertNil(
            keyStore.loadKeyId(),
            "a rejected assertion must clear the cached key so the next attempt re-attests"
        )
    }

    func testPublishSurfacesErrorInsteadOfRetryingWhenServerRejectsForPermissionReasons() async throws {
        CannedFeedURLProtocol.queuedHandlers["/api/v1/posts"] = [
            (Data(#"{"code":"IDENTITY_REQUIRED"}"#.utf8), 403, 0),
            (makePostEnvelope(id: "post-1"), 201, 0)
        ]
        let client = try makeClient()
        let service = AppAttestationService(client: client, provider: provider, keyStore: keyStore)
        let viewModel = NativePostComposeViewModel(client: client, appAttestationService: service)
        viewModel.title = "Native title"
        viewModel.bodyText = "Native body"
        viewModel.turnstileToken = "turnstile-token"

        await viewModel.publish()

        let attempts = CannedFeedURLProtocol.capturedURLs.filter { $0.path == "/api/v1/posts" }
        XCTAssertEqual(attempts.count, 1, "a real permission 403 must not be retried with Turnstile")
        if case .error = viewModel.state {
            return
        }
        XCTFail("Expected error state")
    }
}
