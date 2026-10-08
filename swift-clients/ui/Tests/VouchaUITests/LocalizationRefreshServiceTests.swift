import Foundation
import VouchaAPI
import VouchaCore
@testable import VouchaFeatures
@testable import VouchaLocalization
import XCTest

@MainActor
final class LocalizationRefreshServiceTests: XCTestCase {
    private final class TestClock {
        var now: Date

        init(_ now: Date) {
            self.now = now
        }
    }

    private func waitForHeldRequest(
        _ barrier: CannedFeedURLProtocol.RequestBarrier,
        path: String,
        tasks: [Task<Void, Never>]
    ) async throws {
        do {
            _ = try await barrier.wait()
        } catch {
            CannedFeedURLProtocol.releaseResponse(path: path)
            for task in tasks {
                task.cancel()
            }
            for task in tasks {
                await task.value
            }
            throw error
        }
    }

    override func tearDown() {
        LocalizationValueCache.shared.reset()
        CannedFeedURLProtocol.reset()
        super.tearDown()
    }

    func testRefreshAppliesFlattenedChromeValues() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/localization"] = (
            Data("""
            {
              "contract": "v1",
              "revision": "rev-1",
              "ttlSeconds": 120,
              "messages": { "common.cancel": "Abort" }
            }
            """.utf8),
            200
        )
        let baseURL = try XCTUnwrap(URL(string: "https://example.test"))
        let client = APIClient(
            config: .init(baseURL: baseURL, turnstileSiteKey: "test-site-key"),
            protocolClasses: [CannedFeedURLProtocol.self],
            bootstrapSession: false
        )
        let controller = UiLocaleController(savedUiLocale: "en", preferredLanguages: [])

        await LocalizationRefreshService.refresh(client: client, controller: controller)

        XCTAssertEqual(controller.string(.commonCancel), "Abort")
        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.path), ["/api/v1/localization"])
        XCTAssertEqual(controller.overlayGeneration, 1)
        let capturedURL = try XCTUnwrap(CannedFeedURLProtocol.capturedURLs.first)
        let selectors = URLComponents(
            url: capturedURL,
            resolvingAgainstBaseURL: false
        )?.queryItems?.first(where: { $0.name == "selectors" })?.value ?? ""
        XCTAssertTrue(selectors.split(separator: ",").contains("native.swift.navigationTitles.*"))
        XCTAssertTrue(selectors.split(separator: ",").contains("native.swift.navigation.*"))
        XCTAssertTrue(selectors.split(separator: ",").contains("native.navigation.*"))
        XCTAssertTrue(selectors.split(separator: ",").contains("images.uploadPreviewUnavailable"))
        let navigationKey = UiMessageKey.nativeSwiftNavigationVoucha.rawValue
        XCTAssertTrue(selectors.split(separator: ",").contains { selector in
            selector.hasSuffix("*")
                ? navigationKey.hasPrefix(String(selector.dropLast()))
                : navigationKey == selector
        })
    }

    func testFreshCacheSkipsNetworkRefresh() async throws {
        let cache = LocalizationValueCache()
        cache.apply(
            locale: "en",
            revision: "rev-1",
            ttlSeconds: 120,
            values: ["common.cancel": "Abort"]
        )
        let baseURL = try XCTUnwrap(URL(string: "https://example.test"))
        let client = APIClient(
            config: .init(baseURL: baseURL, turnstileSiteKey: "test-site-key"),
            protocolClasses: [CannedFeedURLProtocol.self],
            bootstrapSession: false
        )
        let controller = UiLocaleController(savedUiLocale: "en", preferredLanguages: [])

        await LocalizationRefreshService.refresh(
            client: client,
            controller: controller,
            cache: cache
        )

        XCTAssertTrue(CannedFeedURLProtocol.capturedURLs.isEmpty)
        XCTAssertEqual(cache.value(for: "common.cancel", locale: "en"), "Abort")
    }

    func testExpiredCacheRevalidatesWithEtagAndReusesStoredTtlOnNotModified() async throws {
        CannedFeedURLProtocol.handlers["/api/v1/localization"] = (Data(), 304)
        let now = Date(timeIntervalSince1970: 2_000)
        let cache = LocalizationValueCache()
        cache.apply(
            locale: "en",
            revision: "rev-1",
            ttlSeconds: 10,
            values: ["common.cancel": "Abort"],
            now: now.addingTimeInterval(-11)
        )
        let baseURL = try XCTUnwrap(URL(string: "https://example.test"))
        let client = APIClient(
            config: .init(baseURL: baseURL, turnstileSiteKey: "test-site-key"),
            protocolClasses: [CannedFeedURLProtocol.self],
            bootstrapSession: false
        )
        let controller = UiLocaleController(savedUiLocale: "en", preferredLanguages: [])

        await LocalizationRefreshService.refresh(
            client: client,
            controller: controller,
            cache: cache,
            clock: { now }
        )

        XCTAssertEqual(CannedFeedURLProtocol.capturedURLs.map(\.path), ["/api/v1/localization"])
        XCTAssertEqual(CannedFeedURLProtocol.capturedRequests.first?.ifNoneMatch, "\"rev-1\"")
        XCTAssertEqual(cache.value(for: "common.cancel", locale: "en"), "Abort")
        XCTAssertFalse(cache.isExpired(locale: "en", now: now.addingTimeInterval(9)))
        XCTAssertTrue(cache.isExpired(locale: "en", now: now.addingTimeInterval(10)))
    }

    func testOlderOverlappingResponseCannotReplaceNewerLocaleRevision() async throws {
        let path = "/api/v1/localization"
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (Data("""
            {"contract":"v1","revision":"old","ttlSeconds":120,
             "messages":{"common.cancel":"Old"}}
            """.utf8), 200, 0),
            (Data("""
            {"contract":"v1","revision":"new","ttlSeconds":120,
             "messages":{"common.cancel":"New"}}
            """.utf8), 200, 0)
        ]
        CannedFeedURLProtocol.suspendResponse(path: path)
        defer { CannedFeedURLProtocol.releaseResponse(path: path) }

        let baseURL = try XCTUnwrap(URL(string: "https://example.test"))
        let client = APIClient(
            config: .init(baseURL: baseURL, turnstileSiteKey: "test-site-key"),
            protocolClasses: [CannedFeedURLProtocol.self],
            bootstrapSession: false
        )
        let cache = LocalizationValueCache()
        let controller = UiLocaleController(savedUiLocale: "en", preferredLanguages: [])
        let firstBarrier = CannedFeedURLProtocol.requestBarrier(path: path, method: "GET")
        let first = Task {
            await LocalizationRefreshService.refresh(client: client, controller: controller, cache: cache)
        }
        defer { first.cancel() }
        try await waitForHeldRequest(firstBarrier, path: path, tasks: [first])

        let secondBarrier = CannedFeedURLProtocol.requestBarrier(path: path, method: "GET")
        let second = Task {
            await LocalizationRefreshService.refresh(client: client, controller: controller, cache: cache)
        }
        defer { second.cancel() }
        try await waitForHeldRequest(secondBarrier, path: path, tasks: [first, second])

        CannedFeedURLProtocol.releaseNewestResponse(path: path)
        await second.value
        CannedFeedURLProtocol.releaseOldestResponse(path: path)
        await first.value

        XCTAssertEqual(cache.value(for: "common.cancel", locale: "en"), "New")
        XCTAssertEqual(cache.etag(for: "en"), "\"new\"")
    }

    func testOlderSuccessfulResponseSurvivesNewerFailedRefresh() async throws {
        let path = "/api/v1/localization"
        CannedFeedURLProtocol.queuedHandlers[path] = [
            (Data("""
            {"contract":"v1","revision":"old","ttlSeconds":120,
             "messages":{"common.cancel":"Old"}}
            """.utf8), 200, 0),
            (Data(), 500, 0)
        ]
        CannedFeedURLProtocol.suspendResponse(path: path)
        defer { CannedFeedURLProtocol.releaseResponse(path: path) }

        let client = try APIClient(
            config: .init(
                baseURL: XCTUnwrap(URL(string: "https://example.test")),
                turnstileSiteKey: "test-site-key"
            ),
            protocolClasses: [CannedFeedURLProtocol.self],
            bootstrapSession: false
        )
        let cache = LocalizationValueCache()
        let controller = UiLocaleController(savedUiLocale: "en", preferredLanguages: [])
        let firstBarrier = CannedFeedURLProtocol.requestBarrier(path: path, method: "GET")
        let first = Task {
            await LocalizationRefreshService.refresh(client: client, controller: controller, cache: cache)
        }
        defer { first.cancel() }
        try await waitForHeldRequest(firstBarrier, path: path, tasks: [first])

        let secondBarrier = CannedFeedURLProtocol.requestBarrier(path: path, method: "GET")
        let second = Task {
            await LocalizationRefreshService.refresh(client: client, controller: controller, cache: cache)
        }
        defer { second.cancel() }
        try await waitForHeldRequest(secondBarrier, path: path, tasks: [first, second])

        CannedFeedURLProtocol.releaseNewestResponse(path: path)
        await second.value
        CannedFeedURLProtocol.releaseOldestResponse(path: path)
        await first.value

        XCTAssertEqual(cache.value(for: "common.cancel", locale: "en"), "Old")
        XCTAssertEqual(cache.etag(for: "en"), "\"old\"")
    }

    func testDelayedSuccessStartsTtlAtResponseTime() async throws {
        let path = "/api/v1/localization"
        CannedFeedURLProtocol.handlers[path] = (Data("""
        {"contract":"v1","revision":"rev-1","ttlSeconds":10,
         "messages":{"common.cancel":"Abort"}}
        """.utf8), 200)
        CannedFeedURLProtocol.suspendResponse(path: path)
        defer { CannedFeedURLProtocol.releaseResponse(path: path) }

        let client = try APIClient(
            config: .init(
                baseURL: XCTUnwrap(URL(string: "https://example.test")),
                turnstileSiteKey: "test-site-key"
            ),
            protocolClasses: [CannedFeedURLProtocol.self],
            bootstrapSession: false
        )
        let cache = LocalizationValueCache()
        let controller = UiLocaleController(savedUiLocale: "en", preferredLanguages: [])
        let clock = TestClock(Date(timeIntervalSince1970: 2_000))
        let barrier = CannedFeedURLProtocol.requestBarrier(path: path, method: "GET")
        let refresh = Task {
            await LocalizationRefreshService.refresh(
                client: client,
                controller: controller,
                cache: cache,
                clock: { clock.now }
            )
        }
        defer { refresh.cancel() }
        try await waitForHeldRequest(barrier, path: path, tasks: [refresh])

        clock.now = Date(timeIntervalSince1970: 2_020)
        CannedFeedURLProtocol.releaseResponse(path: path)
        await refresh.value

        XCTAssertFalse(cache.isExpired(locale: "en", now: Date(timeIntervalSince1970: 2_029)))
        XCTAssertTrue(cache.isExpired(locale: "en", now: Date(timeIntervalSince1970: 2_030)))
    }

    func testDelayedNotModifiedStartsTtlAtResponseTime() async throws {
        let path = "/api/v1/localization"
        CannedFeedURLProtocol.handlers[path] = (Data(), 304)
        CannedFeedURLProtocol.suspendResponse(path: path)
        defer { CannedFeedURLProtocol.releaseResponse(path: path) }

        let client = try APIClient(
            config: .init(
                baseURL: XCTUnwrap(URL(string: "https://example.test")),
                turnstileSiteKey: "test-site-key"
            ),
            protocolClasses: [CannedFeedURLProtocol.self],
            bootstrapSession: false
        )
        let cache = LocalizationValueCache()
        cache.apply(
            locale: "en", revision: "rev-1", ttlSeconds: 10,
            values: ["common.cancel": "Abort"],
            now: Date(timeIntervalSince1970: 2_000)
        )
        let controller = UiLocaleController(savedUiLocale: "en", preferredLanguages: [])
        let clock = TestClock(Date(timeIntervalSince1970: 2_011))
        let barrier = CannedFeedURLProtocol.requestBarrier(path: path, method: "GET")
        let refresh = Task {
            await LocalizationRefreshService.refresh(
                client: client,
                controller: controller,
                cache: cache,
                clock: { clock.now }
            )
        }
        defer { refresh.cancel() }
        try await waitForHeldRequest(barrier, path: path, tasks: [refresh])

        clock.now = Date(timeIntervalSince1970: 2_020)
        CannedFeedURLProtocol.releaseResponse(path: path)
        await refresh.value

        XCTAssertFalse(cache.isExpired(locale: "en", now: Date(timeIntervalSince1970: 2_029)))
        XCTAssertTrue(cache.isExpired(locale: "en", now: Date(timeIntervalSince1970: 2_030)))
    }
}
