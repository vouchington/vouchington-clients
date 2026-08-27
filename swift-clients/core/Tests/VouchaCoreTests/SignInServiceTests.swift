import Foundation
@testable import VouchaAPI
@testable import VouchaAuth
@testable import VouchaCore
import XCTest

private final class StubAppAttestProvider: AppAttestProviding, @unchecked Sendable {
    let isSupported = true

    func generateKey() async throws -> String {
        "stub-key-id"
    }

    func attestKey(_: String, clientDataHash _: Data) async throws -> Data {
        Data("attestation-blob".utf8)
    }

    func generateAssertion(_: String, clientDataHash _: Data) async throws -> Data {
        Data("assertion-blob".utf8)
    }
}

/// Routes by request shape rather than a single shared status code, since a single
/// `requestEmailOTP` call can make two requests to the same path: the App Attest bypass attempt
/// (carries `x-app-attest-key-id`), then — if that's rejected — the Turnstile fallback.
private final class RoutingURLProtocol: URLProtocol {
    static var bypassStatusCode = 200
    static var bypassBodies: [String?] = []
    static var turnstileStatusCode = 200
    static var turnstileBodies: [String?] = []
    /// When `bypassStatusCode == 403`, selects `ATTESTATION_REJECTED` (key genuinely invalid)
    /// over `BYPASS_DISABLED` (bypass off, key unaffected).
    static var rejectAssertionOn403 = false

    override class func canInit(with _: URLRequest) -> Bool {
        true
    }

    override class func canonicalRequest(for request: URLRequest) -> URLRequest {
        request
    }

    override func startLoading() {
        let path = request.url?.path ?? ""
        let statusCode: Int
        let data: Data
        if path.contains("/app-attestation/challenge") {
            statusCode = 200
            data = Data(#"{"challengeId":"chal-1","challenge":"nonce-abc"}"#.utf8)
        } else if request.value(forHTTPHeaderField: "x-app-attest-key-id") != nil {
            statusCode = Self.bypassStatusCode
            Self.bypassBodies.append(Self.capturedBody(from: request))
            // Real bodies per `verifyCaptchaOrAttestation`/`assertion.mts`: a bypass-disabled 403
            // carries `BYPASS_DISABLED`. `ATTESTATION_REJECTED` is used on both a 403 (the
            // assertion itself is invalid/replayed/device-mismatched -- the key is genuinely bad)
            // and a 409 (the assertion verified fine but lost a sign-count ordering race against a
            // concurrent request from the same healthy key). This mock disambiguates via a
            // dedicated flag since both share the 403 status. A real 401 (e.g. an expired session)
            // has no precondition code at all -- that's the case this mock must NOT attach a code to.
            switch statusCode {
            case 403: data = Data(
                    #"{"code":"\#(Self.rejectAssertionOn403 ? "ATTESTATION_REJECTED" : "BYPASS_DISABLED")"}"#
                        .utf8
                )
            case 409: data = Data(#"{"code":"ATTESTATION_REJECTED"}"#.utf8)
            default: data = Data("{}".utf8)
            }
        } else {
            statusCode = Self.turnstileStatusCode
            Self.turnstileBodies.append(Self.capturedBody(from: request))
            data = Data("{}".utf8)
        }
        let response = HTTPURLResponse(
            url: request.url!,
            statusCode: statusCode,
            httpVersion: "HTTP/1.1",
            headerFields: ["Content-Type": "application/json"]
        )!
        client?.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed)
        client?.urlProtocol(self, didLoad: data)
        client?.urlProtocolDidFinishLoading(self)
    }

    override func stopLoading() {}

    private static func capturedBody(from request: URLRequest) -> String? {
        if let body = request.httpBody {
            return String(data: body, encoding: .utf8)
        }
        guard let stream = request.httpBodyStream else { return nil }
        stream.open()
        defer { stream.close() }
        var data = Data()
        let bufferSize = 1_024
        let buffer = UnsafeMutablePointer<UInt8>.allocate(capacity: bufferSize)
        defer { buffer.deallocate() }
        while stream.hasBytesAvailable {
            let read = stream.read(buffer, maxLength: bufferSize)
            if read <= 0 {
                break
            }
            data.append(buffer, count: read)
        }
        return String(data: data, encoding: .utf8)
    }
}

@MainActor
final class SignInServiceTests: XCTestCase {
    private var keyStore: AppAttestKeyStore!

    override func setUp() {
        super.setUp()
        // Unique service name prevents cross-test keychain pollution.
        keyStore = AppAttestKeyStore(serviceName: "ai.voucha.test.\(UUID().uuidString)")
        keyStore.save(keyId: "stub-key-id")
        RoutingURLProtocol.bypassStatusCode = 200
        RoutingURLProtocol.bypassBodies = []
        RoutingURLProtocol.turnstileStatusCode = 200
        RoutingURLProtocol.turnstileBodies = []
        RoutingURLProtocol.rejectAssertionOn403 = false
    }

