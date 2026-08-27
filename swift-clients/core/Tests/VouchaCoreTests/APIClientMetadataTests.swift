import Foundation
@testable import VouchaAPI
@testable import VouchaCore
import XCTest

final class APIClientMetadataTests: XCTestCase {
    private struct EmptyResponse: Decodable {}

    override func setUp() {
        super.setUp()
        CapturingURLProtocol.responseData = Data("{}".utf8)
        CapturingURLProtocol.responseStatusCode = 200
        SessionBootstrapURLProtocol.requestedPaths = []
        SessionBootstrapURLProtocol.bootstrapDelay = 0
    }

    private func makeCookieStorage() -> HTTPCookieStorage {
        InMemoryCookieStorage()
    }

    private func makeCookie(
        name: String,
        value: String,
        domain: String,
        path: String,
        secure: Bool = false
    ) throws -> HTTPCookie {
        var properties: [HTTPCookiePropertyKey: Any] = [
            .name: name,
            .value: value,
            .domain: domain,
            .path: path
        ]
        if secure {
            properties[.secure] = "TRUE"
        }
        return try XCTUnwrap(HTTPCookie(properties: properties))
    }

    func testBuildRequestAddsImmutableClientMetadata() async throws {
        let metadata = ClientMetadata(platform: .ios, appVersion: "2.3.4+56", sdkVersion: "1.2.0")
        let client = try APIClient(
            config: AppConfig(baseURL: XCTUnwrap(URL(string: "https://voucha.test"))),
            metadata: metadata
        )

        let request = try await client.buildRequest(Endpoint(
            .GET,
            path: "/api/v1/session",
            headers: ["x-voucha-platform": "windows", "x-voucha-app-version": "spoofed"]
        ))

        XCTAssertEqual(request.value(forHTTPHeaderField: "x-voucha-client"), "swift")
        XCTAssertEqual(request.value(forHTTPHeaderField: "x-voucha-platform"), "ios")
        XCTAssertEqual(request.value(forHTTPHeaderField: "x-voucha-app-version"), "2.3.4+56")
        XCTAssertEqual(request.value(forHTTPHeaderField: "x-voucha-sdk-version"), "1.2.0")
    }

    func testBuildRequestOmitsAbsentSDKVersion() async throws {
        let client = try APIClient(
            config: AppConfig(baseURL: XCTUnwrap(URL(string: "https://voucha.test"))),
            metadata: ClientMetadata(platform: .android, appVersion: "4.0")
        )

        let request = try await client.buildRequest(Endpoint(.GET, path: "/api/v1/session"))

        XCTAssertNil(request.value(forHTTPHeaderField: "x-voucha-sdk-version"))
    }

    func testSendBootstrapsSessionBeforeFirstPublicRequest() async throws {
        let cookieStorage = makeCookieStorage()
        let client = try APIClient(
            config: AppConfig(baseURL: XCTUnwrap(URL(string: "https://voucha.test"))),
            cookieStorage: cookieStorage,
            protocolClasses: [SessionBootstrapURLProtocol.self],
            bootstrapSession: true,
            metadata: ClientMetadata(platform: .ios, appVersion: "1.0")
        )

        let _: EmptyResponse = try await client.send(Endpoint(.GET, path: "/api/v1/posts"))
        let _: EmptyResponse = try await client.send(Endpoint(.GET, path: "/api/v1/communities"))

        XCTAssertEqual(
            SessionBootstrapURLProtocol.requestedPaths,
            ["/api/v1/session", "/api/v1/posts", "/api/v1/communities"]
        )
        XCTAssertEqual(cookieStorage.cookies?.first(where: { $0.name == "dt" })?.value, "device-token")
    }

    func testExplicitSessionRequestDoesNotBootstrapTwice() async throws {
        let cookieStorage = makeCookieStorage()
        let client = try APIClient(
            config: AppConfig(baseURL: XCTUnwrap(URL(string: "https://voucha.test"))),
            cookieStorage: cookieStorage,
            protocolClasses: [SessionBootstrapURLProtocol.self],
            bootstrapSession: true
        )

        let _: EmptyResponse = try await client.send(Endpoint(
            .PATCH,
            path: "/api/v1/session",
            body: [String: String]()
        ))
        let _: EmptyResponse = try await client.send(Endpoint(.GET, path: "/api/v1/posts"))

        XCTAssertEqual(SessionBootstrapURLProtocol.requestedPaths, ["/api/v1/session", "/api/v1/posts"])
    }

