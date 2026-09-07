import SwiftUI
@testable import VouchaDesignSystem
import XCTest

final class AuthoredContentLanguageTests: XCTestCase {
    func testDeclaredLanguageWinsOverDetectedLanguage() {
        XCTAssertEqual(AuthoredContentLanguage.resolved(declared: "fr-CA", detected: "ar"), "fr")
        XCTAssertEqual(AuthoredContentLanguage.layoutDirection(declared: "fr-CA", detected: "ar"), .leftToRight)
    }

    func testDetectedRtlLanguageSetsRtlDirection() {
        XCTAssertEqual(AuthoredContentLanguage.resolved(declared: nil, detected: "ar"), "ar")
        XCTAssertEqual(AuthoredContentLanguage.layoutDirection(declared: nil, detected: "ar"), .rightToLeft)
    }

    func testUnderscoreTagsUseSupportedBaseLanguage() {
        XCTAssertEqual(AuthoredContentLanguage.resolved(declared: "ar_EG", detected: nil), "ar")
        XCTAssertEqual(AuthoredContentLanguage.layoutDirection(declared: "ar_EG", detected: nil), .rightToLeft)
        XCTAssertEqual(AuthoredContentLanguage.resolved(declared: "en_US", detected: nil), "en")
    }

    func testInvalidOrMissingLanguageLeavesLeafInherited() {
        XCTAssertNil(AuthoredContentLanguage.resolved(declared: "invalid_language", detected: nil))
        XCTAssertNil(AuthoredContentLanguage.resolved(declared: "dv", detected: nil))
        XCTAssertNil(AuthoredContentLanguage.layoutDirection(declared: nil, detected: nil))
    }
}
