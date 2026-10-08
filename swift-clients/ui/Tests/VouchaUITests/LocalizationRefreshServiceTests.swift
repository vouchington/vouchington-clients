import Foundation
import VouchaAPI
import VouchaCore
@testable import VouchaFeatures
@testable import VouchaLocalization
import XCTest

@MainActor
final class LocalizationRefreshServiceTests: XCTestCase {
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
        XCTAssertTrue(selectors.split(separator: ",").contains("native.swift.navigation.*"))
        XCTAssertTrue(selectors.split(separator: ",").contains("native.navigation.*"))
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
            now: now
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
        _ = try await firstBarrier.wait()

        let secondBarrier = CannedFeedURLProtocol.requestBarrier(path: path, method: "GET")
        let second = Task {
            await LocalizationRefreshService.refresh(client: client, controller: controller, cache: cache)
        }
        defer { second.cancel() }
        _ = try await secondBarrier.wait()

        CannedFeedURLProtocol.releaseNewestResponse(path: path)
        await second.value
        CannedFeedURLProtocol.releaseOldestResponse(path: path)
        await first.value

        XCTAssertEqual(cache.value(for: "common.cancel", locale: "en"), "New")
        XCTAssertEqual(cache.etag(for: "en"), "\"new\"")
    }
}
