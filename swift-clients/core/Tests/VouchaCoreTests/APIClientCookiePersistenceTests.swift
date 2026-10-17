import Foundation
@testable import VouchaAPI
@testable import VouchaAuth
@testable import VouchaCore
import XCTest

final class APIClientCookiePersistenceTests: XCTestCase {
    private struct Empty: Decodable {}

    func testProvidedConfigurationKeepsItsExactJarAndCopiesSettings() async throws {
        let configuration = URLSessionConfiguration.ephemeral
        let jar = try XCTUnwrap(configuration.httpCookieStorage)
        configuration.timeoutIntervalForRequest = 17
        configuration.protocolClasses = [CookiePersistenceURLProtocol.self]
        let client = try APIClient(
            config: AppConfig(baseURL: XCTUnwrap(URL(string: "http://localhost:2999"))),
            cookieStorage: IsolatedHTTPCookieStorage.make(), bootstrapSession: false,
            sessionConfiguration: configuration
        )
        configuration.timeoutIntervalForRequest = 99

        let storedJar = await client.cookieStorage
        let session = await client.session
        XCTAssertTrue(storedJar === jar)
        XCTAssertTrue(session.configuration.httpCookieStorage === jar)
        XCTAssertEqual(session.configuration.timeoutIntervalForRequest, 17)
        let _: Empty = try await client.send(Endpoint(.GET, path: "/empty"))
    }

    func testDefaultAndEphemeralConfigurationsKeepProductionBootstrapEnabled() async throws {
        let defaultClient = APIClient(cookieStorage: IsolatedHTTPCookieStorage.make())
        let defaultEnabled = await defaultClient.sessionBootstrapEnabled
        XCTAssertTrue(defaultEnabled)
        let configuration = URLSessionConfiguration.ephemeral
        let jar = try XCTUnwrap(configuration.httpCookieStorage)
        let configuredClient = APIClient(cookieStorage: jar, sessionConfiguration: configuration)
        let configuredEnabled = await configuredClient.sessionBootstrapEnabled
        XCTAssertTrue(configuredEnabled)
    }

    func testLegacyProtocolClassArgumentKeepsBootstrapDisabled() async {
        let client = APIClient(
            cookieStorage: IsolatedHTTPCookieStorage.make(), protocolClasses: [CookiePersistenceURLProtocol.self]
        )
        let enabled = await client.sessionBootstrapEnabled
        XCTAssertFalse(enabled)
    }

    func testConfigurationOnlyProtocolClassesRequireAnExplicitBootstrapOverride() async throws {
        let configuration = URLSessionConfiguration.ephemeral
        let jar = try XCTUnwrap(configuration.httpCookieStorage)
        configuration.protocolClasses = [CookiePersistenceURLProtocol.self]
        let defaultClient = APIClient(cookieStorage: jar, sessionConfiguration: configuration)
        let defaultEnabled = await defaultClient.sessionBootstrapEnabled
        XCTAssertTrue(defaultEnabled)
        let overriddenClient = try APIClient(
            config: AppConfig(baseURL: XCTUnwrap(URL(string: "http://localhost:2999"))),
            cookieStorage: jar, bootstrapSession: false, sessionConfiguration: configuration
        )

        let _: Empty = try await overriddenClient.send(Endpoint(.GET, path: "/empty"))

        XCTAssertTrue(jar.cookies?.isEmpty ?? true)
        let overriddenEnabled = await overriddenClient.sessionBootstrapEnabled
        XCTAssertFalse(overriddenEnabled)
    }

    func testBootstrapPersistsJSONTokensBeforeReturningTheRequestedResponse() async throws {
        let (client, recorder) = try makeClient(bootstrap: true)

        let _: Empty = try await client.send(Endpoint(.GET, path: "/empty"))

        let snapshots = recorder.snapshots
        XCTAssertTrue(snapshots.contains { $0["dt"] == "device-token" && $0["st"] == "session-token" })
        XCTAssertEqual(snapshots.last?["dt"], "device-token")
        XCTAssertEqual(snapshots.last?["st"], "session-token")
    }

    func testExplicitSessionTokensArePersistedWithoutAnExtraBootstrap() async throws {
        let (client, recorder) = try makeClient()

        let _: Empty = try await client.send(Endpoint(.PATCH, path: "/api/v1/session"))

        XCTAssertEqual(recorder.snapshots.last?["st"], "session-token")
    }

    func testOrdinaryResponseCookieIsPersistedAfterTheJarUpdates() async throws {
        let server = try CookieResponseHTTPServer()
        defer { server.close() }
        let configuration = URLSessionConfiguration.ephemeral
        let jar = try XCTUnwrap(configuration.httpCookieStorage)
        let recorder = CookiePersistenceRecorder(jar: jar)
        let client = try APIClient(
            config: AppConfig(baseURL: XCTUnwrap(URL(string: "http://127.0.0.1:\(server.port)"))),
            cookieStorage: jar, bootstrapSession: false, sessionConfiguration: configuration,
            persistCookies: { recorder.snapshot() }
        )
        async let response: Void = server.respondOnce()
        let _: Empty = try await client.send(Endpoint(.GET, path: "/response-cookie"))
        await response
        XCTAssertEqual(recorder.snapshots.last?["response_session"], "updated")
    }

