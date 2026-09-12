@testable import VouchaLocalization
import XCTest

final class LocalizationValueCacheTests: XCTestCase {
    override func tearDown() {
        LocalizationValueCache.shared.reset()
        super.tearDown()
    }

    func testOverlayReturnsAppliedStringAndHonorsTtl() {
        let cache = LocalizationValueCache()
        let now = Date(timeIntervalSince1970: 1_000)
        cache.apply(
            locale: "en",
            revision: "rev-1",
            ttlSeconds: 60,
            values: ["common.cancel": "Abort"],
            now: now
        )
        XCTAssertEqual(cache.value(for: "common.cancel", locale: "en"), "Abort")
        XCTAssertEqual(cache.etag(for: "en"), "\"rev-1\"")
        XCTAssertFalse(cache.isExpired(locale: "en", now: now.addingTimeInterval(59)))
        XCTAssertTrue(cache.isExpired(locale: "en", now: now.addingTimeInterval(60)))
        XCTAssertEqual(cache.value(for: "common.cancel", locale: "en"), "Abort")
    }

    func testRememberNotModifiedExtendsExpiryWithoutChangingValues() {
        let cache = LocalizationValueCache()
        let now = Date(timeIntervalSince1970: 1_000)
        cache.apply(
            locale: "en",
            revision: "rev-1",
            ttlSeconds: 10,
            values: ["common.cancel": "Abort"],
            now: now
        )
        cache.rememberNotModified(locale: "en", ttlSeconds: 300, now: now.addingTimeInterval(10))
        XCTAssertFalse(cache.isExpired(locale: "en", now: now.addingTimeInterval(11)))
        XCTAssertEqual(cache.value(for: "common.cancel", locale: "en"), "Abort")
    }

    func testLruEvictsOldestLocaleWhenByteBoundIsExceeded() {
        let cache = LocalizationValueCache(maxBytes: 8)
        cache.apply(locale: "en", revision: "a", ttlSeconds: 60, values: ["a": "12345"])
        cache.apply(locale: "es", revision: "b", ttlSeconds: 60, values: ["b": "67890"])
        XCTAssertNil(cache.value(for: "a", locale: "en"))
        XCTAssertEqual(cache.value(for: "b", locale: "es"), "67890")
    }
}
