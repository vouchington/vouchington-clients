import Foundation
import Observation
import VouchaAPI
import VouchaAuth
@testable import VouchaCore
@testable import VouchaFeatures
import VouchaModels
import XCTest

@MainActor
final class FreshAccountConfirmationTests: XCTestCase {
    override func setUp() {
        super.setUp()
        CannedFeedURLProtocol.reset()
    }

    func testEmailProofIsPurposeScopedAndUsedForExactlyOneMutationAfterExplicitVerification() async throws {
        let fixture = try makeFixture()
        let mutation = Task {
            let proof = try await fixture.model.authorize()
            let _: EmptyResponse = try await fixture.client.send(.disconnectBlueskyAccount(reAuthToken: proof))
        }
        await waitForMethods(fixture.model)
        XCTAssertFalse(CannedFeedURLProtocol.capturedURLs.contains { $0.path == "/api/v1/auth/bluesky/link" })
        await fixture.model.sendEmail()
        fixture.model.code = "abcdefgh23"
        await fixture.model.verifyCode()
        try await mutation.value
        await fixture.model.verifyCode()

        XCTAssertEqual(paths.filter { $0 == "/api/v1/auth/mfa/re-auth/email/verification" }.count, 1)
        XCTAssertFalse(paths.contains("/api/v1/auth/email-address/login"))
        XCTAssertEqual(paths.filter { $0 == "/api/v1/auth/bluesky/link" }.count, 1)
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies.contains { $0?.contains("ABCDEFGH23") == true })
        XCTAssertTrue(CannedFeedURLProtocol.capturedBodies.contains { $0?.contains("one-use-proof") == true })
        XCTAssertEqual(
            fixture.cookies.cookies(for: fixture.client.baseURL)?.first { $0.name == "st" }?.value,
            "initiating-session"
        )
    }

    func testTOTPRejectsEmailCodeWithoutSendingVerification() async throws {
        let fixture = try makeFixture()
        let waiting = Task { try await fixture.model.authorize() }
        await waitForMethods(fixture.model)
        fixture.model.usingEmail = false
        fixture.model.code = "ABCDEFGH23"
        await fixture.model.verifyCode()
        XCTAssertFalse(paths.contains("/api/v1/auth/mfa/re-auth/totp/verification"))
        fixture.model.code = "123456"
        await fixture.model.verifyCode()
        let proof = try await waiting.value
        XCTAssertEqual(proof, "one-use-proof")
        XCTAssertTrue(paths.contains("/api/v1/auth/mfa/re-auth/totp/verification"))
    }

    func testCancelBeforeVerificationDoesNotCommitAndLateProofCannotCompleteAnotherRequest() async throws {
        let fixture = try makeFixture()
        let waiting = Task { try await fixture.model.authorize() }
        await waitForMethods(fixture.model)
        let path = "/api/v1/auth/mfa/re-auth/totp/verification"
        CannedFeedURLProtocol.suspendResponse(path: path)
        let barrier = CannedFeedURLProtocol.requestBarrier(path: path, method: "POST")
        fixture.model.usingEmail = false
        fixture.model.code = "123456"
        let verifying = Task { await fixture.model.verifyCode() }
        _ = try await barrier.wait()
        fixture.model.cancel()
        CannedFeedURLProtocol.releaseResponse(path: path)
        await verifying.value
        do { _ = try await waiting.value
            XCTFail("Canceled proof must fail")
        } catch is CancellationError {}
        XCTAssertFalse(fixture.model.isPresented)
        XCTAssertFalse(fixture.model.isWorking)
        XCTAssertFalse(paths.contains("/api/v1/auth/bluesky/link"))
    }

    func testChangedSessionOrDeviceRejectsProofWithoutMutation() async throws {
        for cookieName in ["st", "dt"] {
            let fixture = try makeFixture()
            let waiting = Task { try await fixture.model.authorize() }
            await waitForMethods(fixture.model)
            let path = "/api/v1/auth/mfa/re-auth/totp/verification"
            CannedFeedURLProtocol.suspendResponse(path: path)
            let barrier = CannedFeedURLProtocol.requestBarrier(path: path, method: "POST")
            fixture.model.usingEmail = false
            fixture.model.code = "123456"
            let verifying = Task { await fixture.model.verifyCode() }
            _ = try await barrier.wait()
            try setCookie(cookieName, value: "another-context", storage: fixture.cookies)
            CannedFeedURLProtocol.releaseResponse(path: path)
            await verifying.value
            XCTAssertFalse(fixture.model.isPresented)
            XCTAssertNil(fixture.model.methods)
            do { _ = try await waiting.value
                XCTFail("Changed context must fail")
            } catch is CancellationError {}
        }
    }

    func testInvalidExpiredReplayedRevokedAndQuotaProofsNeverRetryOrCommit() async throws {
        for status in [400, 401, 403, 409, 410, 429] {
            let fixture = try makeFixture()
            CannedFeedURLProtocol.capturedURLs = []
            CannedFeedURLProtocol.handlers["/api/v1/auth/mfa/re-auth/totp/verification"] = (
                Data(#"{"message":"Proof rejected"}"#.utf8), status
            )
            let waiting = Task { try await fixture.model.authorize() }
            await waitForMethods(fixture.model)
            fixture.model.usingEmail = false
            fixture.model.code = "123456"
            await fixture.model.verifyCode()
            XCTAssertNotNil(fixture.model.errorMessage)
            XCTAssertEqual(paths.filter { $0 == "/api/v1/auth/mfa/re-auth/totp/verification" }.count, 1)
            XCTAssertFalse(paths.contains("/api/v1/auth/bluesky/link"))
            fixture.model.cancel()
            do { _ = try await waiting.value
                XCTFail("Rejected proof must fail")
            } catch is CancellationError {}
        }
    }

    func testProviderOnlyAccountUsesFreshSDKNonceAndUnconfiguredGoogleFailsClosed() async throws {
        let spy = FreshGoogleAssertionSpy()
        let fixture = try makeFixture(google: spy)
        CannedFeedURLProtocol.handlers["/api/v1/auth/re-auth/methods"] = (
            Data(#"{"has_email":false,"has_passkey":false,"has_totp":false,"providers":["google"]}"#.utf8), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/auth/re-auth/providers/google/challenges"] = (
            Data(#"{"flow_id":"fresh-flow","nonce":"server-nonce","expires_at":"2027-01-01T00:00:00Z"}"#.utf8), 200
        )
        CannedFeedURLProtocol.handlers["/api/v1/auth/re-auth/providers/google/verification"] = (
            Data(#"{"re_auth_token":"provider-proof"}"#.utf8), 200
        )
        let waiting = Task { try await fixture.model.authorize() }
        await waitForMethods(fixture.model)
        await fixture.model.verifyGoogle()
        let proof = try await waiting.value
        XCTAssertEqual(proof, "provider-proof")
        let nonces = await spy.nonces
        XCTAssertEqual(nonces, ["server-nonce"])
        XCTAssertFalse(paths.contains("/api/v1/auth/re-auth/providers/google/authorizations"))

        let unconfigured = try makeFixture()
        await unconfigured.model.verifyGoogle()
        XCTAssertNotNil(unconfigured.model.errorMessage)
    }

    var paths: [String] {
        CannedFeedURLProtocol.capturedURLs.map(\.path)
    }

    func waitForMethods(_ model: FreshAccountConfirmationViewModel) async {
        await waitForAccountConfirmationState { model.methods != nil }
    }

    func makeFixture(google: (any FreshProviderAssertion)? = nil, brokerEnabled: Bool = false) throws
        -> (
            model: FreshAccountConfirmationViewModel,
            client: APIClient,
            cookies: HTTPCookieStorage,
            broker: NativeOAuthAuthorizationCoordinator?
        ) {
        let cookies = HTTPCookieStorage()
        try setCookie("dt", value: "initiating-device", storage: cookies)
        try setCookie("st", value: "initiating-session", storage: cookies)
        let client = APIClient(
            config: AppConfig(baseURL: URL(string: "http://localhost:2999")!), cookieStorage: cookies,
            protocolClasses: [CannedFeedURLProtocol.self], bootstrapSession: false
        )
        CannedFeedURLProtocol.handlers["/api/v1/auth/re-auth/methods"] = (
            Data(#"{"has_email":true,"has_passkey":false,"has_totp":true,"providers":[]}"#.utf8), 200
        )
        for path in ["/api/v1/auth/mfa/re-auth/email/verification", "/api/v1/auth/mfa/re-auth/totp/verification"] {
            CannedFeedURLProtocol.handlers[path] = (Data(#"{"re_auth_token":"one-use-proof"}"#.utf8), 200)
        }
        CannedFeedURLProtocol.handlers["/api/v1/auth/mfa/re-auth/email/tokens"] = (Data("{}".utf8), 200)
        CannedFeedURLProtocol.handlers["/api/v1/auth/bluesky/link"] = (Data("{}".utf8), 200)
        let broker: NativeOAuthAuthorizationCoordinator?
        if brokerEnabled {
            let defaults = try XCTUnwrap(UserDefaults(suiteName: UUID().uuidString))
            broker = NativeOAuthAuthorizationCoordinator(
                client: client, sessionManager: SessionManager(client: client, cookieStorage: cookies),
                store: NativeOAuthAuthorizationStore(secureState: UserDefaultsNativePendingState(
                    key: "reauth",
                    defaults: defaults
                ))
            )
        } else { broker = nil }
        return (
            FreshAccountConfirmationViewModel(client: client, broker: broker, googleAssertion: google),
            client,
            cookies,
            broker
        )
    }

    func setCookie(_ name: String, value: String, storage: HTTPCookieStorage) throws {
        try storage.setCookie(XCTUnwrap(HTTPCookie(properties: [
            .name: name, .value: value, .domain: "localhost", .path: "/"
        ])))
    }
}

private actor FreshGoogleAssertionSpy: FreshProviderAssertion {
    var nonces: [String] = []
    func assertion(nonce: String) async throws -> String {
        nonces.append(nonce)
        return "fresh-sdk-assertion"
    }
}