    func testStreamBootstrapsSessionBeforeOpeningRequest() async throws {
        let cookieStorage = makeCookieStorage()
        let client = try APIClient(
            config: AppConfig(baseURL: XCTUnwrap(URL(string: "https://voucha.test"))),
            cookieStorage: cookieStorage,
            protocolClasses: [SessionBootstrapURLProtocol.self],
            bootstrapSession: true
        )

        let stream: AsyncThrowingStream<EmptyResponse, Error> = await client.stream(
            Endpoint(.GET, path: "/api/v1/stream")
        )
        for try await _ in stream {}

        XCTAssertEqual(SessionBootstrapURLProtocol.requestedPaths, ["/api/v1/session", "/api/v1/stream"])
    }

    func testSendBootstrapsAgainAfterSessionCookiesAreCleared() async throws {
        let cookieStorage = makeCookieStorage()
        let client = try APIClient(
            config: AppConfig(baseURL: XCTUnwrap(URL(string: "https://voucha.test"))),
            cookieStorage: cookieStorage,
            protocolClasses: [SessionBootstrapURLProtocol.self],
            bootstrapSession: true
        )

        let _: EmptyResponse = try await client.send(Endpoint(.GET, path: "/api/v1/posts"))
        for cookie in cookieStorage.cookies ?? [] {
            cookieStorage.deleteCookie(cookie)
        }
        let _: EmptyResponse = try await client.send(Endpoint(.GET, path: "/api/v1/communities"))

        XCTAssertEqual(SessionBootstrapURLProtocol.requestedPaths.filter { $0 == "/api/v1/session" }.count, 2)
    }

    func testSendCoalescesConcurrentSessionBootstrapRequests() async throws {
        let cookieStorage = makeCookieStorage()
        SessionBootstrapURLProtocol.bootstrapDelay = 0.05
        let client = try APIClient(
            config: AppConfig(baseURL: XCTUnwrap(URL(string: "https://voucha.test"))),
            cookieStorage: cookieStorage,
            protocolClasses: [SessionBootstrapURLProtocol.self],
            bootstrapSession: true
        )

        async let first: EmptyResponse = client.send(Endpoint(.GET, path: "/api/v1/posts"))
        async let second: EmptyResponse = client.send(Endpoint(.GET, path: "/api/v1/communities"))
        _ = try await (first, second)

        XCTAssertEqual(SessionBootstrapURLProtocol.requestedPaths.filter { $0 == "/api/v1/session" }.count, 1)
    }

    func testInMemoryCookieStorageMatchesSecureDomainAndPath() throws {
        let cookieStorage = makeCookieStorage()
        let cookie = try makeCookie(
            name: "dt",
            value: "device-token",
            domain: ".voucha.test",
            path: "/api",
            secure: true
        )

        cookieStorage.setCookie(cookie)

        let nestedURL = try XCTUnwrap(URL(string: "https://api.voucha.test/api/v1/session"))
        let exactPathURL = try XCTUnwrap(URL(string: "https://api.voucha.test/api"))
        let insecureURL = try XCTUnwrap(URL(string: "http://api.voucha.test/api/v1/session"))

        XCTAssertEqual(cookieStorage.cookies(for: nestedURL)?.first?.value, "device-token")
        XCTAssertEqual(cookieStorage.cookies(for: exactPathURL)?.first?.value, "device-token")
        XCTAssertTrue(cookieStorage.cookies(for: insecureURL)?.isEmpty ?? true)
    }
}

private final class InMemoryCookieStorage: HTTPCookieStorage, @unchecked Sendable {
    private let lock = NSLock()
    private var storedCookies: [HTTPCookie] = []

    override var cookies: [HTTPCookie]? {
        lock.withLock { storedCookies }
    }

    override func setCookie(_ cookie: HTTPCookie) {
        lock.withLock {
            storedCookies.removeAll { $0.matchesStorageKey(of: cookie) }
            storedCookies.append(cookie)
        }
    }

    override func cookies(for url: URL) -> [HTTPCookie]? {
        lock.withLock {
            storedCookies.filter { $0.matches(url: url) }
        }
    }

    override func deleteCookie(_ cookie: HTTPCookie) {
        lock.withLock {
            storedCookies.removeAll { $0.matchesStorageKey(of: cookie) }
        }
    }
}