    func testNonSuccessAndNetworkErrorsStillSnapshotCookies() async throws {
        for path in ["/error", "/network-error"] {
            let (client, recorder) = try makeClient()
            do {
                let _: Empty = try await client.send(Endpoint(.GET, path: path))
                XCTFail("Expected request failure")
            } catch {
                XCTAssertFalse(recorder.snapshots.isEmpty)
            }
        }
    }

    func testFailedBootstrapAlsoSnapshotsBeforeReturningItsError() async throws {
        let (client, recorder) = try makeClient(bootstrap: true, basePath: "/bootstrap-error")
        do {
            let _: Empty = try await client.send(Endpoint(.GET, path: "/empty"))
            XCTFail("Expected bootstrap failure")
        } catch {
            XCTAssertFalse(recorder.snapshots.isEmpty)
        }
    }

    func testRawDataAndNotModifiedRequestsUseThePersistenceCallback() async throws {
        let (client, recorder) = try makeClient()
        let bytes = try await client.data(for: Endpoint(.GET, path: "/raw"))
        XCTAssertEqual(bytes, Data("raw bytes".utf8))
        let afterRaw = recorder.snapshots.count
        XCTAssertGreaterThan(afterRaw, 0)

        let unchanged: Empty? = try await client.sendAllowingNotModified(Endpoint(.GET, path: "/not-modified"))

        XCTAssertNil(unchanged)
        XCTAssertGreaterThan(recorder.snapshots.count, afterRaw)
    }

    func testResponseLinesSnapshotAtHeadersAndStreamCompletion() async throws {
        let (client, recorder) = try makeClient()
        let request = try URLRequest(url: XCTUnwrap(URL(string: "http://localhost:2999/lines")))
        let response = try await client.responseLines(for: request)
        XCTAssertFalse(recorder.snapshots.isEmpty)
        var lines: [String] = []
        for try await line in response.lines {
            lines.append(line)
        }

        XCTAssertEqual(lines, ["first", "second"])
        XCTAssertGreaterThanOrEqual(recorder.snapshots.count, 2)
    }

    func testDelegateResponseLinesSnapshotAtHeadersAndCompletion() async throws {
        let (client, recorder) = try makeClient()
        let request = try URLRequest(url: XCTUnwrap(URL(string: "http://localhost:2999/lines")))
        let response = try await client.responseLinesUsingURLSessionDataDelegate(for: request)
        XCTAssertFalse(recorder.snapshots.isEmpty)
        var lines: [String] = []
        for try await line in response.lines {
            lines.append(line)
        }

        XCTAssertEqual(lines, ["first", "second"])
        XCTAssertGreaterThanOrEqual(recorder.snapshots.count, 2)
    }

    func testCancelledDelegateStreamSnapshotsBeforeItsConsumerFinishes() async throws {
        let (client, recorder) = try makeClient()
        let request = try URLRequest(url: XCTUnwrap(URL(string: "http://localhost:2999/hold")))
        let response = try await client.responseLinesUsingURLSessionDataDelegate(for: request)
        let consumer = Task {
            for try await line in response.lines {
                XCTAssertEqual(line, "first")
            }
        }
        let beforeCancellation = recorder.snapshots.count

        consumer.cancel()
        _ = await consumer.result

        XCTAssertGreaterThan(recorder.snapshots.count, beforeCancellation)
    }

    #if canImport(Darwin)
        func testDownloadSnapshotsBeforeReturningItsOwnedFile() async throws {
            let (client, recorder) = try makeClient()
            let file = try await client.download(for: Endpoint(.GET, path: "/raw"), filename: "cookies.txt")
            defer { try? FileManager.default.removeItem(at: file) }

            XCTAssertEqual(try Data(contentsOf: file), Data("raw bytes".utf8))
            XCTAssertFalse(recorder.snapshots.isEmpty)
        }
    #endif

    private func makeClient(
        bootstrap: Bool = false,
        basePath: String = ""
    ) throws -> (APIClient, CookiePersistenceRecorder) {
        let configuration = URLSessionConfiguration.ephemeral
        let jar = try XCTUnwrap(configuration.httpCookieStorage)
        configuration.protocolClasses = [CookiePersistenceURLProtocol.self]
        let recorder = CookiePersistenceRecorder(jar: jar)
        let client = try APIClient(
            config: AppConfig(baseURL: XCTUnwrap(URL(string: "http://localhost:2999\(basePath)"))),
            cookieStorage: jar, bootstrapSession: bootstrap, sessionConfiguration: configuration,
            persistCookies: { recorder.snapshot() }
        )
        return (client, recorder)
    }
}

final class CookiePersistenceRecorder: @unchecked Sendable {
    private let lock = NSLock()
    private let jar: HTTPCookieStorage
    private var stored: [[String: String]] = []

    init(jar: HTTPCookieStorage) {
        self.jar = jar
    }

    func snapshot() {
        let cookies = Dictionary(
            (jar.cookies ?? []).map { ($0.name, $0.value) },
            uniquingKeysWith: { _, latest in latest }
        )
        lock.lock()
        stored.append(cookies)
        lock.unlock()
    }

    var snapshots: [[String: String]] {
        lock.lock()
        defer { lock.unlock() }
        return stored
    }
}
