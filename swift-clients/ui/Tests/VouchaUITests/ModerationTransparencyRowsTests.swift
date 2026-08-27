import Foundation
@testable import VouchaFeatures
import VouchaModels
import XCTest

final class ModerationTransparencyRowsTests: XCTestCase {
    func testAllRangeRendersLocalizedMonthAndYear() throws {
        let bucket = try bucket()
        let rows = moderationTransparencyRows([bucket], range: .all)

        XCTAssertEqual(rows.first?.detail, "Released August 2026 · Spam · 20")
        XCTAssertTrue(rows[0].localizedDetail(locale: Locale(identifier: "fr")).contains("août 2026"))
    }

    func testDayRangePreservesReleasedDay() throws {
        let bucket = try bucket()
        let rows = moderationTransparencyRows([bucket], range: .days30)

        XCTAssertEqual(rows.first?.detail, "Released Aug 1, 2026 · Spam · 20")
    }

    private func bucket() throws -> ModerationTransparencyBucket {
        try JSONDecoder().decode(
            ModerationTransparencyBucket.self,
            from: Data(#"{"date":"2026-08-01","metric":"reports","category":"spam","count":20}"#.utf8)
        )
    }
}
