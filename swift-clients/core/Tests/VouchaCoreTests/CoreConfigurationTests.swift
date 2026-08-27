import Foundation
@testable import VouchaAuth
@testable import VouchaCore
import XCTest

// MARK: - AppConfig

final class AppConfigTests: XCTestCase {
    func testFallbackToProductionAPI() {
        guard ProcessInfo.processInfo.environment["VOUCHA_API_BASE_URL"] == nil else { return }
        let config = AppConfig.from(environment: [:])
        XCTAssertEqual(config.baseURL.scheme, "https")
        XCTAssertEqual(config.baseURL.host, "voucha.ai")
        XCTAssertNil(config.baseURL.port)
        XCTAssertEqual(config.turnstileSiteKey, "")
    }

    func testCustomURL() throws {
        let url = try XCTUnwrap(URL(string: "https://api.voucha.ai"))
        let config = AppConfig(baseURL: url, turnstileSiteKey: "test-site-key")
        XCTAssertEqual(config.baseURL.host, "api.voucha.ai")
        XCTAssertEqual(config.baseURL.scheme, "https")
    }

    func testSharedRuntimePublicTurnstileSiteKeyFallback() {
        let config = AppConfig.from(environment: [
            "VOUCHA_API_BASE_URL": "https://api.example.test",
            "NEXT_PUBLIC_CLOUDFLARE_TURNSTILE_SITE_KEY": "shared-site-key"
        ])

        XCTAssertEqual(config.turnstileSiteKey, "shared-site-key")
    }

    func testNativeTurnstileSiteKeyOverridesSharedRuntimePublicConfig() {
        let config = AppConfig.from(environment: [
            "VOUCHA_API_BASE_URL": "https://api.example.test",
            "VOUCHA_TURNSTILE_SITE_KEY": "native-site-key",
            "NEXT_PUBLIC_CLOUDFLARE_TURNSTILE_SITE_KEY": "shared-site-key"
        ])

        XCTAssertEqual(config.turnstileSiteKey, "native-site-key")
    }

    func testRequiredTurnstileSiteKeyReturnsConfiguredValue() {
        let config = AppConfig.from(environment: ["VOUCHA_TURNSTILE_SITE_KEY": "site-key"])

        XCTAssertEqual(config.requiredTurnstileSiteKey, "site-key")
    }
}

// MARK: - ApiFixtureLoader

final class ApiFixtureLoaderTests: XCTestCase {
    func testMissingFixtureReportsFailureAndReturnsFallbackData() {
        XCTExpectFailure("Missing API fixtures should report XCTest failures and return fallback data.") {
            XCTAssertEqual(
                ApiFixtureLoader.data("missing.fixture"),
                Data("{}".utf8)
            )
        }
    }
}

// MARK: - VouchaError

final class VouchaErrorTests: XCTestCase {
    func testDescriptions() {
        XCTAssertEqual(VouchaError.unauthorized.errorDescription, "Sign in to continue.")
        XCTAssertEqual(
            VouchaError.forbidden(preconditionCode: nil).errorDescription,
            "You don't have permission to do this."
        )
        XCTAssertEqual(VouchaError.notFound.errorDescription, "Not found.")
        XCTAssertEqual(VouchaError.sessionExpired.errorDescription, "Your session has expired. Please sign in again.")
        XCTAssertEqual(VouchaError.cancelled.errorDescription, "Cancelled.")
    }

    func testAPIDescriptions() {
        XCTAssertEqual(
            VouchaError.api(statusCode: 422, preconditionCode: "IDENTITY_REQUIRED").errorDescription,
            "API error: IDENTITY_REQUIRED"
        )
        XCTAssertEqual(
            VouchaError.apiMessage(
                statusCode: 422,
                preconditionCode: "INVALID_REFERRAL_URL",
                message: "Use a referral-program URL."
            ).errorDescription,
            "Use a referral-program URL."
        )
        XCTAssertEqual(VouchaError.api(statusCode: 500, preconditionCode: nil).errorDescription, "An error occurred.")
    }

    func testUnexpectedDescriptionsPreserveMessage() {
        XCTAssertEqual(VouchaError.unexpected("Boom").errorDescription, "Boom")
    }
}

// MARK: - AppConfig imageURL

extension AppConfigTests {
    func testImageURLReturnsNilForNilId() throws {
        let config = try AppConfig(
            baseURL: XCTUnwrap(URL(string: "http://localhost:2999")),
            turnstileSiteKey: "test-site-key"
        )
        XCTAssertNil(config.imageURL(forImageId: nil))
    }

    func testImageURLReturnsNilForEmptyId() throws {
        let config = try AppConfig(
            baseURL: XCTUnwrap(URL(string: "http://localhost:2999")),
            turnstileSiteKey: "test-site-key"
        )
        XCTAssertNil(config.imageURL(forImageId: ""))
    }

    func testImageURLBuildsCorrectPath() throws {
        let config = try AppConfig(
            baseURL: XCTUnwrap(URL(string: "http://localhost:2999")),
            imageBaseURL: XCTUnwrap(URL(string: "https://images.voucha.ai")),
            turnstileSiteKey: "test-site-key"
        )
        let url = config.imageURL(forImageId: "abc123", width: 96)
        XCTAssertEqual(url, "https://images.voucha.ai/images/abc123?w=96")
    }

    func testImageURLUsesDefaultWidth() throws {
        let config = try AppConfig(
            baseURL: XCTUnwrap(URL(string: "http://localhost:2999")),
            imageBaseURL: XCTUnwrap(URL(string: "https://images.voucha.ai")),
            turnstileSiteKey: "test-site-key"
        )
        let url = config.imageURL(forImageId: "xyz")
        XCTAssertTrue(url?.contains("w=96") == true)
    }

    func testURLResolverHandlesSchemeRelativeAndPathRelativeURLs() throws {
        let baseURL = try XCTUnwrap(URL(string: "https://api.voucha.ai"))

        XCTAssertEqual(
            VouchaURLResolver.absoluteString(for: "//cdn.example.com/image.jpg", relativeTo: baseURL),
            "https://cdn.example.com/image.jpg"
        )
        XCTAssertEqual(
            VouchaURLResolver.absoluteString(for: "/sideload/image.jpg", relativeTo: baseURL),
            "https://api.voucha.ai/sideload/image.jpg"
        )
        XCTAssertNil(VouchaURLResolver.absoluteString(for: "", relativeTo: baseURL))
    }
}
