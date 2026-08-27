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
    func testContractRootRequiresExplicitEnvironment() {
        XCTAssertThrowsError(try FilamentsContractRoot.url(environment: [:])) { error in
            XCTAssertEqual(
                String(describing: error),
                "Missing required VOUCHA_FILAMENTS_CONTRACT_ROOT. Fetch Filaments contracts before running native contract tests."
            )
        }
    }

    func testContractRootRejectsInvalidExplicitDirectory() {
        XCTAssertThrowsError(
            try FilamentsContractRoot.url(environment: [
                FilamentsContractRoot.environmentKey: "/definitely-not-a-filaments-contract-root"
            ])
        ) { error in
            XCTAssertTrue(String(describing: error).contains("must name an existing directory"))
        }
    }

    func testContractRootNeverDiscoversFilamentsLookingAncestorOrSiblingWithoutEnvironment() throws {
        let temporaryRoot = FileManager.default.temporaryDirectory
            .appendingPathComponent("filaments-contract-root-\(UUID().uuidString)", isDirectory: true)
        let ancestor = temporaryRoot.appendingPathComponent("filaments", isDirectory: true)
        let sibling = temporaryRoot.appendingPathComponent("filaments-sibling", isDirectory: true)
        defer { try? FileManager.default.removeItem(at: temporaryRoot) }

        for root in [ancestor, sibling] {
            let fixtures = root.appendingPathComponent("api-fixtures/v1", isDirectory: true)
            try FileManager.default.createDirectory(at: fixtures, withIntermediateDirectories: true)
            try Data("{\"fixtures\":[]}".utf8).write(to: fixtures.appendingPathComponent("manifest.json"))
        }

        XCTAssertThrowsError(try FilamentsContractRoot.url(environment: [:])) { error in
            XCTAssertEqual(
                String(describing: error),
                "Missing required VOUCHA_FILAMENTS_CONTRACT_ROOT. Fetch Filaments contracts before running native contract tests."
            )
        }
    }

    func testContractRootRejectsSymbolicLinkedRequiredPath() throws {
        let root = try makeFixtureRoot()
        defer { try? FileManager.default.removeItem(at: root) }
        let manifest = root.appendingPathComponent("api-fixtures/v1/manifest.json")
        let movedManifest = root.appendingPathComponent("manifest.json")
        try FileManager.default.moveItem(at: manifest, to: movedManifest)
        try FileManager.default.createSymbolicLink(at: manifest, withDestinationURL: movedManifest)

        XCTAssertThrowsError(
            try FilamentsContractRoot.url(
                environment: [FilamentsContractRoot.environmentKey: root.path],
                requiredPaths: ["api-fixtures/v1/manifest.json"]
            )
        ) { error in
            XCTAssertTrue(String(describing: error).contains("must not contain symbolic links"))
        }
    }

    func testFixtureBodyFileRejectsAbsoluteAndTraversalPaths() throws {
        let root = try makeFixtureRoot()
        defer { try? FileManager.default.removeItem(at: root) }

        for path in [
            "/outside.json",
            "C:fixture.json",
            "../outside.json",
            "nested/../../outside.json",
            "./fixture.json",
            "nested//fixture.json",
            "nested\\\\fixture.json",
            "nested/fixture:copy.json"
        ] {
            XCTAssertThrowsError(try ApiFixtureLoader.fixtureURL(path, root: root), path) { error in
                XCTAssertTrue(String(describing: error).contains("must be a relative path"))
            }
        }
    }

    func testFixtureBodyFileRejectsSymbolicLinkComponentsAndFiles() throws {
        let root = try makeFixtureRoot()
        defer { try? FileManager.default.removeItem(at: root) }

        let fixtureRoot = root.appendingPathComponent("api-fixtures/v1", isDirectory: true)
        let outside = root.appendingPathComponent("outside.json")
        try Data("{}".utf8).write(to: outside)
        try FileManager.default.createSymbolicLink(
            at: fixtureRoot.appendingPathComponent("linked-directory"),
            withDestinationURL: root
        )
        try FileManager.default.createSymbolicLink(
            at: fixtureRoot.appendingPathComponent("linked-file.json"),
            withDestinationURL: outside
        )

        for path in ["linked-directory/outside.json", "linked-file.json"] {
            XCTAssertThrowsError(try ApiFixtureLoader.fixtureURL(path, root: root), path) { error in
                XCTAssertTrue(String(describing: error).contains("must not contain symbolic links"))
            }
        }
    }

    func testFixtureBodyFileRejectsSymbolicLinkedConfiguredRoot() throws {
        let root = try makeFixtureRoot()
        defer { try? FileManager.default.removeItem(at: root) }

        let configuredRoot = root.appendingPathComponent("configured-root", isDirectory: true)
        try FileManager.default.createSymbolicLink(at: configuredRoot, withDestinationURL: root)

        XCTAssertThrowsError(try ApiFixtureLoader.fixtureURL("fixture.json", root: configuredRoot)) { error in
            XCTAssertTrue(String(describing: error).contains("must not contain symbolic links"))
        }
    }

    private func makeFixtureRoot() throws -> URL {
        let temporaryRoot = FileManager.default.temporaryDirectory
            .appendingPathComponent("filaments-contract-root-\(UUID().uuidString)", isDirectory: true)
        let fixtureRoot = temporaryRoot.appendingPathComponent("api-fixtures/v1", isDirectory: true)
        try FileManager.default.createDirectory(at: fixtureRoot, withIntermediateDirectories: true)
        try Data("{\"fixtures\":[]}".utf8).write(to: fixtureRoot.appendingPathComponent("manifest.json"))
        try Data("{}".utf8).write(to: fixtureRoot.appendingPathComponent("fixture.json"))
        return temporaryRoot
    }

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
