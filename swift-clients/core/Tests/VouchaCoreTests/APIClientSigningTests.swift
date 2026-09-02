import Crypto
import Foundation
@testable import VouchaAPI
@testable import VouchaAuth
@testable import VouchaCore
import XCTest

private final class StubRequestSigner: RequestSigning, @unchecked Sendable {
    var headersToReturn: [String: String]? = [
        "x-app-attest-key-id": "stub-key",
        "x-app-attest-assertion": "stub-assertion",
        "x-app-attest-timestamp": "1700000000",
        "x-app-attest-nonce": "stub-nonce"
    ]
    private(set) var lastMethod: String?
    private(set) var lastPath: String?
    private(set) var callCount = 0

    func signingHeaders(method: String, path: String, body _: Data?) async throws -> [String: String]? {
        callCount += 1
        lastMethod = method
        lastPath = path
        return headersToReturn
    }
}

final class APIClientSigningTests: XCTestCase {
    override func setUp() {
        super.setUp()
        CapturingURLProtocol.responseData = Data("{}".utf8)
        CapturingURLProtocol.responseStatusCode = 200
        CapturingURLProtocol.capturedRequestHeaders = [:]
    }

    override func tearDown() {
        CapturingURLProtocol.responseData = Data("{}".utf8)
        CapturingURLProtocol.responseStatusCode = 200
        CapturingURLProtocol.capturedRequestHeaders = [:]
        super.tearDown()
    }

    private func makeClient(signer: (any RequestSigning)? = nil) -> APIClient {
        APIClient(
            config: AppConfig(baseURL: URL(string: "http://localhost:2999")!, turnstileSiteKey: "test-site-key"),
            cookieStorage: IsolatedHTTPCookieStorage.make(),
            protocolClasses: [CapturingURLProtocol.self],
            signer: signer
        )
    }

    func testNoSignerSendsRequestWithoutSigningHeaders() async throws {
        let client = makeClient(signer: nil)
        let _: EmptyResponse = try await client.send(.init(.GET, path: "/v1/posts"))

        XCTAssertNil(CapturingURLProtocol.capturedRequestHeaders["x-app-attest-key-id"])
        XCTAssertNil(CapturingURLProtocol.capturedRequestHeaders["x-app-attest-assertion"])
    }

    func testSignerInjectsAllFourSigningHeaders() async throws {
        let signer = StubRequestSigner()
        let client = makeClient(signer: signer)
        let _: EmptyResponse = try await client.send(.init(.GET, path: "/v1/posts"))

        XCTAssertEqual(CapturingURLProtocol.capturedRequestHeaders["x-app-attest-key-id"], "stub-key")
        XCTAssertEqual(CapturingURLProtocol.capturedRequestHeaders["x-app-attest-assertion"], "stub-assertion")
        XCTAssertEqual(CapturingURLProtocol.capturedRequestHeaders["x-app-attest-timestamp"], "1700000000")
        XCTAssertEqual(CapturingURLProtocol.capturedRequestHeaders["x-app-attest-nonce"], "stub-nonce")
    }

    func testOptedOutSignerReturnsNilSendsUnsigned() async throws {
        let signer = StubRequestSigner()
        signer.headersToReturn = nil
        let client = makeClient(signer: signer)
        let _: EmptyResponse = try await client.send(.init(.GET, path: "/v1/posts"))

        XCTAssertNil(CapturingURLProtocol.capturedRequestHeaders["x-app-attest-key-id"])
        XCTAssertEqual(signer.callCount, 1)
    }

    func testChallengeIdHeaderPresentSkipsPerRequestSigning() async throws {
        let signer = StubRequestSigner()
        let client = makeClient(signer: signer)
        let endpoint = Endpoint(
            .POST,
            path: "/v1/posts",
            headers: ["x-app-attest-challenge-id": "chal-abc"]
        )
        let _: EmptyResponse = try await client.send(endpoint)

        XCTAssertEqual(signer.callCount, 0, "signer must not be invoked when challenge-id header is present")
        XCTAssertNil(CapturingURLProtocol.capturedRequestHeaders["x-app-attest-key-id"])
        XCTAssertEqual(CapturingURLProtocol.capturedRequestHeaders["x-app-attest-challenge-id"], "chal-abc")
    }

    func testSignerReceivesCorrectMethodAndPercentEncodedPath() async throws {
        let signer = StubRequestSigner()
        let client = makeClient(signer: signer)
        let _: EmptyResponse = try await client.send(.init(.PUT, path: "/v1/settings/profile%2Fprivate"))

        XCTAssertEqual(signer.lastMethod, "PUT")
        XCTAssertEqual(signer.lastPath, "/v1/settings/profile%2Fprivate")
    }

