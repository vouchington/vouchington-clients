import SwiftUI
import ViewInspector
@testable import VouchaDesignSystem
import XCTest

@MainActor
final class NativeHtmlContentWhitespaceTests: XCTestCase {
    func testContentViewOmitsRenderedWhitespaceWithoutApplyingAuthoredMetadata() throws {
        let sut = NativeHtmlContent(html: "<p> \n\t </p>", declaredLanguage: "ar")

        XCTAssertTrue(try sut.inspect().findAll(ViewType.Text.self).isEmpty)
    }
}
