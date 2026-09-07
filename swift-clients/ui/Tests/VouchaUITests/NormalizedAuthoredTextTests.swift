@testable import VouchaFeatures
import XCTest

final class NormalizedAuthoredTextTests: XCTestCase {
    func testTrimsAuthoredTextAndRetainsLanguageMetadata() throws {
        let text = try XCTUnwrap(NormalizedAuthoredText(
            text: "  \u{0645}\u{062b}\u{0627}\u{0644}  ",
            declaredLanguage: "ar",
            detectedLanguage: "en"
        ))

        XCTAssertEqual(text.value, "\u{0645}\u{062b}\u{0627}\u{0644}")
        XCTAssertEqual(text.declaredLanguage, "ar")
        XCTAssertEqual(text.detectedLanguage, "en")
    }

    func testBlankAuthoredTextClearsMetadataByReturningNoLeaf() {
        XCTAssertNil(NormalizedAuthoredText(
            text: " \n\t ",
            declaredLanguage: "ar",
            detectedLanguage: "en"
        ))
        XCTAssertNil(NormalizedAuthoredText(
            text: nil,
            declaredLanguage: "ar",
            detectedLanguage: "en"
        ))
    }

    func testGenericEntityOnlyExposesMetadataForANonblankAuthoredTitle() {
        let entity = NativeGenericEntity(
            id: "entity-1",
            slug: "fallback-slug",
            name: "Fallback name",
            title: " \n ",
            username: nil,
            subject: nil,
            status: nil,
            postType: nil,
            topicType: nil,
            feedType: nil,
            pathname: nil,
            hostname: nil,
            url: nil,
            description: nil,
            summary: nil,
            declaredLanguage: "ar",
            linguaRsDetectedLanguage: "en"
        )

        XCTAssertNil(entity.normalizedAuthoredTitle)
        XCTAssertEqual(entity.displayTitle(fallback: "fallback"), "Fallback name")
    }
}
