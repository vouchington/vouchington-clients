import Foundation
@testable import VouchaFeatures
import XCTest

final class DraftDecimalFormatterTests: XCTestCase {
    func testFrenchFormattingOmitsTrailingZerosAndGroupsThousands() throws {
        let formatter = DraftDecimalFormatter.make(locale: Locale(identifier: "fr_FR"))
        let value = try XCTUnwrap(Decimal(string: "1234.50"))

        XCTAssertEqual(
            formatter.string(from: NSDecimalNumber(decimal: value)),
            "1\u{202F}234,5"
        )
    }

    func testParsingReturnsADecimalForTheWholeSource() throws {
        let formatter = DraftDecimalFormatter.make(locale: Locale(identifier: "en_US"))
        let source = "1.25"
        var object: AnyObject?
        var range = NSRange(location: 0, length: source.utf16.count)

        try formatter.getObjectValue(&object, for: source, range: &range)

        XCTAssertEqual(range, NSRange(location: 0, length: source.utf16.count))
        XCTAssertEqual((object as? NSDecimalNumber)?.decimalValue, Decimal(string: "1.25"))
    }

    func testFormattingOmitsTrailingZerosAndKeepsExtendedFractionDigits() throws {
        let formatter = DraftDecimalFormatter.make(locale: Locale(identifier: "en_US"))
        let value = try XCTUnwrap(Decimal(string: "1.50"))
        let precise = try XCTUnwrap(Decimal(string: "1.234567890123"))

        XCTAssertEqual(formatter.minimumFractionDigits, 0)
        XCTAssertEqual(formatter.maximumFractionDigits, 38)
        XCTAssertEqual(formatter.string(from: NSDecimalNumber(decimal: value)), "1.5")
        XCTAssertEqual(formatter.string(from: NSDecimalNumber(decimal: precise)), "1.234567890123")
    }
}
