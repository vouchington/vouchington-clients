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
        let client = try APIClient(
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
}