private extension HTTPCookie {
    func matchesStorageKey(of cookie: HTTPCookie) -> Bool {
        name == cookie.name && domain == cookie.domain && path == cookie.path
    }

    func matches(url: URL) -> Bool {
        guard let host = url.host else { return false }
        if isSecure && url.scheme?.lowercased() != "https" {
            return false
        }
        guard matchesDomain(host) else { return false }
        let cookiePath = path.isEmpty ? "/" : path
        guard cookiePath != "/" else { return true }
        let urlPath = url.path.isEmpty ? "/" : url.path
        guard urlPath.hasPrefix(cookiePath) else { return false }
        guard urlPath.count > cookiePath.count else { return true }
        let boundary = urlPath.index(urlPath.startIndex, offsetBy: cookiePath.count)
        return urlPath[boundary] == "/"
    }

    private func matchesDomain(_ host: String) -> Bool {
        if domain.hasPrefix(".") {
            let bareDomain = String(domain.dropFirst())
            return host == bareDomain || host.hasSuffix("." + bareDomain)
        }
        return host == domain
    }
}

private final class SessionBootstrapURLProtocol: URLProtocol {
    static var requestedPaths: [String] = []
    static var bootstrapDelay: TimeInterval = 0

    private let stateLock = NSLock()
    private var stopped = false

    /// Runs `callback` only if `stopLoading()` hasn't run yet, with the check and the callback
    /// itself under `stateLock` — see `CannedFeedURLProtocol.deliverIfNotStopped(_:)` for why the
    /// check and the call must share a lock instead of a plain `guard` before the callback.
    private func deliverIfNotStopped(_ callback: () -> Void) {
        stateLock.lock()
        defer { stateLock.unlock() }
        guard !stopped else { return }
        callback()
    }

    /// Invoked immediately before `startLoading()` enters its delay sleep. Test-only: lets a test
    /// synchronize a concurrent `stopLoading()` call to the exact moment delivery starts waiting.
    var onWillSleepForDelivery: (() -> Void)?

    override class func canInit(with _: URLRequest) -> Bool {
        true
    }

    override class func canonicalRequest(for request: URLRequest) -> URLRequest {
        request
    }

    override func startLoading() {
        Self.requestedPaths.append(request.url?.path ?? "")
        if request.url?.path == "/api/v1/session", Self.bootstrapDelay > 0 {
            onWillSleepForDelivery?()
            Thread.sleep(forTimeInterval: Self.bootstrapDelay)
        }
        stateLock.lock()
        let isStopped = stopped
        stateLock.unlock()
        guard !isStopped else { return }
        let response = HTTPURLResponse(
            url: request.url!,
            statusCode: 200,
            httpVersion: "HTTP/1.1",
            headerFields: ["Content-Type": "application/json"]
        )!
        let data = request.url?.path == "/api/v1/session"
            ? Data(#"{"session":{"dt":"device-token","st":"session-token","dte":3600,"ste":1800,"secure":true}}"#.utf8)
            : Data("{}".utf8)
        deliverIfNotStopped { client?.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed) }
        deliverIfNotStopped { client?.urlProtocol(self, didLoad: data) }
        deliverIfNotStopped { client?.urlProtocolDidFinishLoading(self) }
    }

    override func stopLoading() {
        stateLock.lock()
        stopped = true
        stateLock.unlock()
    }
}

/// Regression test for the use-after-free/TOCTOU class fixed in `CannedFeedURLProtocol.swift`
/// (PR #8349): a delayed `SessionBootstrapURLProtocol` delivery must not call back into its
/// `URLProtocolClient` once `stopLoading()` has run, because by then the client's owning
/// `URLSession`/`APIClient` may already be torn down. Fails against an empty `stopLoading()` (all
/// three callback counts land at 1) and passes once `startLoading()` rechecks stopped state after
/// its sleep.
final class SessionBootstrapURLProtocolStopLoadingTests: XCTestCase {
    override func setUp() {
        super.setUp()
        SessionBootstrapURLProtocol.requestedPaths = []
        SessionBootstrapURLProtocol.bootstrapDelay = 0
    }

