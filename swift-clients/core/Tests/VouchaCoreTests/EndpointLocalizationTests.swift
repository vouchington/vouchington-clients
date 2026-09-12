import Foundation
@testable import VouchaAPI
import XCTest

final class EndpointLocalizationTests: XCTestCase {
    func testLocalizationEndpointEncodesConsumerLocalesAndSelectors() {
        let endpoint = Endpoint.localization(
            consumer: "swift",
            locales: ["en", "es"],
            selectors: ["common.*", "native.auth.*"]
        )
        assertEndpoint(endpoint, path: "/api/v1/localization")
        XCTAssertEqual(
            endpoint.queryItems,
            [
                URLQueryItem(name: "consumer", value: "swift"),
                URLQueryItem(name: "locales", value: "en,es"),
                URLQueryItem(name: "selectors", value: "common.*,native.auth.*")
            ]
        )
    }
}