    func testBodyEncodingRespectsEndpointKeyStrategy() async throws {
        let protocolClass = BodyCapturingURLProtocol.self
        BodyCapturingURLProtocol.capturedBodies = []
        let client = try APIClient(
            config: AppConfig(
                baseURL: XCTUnwrap(URL(string: "http://localhost:2999")),
                turnstileSiteKey: "test-site-key"
            ),
            cookieStorage: IsolatedHTTPCookieStorage.make(),
            protocolClasses: [protocolClass]
        )

        let _: EmptyResponse = try await client.send(
            Endpoint.updateCommunityPostTypeSettings(
                idOrSlug: "builders",
                allowReviewPosts: true,
                allowDataPointPosts: false
            )
        )
        let _: EmptyResponse = try await client.send(
            Endpoint.createCommunityWarning(
                idOrSlug: "builders",
                userId: "user-1",
                reason: "Spam",
                publicMessage: "Please follow the rules.",
                reportId: "report-1",
                resolveReport: false
            )
        )

        XCTAssertEqual(BodyCapturingURLProtocol.capturedBodies.count, 2)
        let snakeBody = try jsonBody(BodyCapturingURLProtocol.capturedBodies[0])
        XCTAssertEqual(snakeBody["allow_review_posts"] as? Bool, true)
        XCTAssertEqual(snakeBody["allow_data_point_posts"] as? Bool, false)

        let defaultKeyBody = try jsonBody(BodyCapturingURLProtocol.capturedBodies[1])
        XCTAssertEqual(defaultKeyBody["userId"] as? String, "user-1")
        XCTAssertEqual(defaultKeyBody["reason"] as? String, "Spam")
        XCTAssertEqual(defaultKeyBody["publicMessage"] as? String, "Please follow the rules.")
        XCTAssertEqual(defaultKeyBody["reportId"] as? String, "report-1")
        XCTAssertEqual(defaultKeyBody["resolveReport"] as? Bool, false)
    }

    /// Verifies that APIClient passes the actual encoded request body to the signer, so the signer
    /// can embed the correct body hash in the canonical string. Uses a wrapper signer to capture
    /// the body bytes (URLProtocol.request.httpBody is nil by startLoading time; URLSession has
    /// converted it to httpBodyStream, making direct capture unreliable for this purpose).
    func testBodyHashEndToEnd() async throws {
        let keyStore = AppAttestKeyStore(serviceName: "ai.voucha.test.\(UUID().uuidString)")
        keyStore.save(keyId: "e2e-key-id")
        defer { keyStore.clear() }

        let stubProvider = StubCapturingProvider()
        let innerSigner = AppAttestRequestSigner(
            provider: stubProvider,
            keyStore: keyStore,
            isEnabled: { true },
            now: { Date(timeIntervalSince1970: 1_700_000_000) },
            makeNonce: { "e2e-nonce" }
        )
        let capturingSigner = BodyCapturingSignerWrapper(inner: innerSigner)
        let client = makeClient(signer: capturingSigner)

        struct Payload: Encodable { let title: String }
        let _: EmptyResponse = try await client.send(.init(.PUT, path: "/v1/posts/1", body: Payload(title: "hi")))

        let capturedHash = try XCTUnwrap(stubProvider.lastClientDataHash)
        let capturedBody = capturingSigner.capturedBody
        let bodyHash = CanonicalRequestString.bodyHash(from: capturedBody)
        let canonical = CanonicalRequestString.build(
            method: "PUT",
            path: "/v1/posts/1",
            bodyHash: bodyHash,
            timestamp: 1_700_000_000,
            nonce: "e2e-nonce"
        )
        XCTAssertEqual(capturedHash, Data(SHA256.hash(data: Data(canonical.utf8))))
    }

    private func jsonBody(_ body: String?) throws -> [String: Any] {
        let data = try XCTUnwrap(body?.data(using: .utf8))
        return try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: Any])
    }
}

private final class BodyCapturingURLProtocol: URLProtocol {
    static var capturedBodies: [String?] = []

    override class func canInit(with _: URLRequest) -> Bool {
        true
    }

    override class func canonicalRequest(for request: URLRequest) -> URLRequest {
        request
    }

    override func startLoading() {
        Self.capturedBodies.append(capturedBody(from: request))
        let response = HTTPURLResponse(
            url: request.url!,
            statusCode: 200,
            httpVersion: "HTTP/1.1",
            headerFields: ["Content-Type": "application/json"]
        )!
        client?.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed)
        client?.urlProtocol(self, didLoad: Data("{}".utf8))
        client?.urlProtocolDidFinishLoading(self)
    }

    override func stopLoading() {}

    private func capturedBody(from request: URLRequest) -> String? {
        if let body = request.httpBody {
            return String(data: body, encoding: .utf8)
        }
        guard let stream = request.httpBodyStream else { return nil }
        stream.open()
        defer { stream.close() }
        var data = Data()
        var buffer = [UInt8](repeating: 0, count: 1_024)
        while stream.hasBytesAvailable {
            let read = buffer.withUnsafeMutableBufferPointer { pointer in
                guard let baseAddress = pointer.baseAddress else { return 0 }
                return stream.read(baseAddress, maxLength: pointer.count)
            }
            if read <= 0 {
                break
            }
            data.append(buffer, count: read)
        }
        return String(data: data, encoding: .utf8)
    }
}

private final class BodyCapturingSignerWrapper: RequestSigning, @unchecked Sendable {
    private let inner: AppAttestRequestSigner
    private(set) var capturedBody: Data?

    init(inner: AppAttestRequestSigner) {
        self.inner = inner
    }

    func signingHeaders(method: String, path: String, body: Data?) async throws -> [String: String]? {
        capturedBody = body
        return try await inner.signingHeaders(method: method, path: path, body: body)
    }
}

private final class StubCapturingProvider: AppAttestProviding, @unchecked Sendable {
    var isSupported = true
    private(set) var lastClientDataHash: Data?

    func generateKey() async throws -> String {
        "key"
    }

    func attestKey(_: String, clientDataHash _: Data) async throws -> Data {
        Data()
    }

    func generateAssertion(_: String, clientDataHash: Data) async throws -> Data {
        lastClientDataHash = clientDataHash
        return Data("e2e-assertion".utf8)
    }
}