    func testStoppedProtocolDeliversNoCallbacksAfterDelayedSessionResponse() throws {
        SessionBootstrapURLProtocol.bootstrapDelay = 0.05
        let url = try XCTUnwrap(URL(string: "https://voucha.test/api/v1/session"))

        let client = RecordingURLProtocolClient()
        let protocolInstance = SessionBootstrapURLProtocol(
            request: URLRequest(url: url),
            cachedResponse: nil,
            client: client
        )

        // Stop before the delayed delivery ever runs, mirroring a cancelled URLSessionTask racing
        // a still-sleeping delivery thread.
        protocolInstance.stopLoading()
        protocolInstance.startLoading()

        XCTAssertEqual(client.didReceiveResponseCount, 0)
        XCTAssertEqual(client.didLoadDataCount, 0)
        XCTAssertEqual(client.didFinishLoadingCount, 0)
    }

    func testRunningProtocolDeliversCallbacksWhenNotStopped() throws {
        // Baseline for the test above: without `stopLoading()`, delivery must actually reach the
        // client. This keeps the zero-callback assertion meaningful — it proves the stop is what
        // suppresses delivery, not that the fixture never calls back at all.
        let url = try XCTUnwrap(URL(string: "https://voucha.test/api/v1/session"))

        let client = RecordingURLProtocolClient()
        let protocolInstance = SessionBootstrapURLProtocol(
            request: URLRequest(url: url),
            cachedResponse: nil,
            client: client
        )

        protocolInstance.startLoading()

        XCTAssertEqual(client.didReceiveResponseCount, 1)
        XCTAssertEqual(client.didLoadDataCount, 1)
        XCTAssertEqual(client.didFinishLoadingCount, 1)
    }

    func testConcurrentStopDuringDelayedSessionDeliverySuppressesAllCallbacks() throws {
        SessionBootstrapURLProtocol.bootstrapDelay = 0.05
        let url = try XCTUnwrap(URL(string: "https://voucha.test/api/v1/session"))

        let client = RecordingURLProtocolClient()
        let protocolInstance = SessionBootstrapURLProtocol(
            request: URLRequest(url: url),
            cachedResponse: nil,
            client: client
        )

        // startLoading() sleeps synchronously for the bootstrap delay, so it must run off this
        // thread for stopLoading() to have a chance to race it. `onWillSleepForDelivery` blocks the
        // worker on `mayProceed` until this test has called `stopLoading()` and signaled it, so
        // `stopLoading()` always happens-before the post-sleep stopped recheck — no scheduler-
        // dependent window, unlike racing a fixed timer that can fire late under CI runner load.
        let enteredDelayWindow = expectation(description: "startLoading entered its delay sleep")
        let mayProceed = DispatchSemaphore(value: 0)
        protocolInstance.onWillSleepForDelivery = {
            enteredDelayWindow.fulfill()
            mayProceed.wait()
        }

        let startLoadingFinished = expectation(description: "startLoading returns")
        DispatchQueue.global().async {
            protocolInstance.startLoading()
            startLoadingFinished.fulfill()
        }
        wait(for: [enteredDelayWindow], timeout: 2)
        protocolInstance.stopLoading()
        mayProceed.signal()
        wait(for: [startLoadingFinished], timeout: 2)

        XCTAssertEqual(client.didReceiveResponseCount, 0)
        XCTAssertEqual(client.didLoadDataCount, 0)
        XCTAssertEqual(client.didFinishLoadingCount, 0)
    }
}

private final class RecordingURLProtocolClient: NSObject, URLProtocolClient {
    private(set) var didReceiveResponseCount = 0
    private(set) var didLoadDataCount = 0
    private(set) var didFinishLoadingCount = 0

    func urlProtocol(
        _: URLProtocol,
        didReceive _: URLResponse,
        cacheStoragePolicy _: URLCache.StoragePolicy
    ) {
        didReceiveResponseCount += 1
    }

    func urlProtocol(_: URLProtocol, didLoad _: Data) {
        didLoadDataCount += 1
    }

    func urlProtocolDidFinishLoading(_: URLProtocol) {
        didFinishLoadingCount += 1
    }

    func urlProtocol(_: URLProtocol, didFailWithError _: Error) {}

    func urlProtocol(
        _: URLProtocol,
        wasRedirectedTo _: URLRequest,
        redirectResponse _: URLResponse
    ) {}

    func urlProtocol(_: URLProtocol, cachedResponseIsValid _: CachedURLResponse) {}

    func urlProtocol(_: URLProtocol, didReceive _: URLAuthenticationChallenge) {}

    func urlProtocol(_: URLProtocol, didCancel _: URLAuthenticationChallenge) {}
}
