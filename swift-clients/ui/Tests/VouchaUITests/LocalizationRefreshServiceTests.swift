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
}
