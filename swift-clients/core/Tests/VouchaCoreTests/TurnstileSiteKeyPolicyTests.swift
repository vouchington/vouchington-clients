import Foundation
@testable import VouchaCore
import XCTest

final class TurnstileSiteKeyPolicyTests: XCTestCase {
    private let releaseSiteKey = "0x4AAAAAAAAexample"

    func testDebugResolvesAMissingKeyToTheAlwaysPassSiteKey() {
        XCTAssertEqual(
            TurnstileSiteKeyPolicy.resolve("", configuration: .debug),
            .key(TurnstileSiteKeyPolicy.alwaysPassSiteKey)
        )
        XCTAssertEqual(
            TurnstileSiteKeyPolicy.resolve(" \n", configuration: .debug),
            .key(TurnstileSiteKeyPolicy.alwaysPassSiteKey)
        )
        let config = AppConfig.from(environment: [:])
        XCTAssertEqual(config.turnstileSiteKey, "")
        XCTAssertEqual(config.requiredTurnstileSiteKey, TurnstileSiteKeyPolicy.alwaysPassSiteKey)
    }

    func testDebugKeepsAnExplicitOverride() {
        XCTAssertEqual(
            TurnstileSiteKeyPolicy.resolve(releaseSiteKey, configuration: .debug),
            .key(releaseSiteKey)
        )
        XCTAssertEqual(
            TurnstileSiteKeyPolicy.resolve("2x00000000000000000000AB", configuration: .debug),
            .key("2x00000000000000000000AB")
        )
    }

    func testReleaseRejectsAMissingKeyAndEveryCloudflareTestKey() {
        XCTAssertEqual(
            TurnstileSiteKeyPolicy.resolve(" \t", configuration: .release),
            .rejected(TurnstileSiteKeyPolicy.missingReleaseKeyMessage)
        )
        for testKey in TurnstileSiteKeyPolicy.testSiteKeys {
            XCTAssertEqual(
                TurnstileSiteKeyPolicy.resolve(" \(testKey) ", configuration: .release),
                .rejected(TurnstileSiteKeyPolicy.rejectedTestKeyMessage),
                testKey
            )
        }
    }

    func testReleaseAcceptsAnExplicitNonTestKey() {
        XCTAssertEqual(
            TurnstileSiteKeyPolicy.resolve(" \(releaseSiteKey) ", configuration: .release),
            .key(releaseSiteKey)
        )
        let config = AppConfig.from(environment: ["VOUCHA_TURNSTILE_SITE_KEY": releaseSiteKey])
        XCTAssertEqual(config.requiredTurnstileSiteKey, releaseSiteKey)
    }

    func testCurrentDebugConfigurationMatchesTheCompiledTests() {
        XCTAssertEqual(TurnstileBuildConfiguration.current, .debug)
        XCTAssertEqual(TurnstileSiteKeyPolicy.required(""), TurnstileSiteKeyPolicy.alwaysPassSiteKey)
    }
}
