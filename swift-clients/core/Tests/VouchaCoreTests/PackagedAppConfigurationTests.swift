import Foundation
@testable import VouchaCore
import XCTest

final class PackagedAppConfigurationTests: XCTestCase {
    func testPackagedConfigurationWorksWithoutProcessEnvironment() {
        let config = AppConfig.from(environment: [:], packagedConfiguration: [
            "VOUCHA_API_BASE_URL": "https://staging.example.test",
            "VOUCHA_IMAGE_BASE_URL": "https://images.example.test",
            "VOUCHA_TURNSTILE_SITE_KEY": "packaged-public-site-key"
        ])

        XCTAssertEqual(config.baseURL.absoluteString, "https://staging.example.test")
        XCTAssertEqual(config.imageBaseURL.absoluteString, "https://images.example.test")
        XCTAssertEqual(config.requiredTurnstileSiteKey, "packaged-public-site-key")
    }

    func testEnvironmentOverridesPackagedConfigurationIncludingSharedPublicKey() {
        let packaged = [
            "VOUCHA_API_BASE_URL": "https://voucha.ai",
            "VOUCHA_IMAGE_BASE_URL": "https://images.voucha.ai",
            "VOUCHA_TURNSTILE_SITE_KEY": "packaged-public-site-key"
        ]
        let config = AppConfig.from(environment: [
            "VOUCHA_API_BASE_URL": "https://development.example.test",
            "VOUCHA_IMAGE_BASE_URL": "https://development-images.example.test",
            "NEXT_PUBLIC_CLOUDFLARE_TURNSTILE_SITE_KEY": "environment-public-site-key"
        ], packagedConfiguration: packaged)

        XCTAssertEqual(config.baseURL.host, "development.example.test")
        XCTAssertEqual(config.imageBaseURL.host, "development-images.example.test")
        XCTAssertEqual(config.turnstileSiteKey, "environment-public-site-key")
    }

    func testEmptyOverridesUsePackagedValuesAndEmptyConfigurationUsesProductionDefaults() {
        let config = AppConfig.from(environment: ["VOUCHA_API_BASE_URL": "  "], packagedConfiguration: [
            "VOUCHA_API_BASE_URL": " https://staging.example.test "
        ])
        XCTAssertEqual(config.baseURL.host, "staging.example.test")

        let defaults = AppConfig.from(environment: [:], packagedConfiguration: [
            "VOUCHA_API_BASE_URL": " ", "VOUCHA_TURNSTILE_SITE_KEY": "\n"
        ])
        XCTAssertEqual(defaults.baseURL.absoluteString, AppConfig.defaultAPIBaseURL)
        XCTAssertEqual(defaults.turnstileSiteKey, "")
    }
}