    override func tearDown() {
        keyStore.clear()
        super.tearDown()
    }

    private func makeService() throws -> SignInService {
        let client = try APIClient(
            config: AppConfig(
                baseURL: XCTUnwrap(URL(string: "http://localhost:2999")),
                turnstileSiteKey: "test-site-key"
            ),
            cookieStorage: HTTPCookieStorage(),
            protocolClasses: [RoutingURLProtocol.self]
        )
        let attestationService = AppAttestationService(
            client: client,
            provider: StubAppAttestProvider(),
            keyStore: keyStore
        )
        return SignInService(
            client: client,
            sessionManager: SessionManager(client: client, cookieStorage: HTTPCookieStorage()),
            appAttestationService: attestationService
        )
    }

    func testRequestEmailOTPSucceedsViaAppAttestBypassWithoutTurnstile() async throws {
        RoutingURLProtocol.turnstileStatusCode = 500 // would fail if the fallback were hit
        let service = try makeService()

        try await service.requestEmailOTP(
            email: "person@example.com",
            turnstileToken: nil,
            uiLocale: "es"
        )

        XCTAssertTrue(RoutingURLProtocol.bypassBodies.last??.contains(#""ui_locale":"es""#) == true)
    }

    func testRequestEmailOTPFallsBackToTurnstileWhenBypassForbidden() async throws {
        RoutingURLProtocol.bypassStatusCode = 403
        let service = try makeService()

        try await service.requestEmailOTP(
            email: "person@example.com",
            turnstileToken: "turnstile-token",
            uiLocale: "fr"
        )

        XCTAssertTrue(RoutingURLProtocol.turnstileBodies.last??.contains(#""ui_locale":"fr""#) == true)
    }

    func testRequestEmailOTPDoesNotFallBackToTurnstileWhenUnauthorized() async throws {
        RoutingURLProtocol.bypassStatusCode = 401
        RoutingURLProtocol.turnstileStatusCode = 500 // would fail if the fallback were hit
        let service = try makeService()

        do {
            try await service.requestEmailOTP(email: "person@example.com", turnstileToken: "turnstile-token")
            XCTFail("Expected .unauthorized to propagate instead of falling back to Turnstile")
        } catch VouchaError.unauthorized {
            // Expected: a real 401 carries no precondition code, so it must not be treated as
            // recoverable-by-Turnstile-fallback -- see `isRecoverableByTurnstileFallback`.
        }
    }

    func testRequestEmailOTPFallsBackToTurnstileOnSignCountRegression() async throws {
        RoutingURLProtocol.bypassStatusCode = 409
        let service = try makeService()

        try await service.requestEmailOTP(email: "person@example.com", turnstileToken: "turnstile-token")
    }

    func testRequestEmailOTPForgetsCachedKeyWhenAssertionRejected() async throws {
        RoutingURLProtocol.bypassStatusCode = 403
        RoutingURLProtocol.rejectAssertionOn403 = true
        let service = try makeService()

        try await service.requestEmailOTP(email: "person@example.com", turnstileToken: "turnstile-token")

        XCTAssertNil(
            keyStore.loadKeyId(),
            "a rejected assertion (e.g. did no longer matches after device-token rotation) must clear " +
                "the cached key so the next attempt re-attests instead of retrying the same rejected key"
        )
    }

    func testRequestEmailOTPKeepsCachedKeyOnSignCountRace() async throws {
        RoutingURLProtocol.bypassStatusCode = 409
        let service = try makeService()

        try await service.requestEmailOTP(email: "person@example.com", turnstileToken: "turnstile-token")

        XCTAssertEqual(
            keyStore.loadKeyId(),
            "stub-key-id",
            "a 409 means the assertion verified but lost a sign-count ordering race against a " +
                "concurrent request from the same healthy key -- it says nothing about the key's " +
                "validity, so it must not be cleared"
        )
    }

    func testRequestEmailOTPKeepsCachedKeyWhenBypassDisabled() async throws {
        RoutingURLProtocol.bypassStatusCode = 403
        let service = try makeService()

        try await service.requestEmailOTP(email: "person@example.com", turnstileToken: "turnstile-token")

        XCTAssertEqual(
            keyStore.loadKeyId(),
            "stub-key-id",
            "a disabled bypass says nothing about the key's validity, so it must not be cleared"
        )
    }

    func testRequestEmailOTPDoesNotSwallowUnrelatedBypassErrors() async throws {
        RoutingURLProtocol.bypassStatusCode = 500
        let service = try makeService()

        do {
            try await service.requestEmailOTP(email: "person@example.com", turnstileToken: "turnstile-token")
            XCTFail("Expected the unhandled bypass error to propagate instead of falling back")
        } catch let VouchaError.api(statusCode, _) {
            XCTAssertEqual(statusCode, 500)
        }
    }
}
