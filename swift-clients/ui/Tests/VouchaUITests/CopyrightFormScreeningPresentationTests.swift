import Foundation
@testable import VouchaFeatures
import VouchaLocalization
import VouchaModels
import XCTest

final class CopyrightFormScreeningPresentationTests: XCTestCase {
    func testEachScreeningStateMapsToItsOwnCopyAndUnknownStaysHidden() throws {
        let expected: [(String, UiMessageKey?)] = [
            ("pending", .nativeSwiftAiRunStatusCopyrightScreeningPending),
            ("failed", .nativeSwiftAiRunStatusCopyrightScreeningFailed),
            ("completed", .nativeSwiftAiRunStatusCopyrightScreeningCompleted),
            ("future-state", nil)
        ]
        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        for (state, key) in expected {
            let json = Data("""
            {"attempt_id":"attempt-1","state":"\(state)","recommendation":null,"rationale":null,"guidance":null}
            """.utf8)
            let screening = try decoder.decode(CopyrightStaffFormScreening.self, from: json)
            XCTAssertEqual(screening.attemptId, "attempt-1")
            XCTAssertEqual(screening.statusKey, key, state)
        }
    }
}
